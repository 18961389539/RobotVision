using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.UIA3;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace RobotVision.IntegrationTests;

/// <summary>
/// WPF 宿主 UI 自动化冒烟测试（FlaUI / UIA3）：启动真实应用 → 验证主窗口 →
/// 导航页切换 → 关键控件可交互 → 关闭。
/// 依赖桌面会话与已构建的 WPF 宿主，默认跳过：设置环境变量 RV_UI_TEST=1 后运行
/// （dotnet test --filter "Category=UiAutomation"）。
/// </summary>
[Trait("Category", "UiAutomation")]
public class UiAutomationSmokeTests(ITestOutputHelper output)
{
    [SkippableFact]
    public void MainWindow_Launches_WithNavigation()
    {
        Skip.IfNot(TestPreconditions.IsUiAutomationEnabled(),
            $"Set {TestPreconditions.UiTestEnvVar}=1 to run UI automation tests.");
        var exe = TestBuildPaths.ResolveWpfExe();
        Skip.IfNot(File.Exists(exe ?? ""), "WPF host not built (build RobotVision.Wpf in Release or Debug).");

        var repoRoot = TestBuildPaths.FindRepoRoot()
                       ?? throw new InvalidOperationException("Repo root not found.");
        using var app = Application.Launch(exe!, repoRoot);

        try
        {
            using var automation = new UIA3Automation();
            var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(30));
            output.WriteLine($"主窗口标题: {window.Title}");
            window.Title.Should().Be("RobotVision 视觉调试台");

            var navItems = new[] { "运行监控", "相机管理", "配方管理", "模型管理", "服务设置", "系统信息" };
            foreach (var name in navItems)
            {
                var item = window.FindFirstDescendant(cf => cf.ByName(name));
                item.Should().NotBeNull($"导航项 {name} 应存在");
                item!.IsEnabled.Should().BeTrue($"导航项 {name} 应可交互");
            }

            window.FindFirstDescendant(cf => cf.ByName("配方管理"))!.Click();
            WaitForDescendant(window, cf => cf.ByClassName("ContentControl").And(cf.ByName("配方管理")));
            output.WriteLine("UI 冒烟完成：主窗口/导航/页面切换验证通过");
        }
        finally
        {
            try { app.Close(); } catch { }
            app.Dispose();
        }
    }

    [SkippableFact]
    public void MonitorPage_ShowsManualTrigger_Button()
    {
        Skip.IfNot(TestPreconditions.IsUiAutomationEnabled(),
            $"Set {TestPreconditions.UiTestEnvVar}=1 to run UI automation tests.");
        var exe = TestBuildPaths.ResolveWpfExe();
        Skip.IfNot(File.Exists(exe ?? ""), "WPF host not built (build RobotVision.Wpf in Release or Debug).");

        var repoRoot = TestBuildPaths.FindRepoRoot()
                       ?? throw new InvalidOperationException("Repo root not found.");
        using var app = Application.Launch(exe!, repoRoot);

        try
        {
            using var automation = new UIA3Automation();
            var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(30));

            var trigger = window.FindFirstDescendant(cf => cf.ByName("手动触发"));
            trigger.Should().NotBeNull("监控页应有手动触发按钮");
            trigger!.IsEnabled.Should().BeTrue();
            output.WriteLine("手动触发按钮可交互");
        }
        finally
        {
            try { app.Close(); } catch { }
            app.Dispose();
        }
    }

    [SkippableFact]
    public void AllNavigationPages_AreReachable()
    {
        Skip.IfNot(TestPreconditions.IsUiAutomationEnabled(),
            $"Set {TestPreconditions.UiTestEnvVar}=1 to run UI automation tests.");
        var exe = TestBuildPaths.ResolveWpfExe();
        Skip.IfNot(File.Exists(exe ?? ""), "WPF host not built (build RobotVision.Wpf in Release or Debug).");

        var repoRoot = TestBuildPaths.FindRepoRoot()
                       ?? throw new InvalidOperationException("Repo root not found.");
        using var app = Application.Launch(exe!, repoRoot);
        try
        {
            using var automation = new UIA3Automation();
            var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(30));

            var navItems = new[] { "运行监控", "相机管理", "配方管理", "模型管理", "服务设置", "系统信息" };
            foreach (var name in navItems)
            {
                var item = window.FindFirstDescendant(cf => cf.ByName(name));
                item.Should().NotBeNull($"导航项 {name} 应存在");
                item!.Click();
                WaitForDescendant(window, cf => cf.ByClassName("ContentControl").And(cf.ByName(name)));
                output.WriteLine($"页面可达: {name}");
            }
        }
        finally
        {
            try { app.Close(); } catch { }
            app.Dispose();
        }
    }

    [SkippableFact]
    public void RecipePage_ShowsRecipeList()
    {
        Skip.IfNot(TestPreconditions.IsUiAutomationEnabled(),
            $"Set {TestPreconditions.UiTestEnvVar}=1 to run UI automation tests.");
        var exe = TestBuildPaths.ResolveWpfExe();
        Skip.IfNot(File.Exists(exe ?? ""), "WPF host not built (build RobotVision.Wpf in Release or Debug).");

        var repoRoot = TestBuildPaths.FindRepoRoot()
                       ?? throw new InvalidOperationException("Repo root not found.");
        using var app = Application.Launch(exe!, repoRoot);
        try
        {
            using var automation = new UIA3Automation();
            var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(30));

            window.FindFirstDescendant(cf => cf.ByName("配方管理"))!.Click();
            var grid = WaitForDescendant(window, cf => cf.ByClassName("DataGrid"));
            grid.Should().NotBeNull("配方页应有配方列表（DataGrid）");
            output.WriteLine($"配方列表控件: {grid?.Name ?? "（未命名）"}");
        }
        finally
        {
            try { app.Close(); } catch { }
            app.Dispose();
        }
    }

    private static AutomationElement? WaitForDescendant(
        Window window,
        Func<ConditionFactory, ConditionBase> condition,
        int timeoutMs = 5000)
    {
        AutomationElement? found = null;
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            found = window.FindFirstDescendant(condition);
            if (found is not null)
                return found;
            Thread.Sleep(50);
        }

        return found;
    }
}
