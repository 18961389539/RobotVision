using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Communication;
using RobotVision.Infrastructure.Inference;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Vision.Inference.Strategies;
using RobotVision.Infrastructure.Lighting;
using RobotVision.WpfHost.Features.Recipe;

namespace RobotVision.Wpf.Tests;

public sealed class RecipePageRoiCoordinatorTests : IDisposable
{
    private readonly TestInfra.TempDir _dir = new("rv_page_roi");
    private readonly CameraManager _cameras = new();
    private readonly VisionService _vision;
    private readonly TcpServerManager _tcp;
    private readonly CalibrationManager _calibration = new();
    private readonly ModelManager _models;
    private readonly LightingManager _lighting = new();
    private readonly AngleStrategyTypeRegistry _angleRegistry = new();
    private readonly AssetIntegrityChecker _assets;
    private readonly RecipeLoader _loader;

    public RecipePageRoiCoordinatorTests()
    {
        var recipeFolder = _dir.CreateSub("recipes");
        File.WriteAllText(Path.Combine(recipeFolder, "R1.json"),
            """{"cameraId":"cam_v","angleMode":"MaskTemplate","models":["a.onnx"],"template":{"refineMethod":"Template"}}""");
        _cameras.Register(new VirtualCamera("cam_v", 200, 100, "Bars"));
        _loader = new RecipeLoader(recipeFolder);
        _models = new ModelManager(_dir.Path);
        _assets = new AssetIntegrityChecker(
            TestInfra.CreateAppConfig(_dir.Path), _models, _calibration, NullLogger<AssetIntegrityChecker>.Instance);
        _vision = TestInfra.CreateVisionService(recipeFolder, _cameras);
        _tcp = TestInfra.CreateTcp();
    }

    public void Dispose()
    {
        _models.Dispose();
        _lighting.Dispose();
        _calibration.Dispose();
        _cameras.Dispose();
        _tcp.Dispose();
        _dir.Dispose();
    }

    private RecipeViewModel CreateVm()
    {
        var cfg = TestInfra.CreateAppConfig(_dir.Path);
        cfg.Cameras.Add(new CameraConfig { Id = "cam_v", Type = "Virtual" });
        return new RecipeViewModel(
            _loader, cfg, TestInfra.CameraFacade(_cameras), TestInfra.ModelFacade(_models),
            TestInfra.CalibrationFacade(_calibration), TestInfra.LightingFacade(_lighting),
            TestInfra.AngleCatalog(_angleRegistry), _assets, new TestDialogService(), new NullRecipeWindowService(),
            TestInfra.CreateRecipeTestService(_vision, _cameras, _models, _calibration, _lighting),
            TestInfra.MaskTeach(), TestInfra.RefineGuidance(), TestInfra.Overlay(),
            TestLog.Null<RecipeViewModel>());
    }

    [Fact]
    public void BeginRoiDraw_TemplateGuard_SkipsNonFeatureModes()
    {
        var vm = CreateVm();
        try
        {
            vm.Editor.AngleMode = AngleMode.KeyPointLine;
            var testVp = new FakeRoiViewport();
            var roiVp = new FakeRoiViewport();
            using var coord = new RecipePageRoiCoordinator(vm, testVp, roiVp);
            coord.Wire();

            coord.BeginRoiDraw(template: true);

            testVp.RoiModeStarted.Should().BeFalse();
            roiVp.RoiModeStarted.Should().BeFalse();
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void BeginRoiDraw_WithRefFrame_StartsRoiOnActiveViewport()
    {
        var vm = CreateVm();
        try
        {
            vm.Roi.RoiRefWidth = 200;
            vm.Roi.RoiRefHeight = 100;
            vm.Editor.Roi = new Roi(0, 0, 1, 1);
            var testVp = new FakeRoiViewport();
            var roiVp = new FakeRoiViewport();
            using var coord = new RecipePageRoiCoordinator(vm, testVp, roiVp);
            coord.Wire();

            coord.BeginRoiDraw(template: false);

            roiVp.RoiModeStarted.Should().BeTrue();
            vm.ShowTestImage.Should().BeFalse();
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void Wire_IsIdempotent()
    {
        var vm = CreateVm();
        try
        {
            var testVp = new FakeRoiViewport();
            var roiVp = new FakeRoiViewport();
            using var coord = new RecipePageRoiCoordinator(vm, testVp, roiVp);
            coord.Wire();
            coord.Wire();
            coord.Unwire();
            coord.Unwire();
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void ResultImageChange_ResetsTemplateDrawTarget_WhenShowingTestImage()
    {
        var vm = CreateVm();
        try
        {
            vm.Roi.RoiRefWidth = 200;
            vm.Roi.RoiRefHeight = 100;
            vm.Editor.Roi = new Roi(0, 0, 1, 1);
            vm.Editor.Template = new TemplateOptions { Roi = new Roi(0.2, 0.2, 0.2, 0.2) };
            var testVp = new FakeRoiViewport();
            var roiVp = new FakeRoiViewport();
            using var coord = new RecipePageRoiCoordinator(vm, testVp, roiVp);
            coord.Wire();

            coord.BeginRoiDraw(template: true);
            roiVp.Rects.Should().Contain(r => r.Label == "特征");

            vm.ShowTestImageViewCommand.Execute(null);
            vm.Test.ResultImage = new System.Windows.Media.Imaging.WriteableBitmap(16, 16, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);

            testVp.Rects.Should().NotBeEmpty();
            testVp.Rects.Should().NotContain(r => r.Label == "特征");
        }
        finally { vm.Dispose(); }
    }
}
