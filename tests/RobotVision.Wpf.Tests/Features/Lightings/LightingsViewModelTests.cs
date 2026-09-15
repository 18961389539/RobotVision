using FluentAssertions;
using RobotVision.Core;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Hosting.Lighting;
using RobotVision.Infrastructure.Lighting;
using RobotVision.WpfHost.Features.Lightings;
using RobotVision.WpfHost.Shared;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace RobotVision.Wpf.Tests;

public sealed class LightingsViewModelTests : IDisposable
{
    private readonly TestInfra.TempDir _dir = new("rv_lightings");
    private readonly AppConfig _cfg;
    private readonly LightingManager _lighting = new();
    private readonly LightingConfigStore _store;
    private readonly RecipeLoader _recipes;

    public LightingsViewModelTests()
    {
        _cfg = TestInfra.CreateAppConfig(_dir.Path);
        _cfg.LightControllers.Add(new LightControllerConfig { Id = "light_none", Type = "None" });
        _store = new LightingConfigStore(_cfg, Path.Combine(_dir.Path, "appsettings.json"));
        _recipes = new RecipeLoader(_dir.CreateSub("recipes"));
    }

    public void Dispose()
    {
        _lighting.Dispose();
        _dir.Dispose();
    }

    private LightingsViewModel CreateVm()
    {
        var vm = new LightingsViewModel(_cfg, TestInfra.LightingFacade(_lighting), _store, _recipes,
            LightControllerTypeRegistry.CreateDefault(), new TestDialogService(), TestLog.Null<LightingsViewModel>());
        vm.Refresh();
        return vm;
    }

    [Fact]
    public void Ctor_LoadsConfiguredControllers()
    {
        var vm = CreateVm();

        vm.Items.Should().ContainSingle();
        vm.Items[0].Id.Should().Be("light_none");
        vm.Items[0].Type.Should().Be("None");
        vm.Selected.Should().NotBeNull();
        vm.Selected!.Id.Should().Be("light_none");
        vm.HasSelection.Should().BeTrue();
        vm.NewId.Should().BeEmpty();
        vm.Message.Should().Contain("共 1 个光源控制器");
    }

    [Fact]
    public void AutoTurnOffSuppressed_ReflectsRuntimeSwitch()
    {
        // 默认（产线行为）：不屏蔽，页面不显示调试提示
        CreateVm().AutoTurnOffSuppressed.Should().BeFalse();

        // 宿主按 ROBOTVISION_KEEP_LIGHT_ON 置位后，页面须显式提示——
        // 否则灯一直亮着无从解释（该开关默认关闭，只在临时排查期打开）
        _lighting.SuppressAutoTurnOff = true;
        CreateVm().AutoTurnOffSuppressed.Should().BeTrue();
    }

    [Fact]
    public void Page_SuppressBanner_ReflectsSwitchAndBindingResolves()
    {
        // 默认关闭：提示条隐藏，不干扰正常页面
        AssertSuppressBanner(switchOn: false, expected: Visibility.Collapsed);

        // 打开：提示条必须出现。
        // Visibility 绑定是运行期静默失败——属性名拼错只会让提示条永不显示，不报任何错，
        // 故除断言可见性外还须校验 BindingExpression.Status（见 Page_... 的 PathError 检查）。
        AssertSuppressBanner(switchOn: true, expected: Visibility.Visible);
    }

    private void AssertSuppressBanner(bool switchOn, Visibility expected)
    {
        TestInfra.RunSta(() =>
        {
            _lighting.SuppressAutoTurnOff = switchOn;
            var page = new LightingsPage(CreateVm(), new TestDialogService());
            page.Measure(new Size(1200, 900));
            page.Arrange(new Rect(0, 0, 1200, 900));
            page.UpdateLayout();

            // FindName 而非遍历：x:Name 一旦被删，这里先失败，锚点缺失不会被静默放过
            var banner = page.FindName("autoTurnOffBanner").Should().BeOfType<Border>().Subject;
            var expr = BindingOperations.GetBindingExpression(banner, UIElement.VisibilityProperty);
            expr.Should().NotBeNull("提示条必须用绑定驱动可见性，不能写死");
            expr!.Status.Should().NotBe(BindingStatus.PathError);
            banner.Visibility.Should().Be(expected);
        });
    }

    [Fact]
    public void Add_WithEmptyId_ShowsValidationMessage()
    {
        var vm = CreateVm();
        vm.NewId = "   ";

        vm.AddCommand.Execute(null);

        vm.Message.Should().Contain("请先填写控制器 Id");
        vm.Items.Should().ContainSingle();
    }

    [Fact]
    public void Add_NoneController_RegistersRuntime()
    {
        var vm = CreateVm();
        vm.NewId = "light_lab";
        vm.NewType = "None";

        vm.AddCommand.Execute(null);

        vm.Items.Should().HaveCount(2);
        vm.Items.Should().Contain(i => i.Id == "light_lab" && i.Registered);
        vm.Message.Should().Contain("已添加");
        _lighting.IsRegistered("light_lab").Should().BeTrue();
    }

    [Fact]
    public void BrightnessSetter_RejectsNonFinite_KeepPreviousValue()
    {
        var vm = CreateVm();
        vm.Brightness = 128;

        vm.Brightness = double.NaN;
        vm.Brightness.Should().Be(128);

        vm.Brightness = double.PositiveInfinity;
        vm.Brightness.Should().Be(128);

        vm.Brightness = double.NegativeInfinity;
        vm.Brightness.Should().Be(128);
    }

    [Fact]
    public void BrightnessSetter_RoundsToInteger()
    {
        var vm = CreateVm();

        vm.Brightness = 12.6;

        vm.Brightness.Should().Be(13);
    }

    [Fact]
    public void Delete_Selected_RemovesController()
    {
        var vm = CreateVm();
        vm.NewId = "light_tmp";
        vm.NewType = "None";
        vm.AddCommand.Execute(null);
        vm.Selected = vm.Items.First(i => i.Id == "light_tmp");

        vm.DeleteCommand.Execute(null);

        vm.Items.Should().NotContain(i => i.Id == "light_tmp");
        _lighting.IsRegistered("light_tmp").Should().BeFalse();
    }
}
