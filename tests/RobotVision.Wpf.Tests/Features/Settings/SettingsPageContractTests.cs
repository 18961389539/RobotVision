using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Communication;
using RobotVision.WpfHost.Features.Settings;
using RobotVision.WpfHost.Shared;
using Xunit;

namespace RobotVision.Wpf.Tests;

/// <summary>
/// 服务设置页的 XAML ↔ ViewModel 契约测试。
///
/// 为什么需要单独一组：本页新引入了三类"编译期查不出、运行期还静默"的契约。
/// 1. 字段级错误高亮走索引器绑定 <c>{Binding [TcpPort]}</c>。索引器对任何字符串都返回 bool，
///    字段名拼错**不会**产生绑定错误，只会让那个字段永远不标红。
/// 2. 左侧锚点栏靠 <c>x:Name="anchor_&lt;分组&gt;"</c> + <c>FindName</c> 做滚动定位，
///    SettingsGroup 常量与 x:Name 一旦脱节，点击锚点就静默不滚动。
/// 3. <c>{Binding Xxx}</c> 的属性名 Xxx 拼错不会有编译错误，只会在运行期把控件留空。
///
/// 因此这里用三种手段分别盯住：XAML 文本 ↔ SettingsField.All 对账、分组常量 ↔ 锚点元素对账、
/// 真把页面构造出来强制布局后检查所有绑定表达式不存在 PathError。
/// </summary>
public sealed class SettingsPageContractTests : IDisposable
{
    private static readonly string Xaml = File.ReadAllText(ResolveXamlPath());

    private readonly TestInfra.TempDir _dir = new("rv_settings_page");
    private readonly AppConfig _cfg;
    private readonly TcpServerManager _tcp;
    private readonly VisionService _vision;
    private readonly FailureImageStore _failures;
    private readonly ResultLogStore _results;
    private readonly SuccessCaptureStore _captures;
    private readonly AppSettingsStore _store;

    public SettingsPageContractTests()
    {
        _cfg = TestInfra.CreateAppConfig(_dir.Path);
        _tcp = TestInfra.CreateTcp();
        _vision = TestInfra.CreateVisionService(_cfg.RecipesFolder);
        _failures = new FailureImageStore(
            new FailureImageConfig { Folder = _cfg.FailureImage.Folder, RetainedCount = 200 },
            NullLogger<FailureImageStore>.Instance);
        _results = new ResultLogStore(
            new ResultLogConfig { Folder = Path.Combine(_dir.Path, "results") },
            NullLogger<ResultLogStore>.Instance);
        _captures = new SuccessCaptureStore(
            new CaptureSuccessConfig { Folder = Path.Combine(_dir.Path, "captures") },
            NullLogger<SuccessCaptureStore>.Instance);
        _store = new AppSettingsStore(_cfg, Path.Combine(_dir.Path, "appsettings.json"));
    }

    public void Dispose()
    {
        _results.Dispose();
        _tcp.Dispose();
        _dir.Dispose();
    }

    private SettingsViewModel CreateVm() =>
        new(_cfg, TestInfra.TcpFacade(_tcp), _vision, _failures, _results, _captures, _store,
            new TestDialogService(), TestLog.Null<SettingsViewModel>());

    /// <summary>XAML 里出现的每个 <c>[字段名]</c> 索引器实参都必须存在于 SettingsField.All。</summary>
    [Fact]
    public void Indexer_bindings_only_reference_known_settings_fields()
    {
        var used = Regex.Matches(Xaml, @"\[\s*([A-Za-z_][A-Za-z0-9_]*)\s*\]")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToList();

        // 防呆：正则没匹配到说明写法变了（例如换成了别的绑定形式），此时测试会变成空转
        used.Should().NotBeEmpty("页面应至少有一处字段级错误高亮的索引器绑定");

        var unknown = used.Where(f => !SettingsField.All.Contains(f, StringComparer.Ordinal)).ToList();
        unknown.Should().BeEmpty(
            "XAML 引用了 SettingsField.All 之外的字段名，索引器会静默返回 false，该字段永远不会标红："
            + string.Join("、", unknown));

        // 反向提示：漏接高亮的字段本身不是错误（有些字段不适合标红），但值得知道覆盖面
        var covered = SettingsField.All.Count(f => used.Contains(f, StringComparer.Ordinal));
        covered.Should().BeGreaterThan(10, "至少应覆盖主要输入字段");
    }

    /// <summary>每个 SettingsGroup 常量都必须有对应的 <c>anchor_&lt;key&gt;</c> 元素，否则锚点滚动静默失效。</summary>
    [Fact]
    public void Every_settings_group_has_a_matching_anchor_element()
    {
        var keys = GroupKeys();
        keys.Should().HaveCount(10);

        foreach (var key in keys)
        {
            Xaml.Should().Contain($"x:Name=\"anchor_{key}\"",
                $"分组 {key} 缺少 anchor_{key} 元素，锚点栏点击或校验失败定位时不会滚动过去");
        }
    }

