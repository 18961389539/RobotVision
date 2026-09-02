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

public sealed class RecipeWizardImageHostTests : IDisposable
{
    private readonly TestInfra.TempDir _dir = new("rv_wizard_host");
    private readonly CameraManager _cameras = new();
    private readonly VisionService _vision;
    private readonly TcpServerManager _tcp;
    private readonly CalibrationManager _calibration = new();
    private readonly ModelManager _models;
    private readonly LightingManager _lighting = new();
    private readonly AngleStrategyTypeRegistry _angleRegistry = new();
    private readonly AssetIntegrityChecker _assets;
    private readonly RecipeLoader _loader;

    public RecipeWizardImageHostTests()
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
    public void BeginDetectionRoiDraw_WithRefFrame_StartsRoiMode()
    {
        var vm = CreateVm();
        try
        {
            vm.Roi.RoiRefWidth = 200;
            vm.Roi.RoiRefHeight = 100;
            vm.Editor.Roi = new Roi(0, 0, 1, 1);
            var viewport = new FakeRoiViewport();
            using var host = new RecipeWizardImageHost(viewport, vm, vm.Roi, () => vm.UsesFeatureTeachRoi);
            host.Wire();

            host.BeginDetectionRoiDraw();

            viewport.RoiModeStarted.Should().BeTrue();
            viewport.Rects.Should().NotBeEmpty();
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void BeginDetectionRoiDraw_WithoutRefFrame_DefersUntilPreview()
    {
        var vm = CreateVm();
        try
        {
            var viewport = new FakeRoiViewport();
            using var host = new RecipeWizardImageHost(viewport, vm, vm.Roi, () => vm.UsesFeatureTeachRoi);
            host.Wire();

            host.BeginDetectionRoiDraw();

            viewport.RoiModeStarted.Should().BeFalse();
            vm.Roi.PreviewImage = new System.Windows.Media.Imaging.WriteableBitmap(8, 8, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
            viewport.RoiModeStarted.Should().BeTrue();
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void BeginFeatureRoiDraw_WhenNotSupported_IsNoOp()
    {
        var vm = CreateVm();
        try
        {
            vm.Editor.AngleMode = AngleMode.KeyPointLine;
            var viewport = new FakeRoiViewport();
            using var host = new RecipeWizardImageHost(viewport, vm, vm.Roi, () => vm.UsesFeatureTeachRoi);
            host.Wire();

            host.BeginFeatureRoiDraw();

            viewport.RoiModeStarted.Should().BeFalse();
            viewport.Rects.Should().BeEmpty();
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void BeginFeatureRoiDraw_WithRefFrame_StartsTemplateMode()
    {
        var vm = CreateVm();
        try
        {
            vm.Roi.RoiRefWidth = 200;
            vm.Roi.RoiRefHeight = 100;
            vm.Editor.Template = new TemplateOptions { Roi = new Roi(0.2, 0.2, 0.2, 0.2) };
            var viewport = new FakeRoiViewport();
            using var host = new RecipeWizardImageHost(viewport, vm, vm.Roi, () => vm.UsesFeatureTeachRoi);
            host.Wire();

            host.BeginFeatureRoiDraw();

            viewport.RoiModeStarted.Should().BeTrue();
            viewport.Rects.Should().Contain(r => r.Label == "特征");
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void Dispose_ClearsSyncedRects()
    {
        var vm = CreateVm();
        try
        {
            vm.Roi.RoiRefWidth = 100;
            vm.Roi.RoiRefHeight = 100;
            vm.Editor.Roi = new Roi(0, 0, 0.5, 0.5);
            var viewport = new FakeRoiViewport();
            var host = new RecipeWizardImageHost(viewport, vm, vm.Roi, () => vm.UsesFeatureTeachRoi);
            host.Wire();
            host.SyncFromRecipe();
            viewport.Rects.Should().NotBeEmpty();

            host.Dispose();

            viewport.Rects.Should().BeEmpty();
        }
        finally { vm.Dispose(); }
    }
}
