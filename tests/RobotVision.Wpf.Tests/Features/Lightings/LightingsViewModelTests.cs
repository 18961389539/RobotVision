using FluentAssertions;
using RobotVision.Core;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Hosting.Lighting;
using RobotVision.Infrastructure.Lighting;
using RobotVision.WpfHost.Features.Lightings;
using RobotVision.WpfHost.Shared;

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

    private LightingsViewModel CreateVm() =>
        new(_cfg, TestInfra.LightingFacade(_lighting), _store, _recipes,
            LightControllerTypeRegistry.CreateDefault(), new TestDialogService(), TestLog.Null<LightingsViewModel>());

    [Fact]
    public void Ctor_LoadsConfiguredControllers()
    {
        var vm = CreateVm();

        vm.Items.Should().ContainSingle();
        vm.Items[0].Id.Should().Be("light_none");
        vm.Items[0].Type.Should().Be("None");
        vm.Message.Should().Contain("共 1 个光源控制器");
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