    /// <summary>锚点栏条目与页面分组一一对应（多一个锚点指向不存在的分组，点了就不滚动）。</summary>
    [Fact]
    public void Anchor_rail_covers_exactly_the_known_groups()
    {
        var vm = CreateVm();
        vm.Anchors.Select(a => a.Key).Should().BeEquivalentTo(GroupKeys());
        vm.Anchors.Select(a => a.Label).Should().OnlyHaveUniqueItems();
    }

    /// <summary>
    /// 真把页面构造出来：解析期会解析所有 {StaticResource}（键拼错即抛），
    /// 强制布局后检查所有绑定表达式没有 PathError（属性名拼错即命中）。
    /// </summary>
    [Fact]
    public void Page_loads_and_every_binding_resolves()
    {
        var pathErrors = new List<string>();
        var bindingCount = 0;

        TestInfra.RunSta(() =>
        {
            TestInfra.EnsureWpfApp();
            var vm = CreateVm();

            // 展开全部分组：折叠卡片里的绑定若不被求值，PathError 检查就是空转
            vm.IsPlcDebugExpanded = true;
            vm.IsSuccessExpanded = true;
            vm.IsFileLogExpanded = true;
            vm.IsWhitelistExpanded = true;
            vm.IsEndpointExpanded = true;

            var page = new SettingsPage(vm, new TestDialogService());
            page.Measure(new Size(1600, 1400));
            page.Arrange(new Rect(0, 0, 1600, 1400));
            page.UpdateLayout();

            foreach (var key in GroupKeys())
            {
                page.FindName($"anchor_{key}").Should().NotBeNull(
                    $"anchor_{key} 未在页面名称作用域中注册，FindName 会返回 null 导致滚动定位失效");
            }

            var anchorList = page.FindName("AnchorList") as ListBox;
            anchorList.Should().NotBeNull();
            anchorList!.ItemsSource.Should().BeSameAs(vm.Anchors);

            var expressions = CollectBindingExpressions(page).ToList();
            bindingCount = expressions.Count;
            pathErrors = expressions
                .Where(e => e.Status == BindingStatus.PathError)
                .Select(e => $"{e.ParentBinding.Path?.Path ?? "(空路径)"} @ {e.Target?.GetType().Name}.{e.TargetProperty?.Name}")
                .Distinct(StringComparer.Ordinal)
                .ToList();
        });

        // 防呆：枚举不到足够多的绑定说明布局没真正展开，后面的断言会变成空转
        bindingCount.Should().BeGreaterThan(50, "页面绑定数量异常偏少，说明控件模板未展开、检查失去意义");
        pathErrors.Should().BeEmpty("以下绑定存在 PathError（属性名或路径拼错）：" + string.Join("；", pathErrors));
    }

    /// <summary>SettingsGroup 的常量值（自动跟随源码，避免手抄清单过期）。</summary>
    private static string[] GroupKeys() =>
        typeof(SettingsGroup)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f is { IsLiteral: true, FieldType: var t } && t == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToArray();

    /// <summary>同时走逻辑树与视觉树枚举 BindingExpression（模板生成的可视元素只在视觉树里）。</summary>
    private static IEnumerable<BindingExpression> CollectBindingExpressions(DependencyObject root)
    {
        var seen = new HashSet<DependencyObject>();
        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (!seen.Add(node))
                continue;

            var local = node.GetLocalValueEnumerator();
            while (local.MoveNext())
            {
                if (local.Current.Value is BindingExpression expression)
                    yield return expression;
            }

            foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
                stack.Push(child);

            if (node is not Visual)
                continue;
            var count = VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < count; i++)
                stack.Push(VisualTreeHelper.GetChild(node, i));
        }
    }

    /// <summary>
    /// 由本文件路径反推仓库根，再定位页面 XAML 源码（测试输出目录里没有 .xaml 副本）。
    /// 用「向上找标记文件」而不是硬编码 <c>..\..\..</c> 层数：本文件将来挪目录也不会静默失效。
    /// </summary>
    private static string ResolveXamlPath([CallerFilePath] string thisFile = "")
    {
        const string relative = "src/RobotVision.Wpf/Features/Settings/SettingsPage.xaml";
        var dir = Path.GetDirectoryName(thisFile);
        while (dir is { Length: > 0 })
        {
            var candidate = Path.Combine(dir, "src", "RobotVision.Wpf", "Features", "Settings", "SettingsPage.xaml");
            if (File.Exists(candidate))
                return candidate;
            dir = Path.GetDirectoryName(dir);
        }

        throw new FileNotFoundException(
            $"未能从测试文件路径向上找到 {relative}（起点：{thisFile}）");
    }
}
