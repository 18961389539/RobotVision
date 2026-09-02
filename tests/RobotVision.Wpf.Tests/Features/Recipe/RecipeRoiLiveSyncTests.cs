using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;
using FluentAssertions;
using ImageViewer.Models;
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

public sealed class RecipeRoiLiveSyncTests : IDisposable
{
    private readonly TestInfra.TempDir _dir = new("rv_roi_sync");
    private readonly CameraManager _cameras = new();
    private readonly VisionService _vision;
    private readonly TcpServerManager _tcp;
    private readonly CalibrationManager _calibration = new();
    private readonly ModelManager _models;
    private readonly LightingManager _lighting = new();
    private readonly AngleStrategyTypeRegistry _angleRegistry = new();
    private readonly AssetIntegrityChecker _assets;
    private readonly RecipeLoader _loader;

    public RecipeRoiLiveSyncTests()
    {
        var recipeFolder = _dir.CreateSub("recipes");
        File.WriteAllText(Path.Combine(recipeFolder, "R1.json"),
            """{"cameraId":"cam_v","angleMode":"KeyPointLine","models":["a.onnx"]}""");
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

    private static (RecipeRoiLiveSync sync, FakeRoiViewport active, FakeRoiViewport other, RecipeViewModel vm) CreateSync(
        RecipeViewModel vm,
        bool usesFeatureTeach = false,
        bool showTemplate = false)
    {
        var active = new FakeRoiViewport();
        var other = new FakeRoiViewport();
        var sync = new RecipeRoiLiveSync(
            vm.Roi,
            () => active,
            [active, other],
            () => vm.Editor.Roi,
            () => vm.Editor.Template?.Roi,
            () => usesFeatureTeach,
            () => showTemplate);
        return (sync, active, other, vm);
    }

    private static void EstablishRefFrame(RecipeViewModel vm)
    {
        vm.Roi.RoiRefWidth = 200;
        vm.Roi.RoiRefHeight = 100;
        vm.Editor.Roi = new Roi(0.1, 0.1, 0.4, 0.5);
    }

    [Fact]
    public void Wire_IsIdempotent()
    {
        var vm = CreateVm();
        try
        {
            var (sync, _, _, _) = CreateSync(vm);
            sync.Wire();
            sync.Wire();
            sync.Unwire();
            sync.Unwire();
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void SyncFromRecipe_AddsDetectionRect_WithPixelGeometry()
    {
        var vm = CreateVm();
        try
        {
            EstablishRefFrame(vm);
            var (sync, active, _, _) = CreateSync(vm);
            sync.Wire();
            sync.SyncFromRecipe();

            active.Rects.Should().ContainSingle();
            var rect = active.Rects[0];
            rect.Label.Should().Be("检测");
            rect.Center.X.Should().BeApproximately(60, 0.01);
            rect.Center.Y.Should().BeApproximately(35, 0.01);
            rect.Width.Should().BeApproximately(80, 0.01);
            rect.Height.Should().BeApproximately(50, 0.01);
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void DrawnRect_UpdatesEditorRoi()
    {
        var vm = CreateVm();
        try
        {
            EstablishRefFrame(vm);
            var (sync, active, _, _) = CreateSync(vm);
            sync.Wire();

            var drawn = new RotatedRect
            {
                Center = new Point(60, 40),
                Width = 40,
                Height = 20,
            };
            active.Rects.Add(drawn);

            vm.Editor.Roi.Should().NotBeNull();
            vm.Editor.Roi!.Width.Should().BeApproximately(0.2, 0.01);
            vm.Editor.Roi.Height.Should().BeApproximately(0.2, 0.01);
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void TemplateDrawTarget_WritesTemplateRoi()
    {
        var vm = CreateVm();
        try
        {
            EstablishRefFrame(vm);
            vm.Editor.Template = new TemplateOptions { Roi = new Roi(0.2, 0.2, 0.2, 0.2) };
            var (sync, active, _, _) = CreateSync(vm, usesFeatureTeach: true, showTemplate: true);
            sync.IsTemplateDrawTarget = true;
            sync.Wire();

            active.Rects.Add(new RotatedRect
            {
                Center = new Point(50, 50),
                Width = 30,
                Height = 30,
            });

            vm.Editor.Template!.Roi.Should().NotBeNull();
            vm.Editor.Template.Roi!.Width.Should().BeApproximately(0.15, 0.02);
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void OnPreviewImageReady_StartsDeferredDetectionDraw()
    {
        TestInfra.RunSta(() =>
        {
            var vm = CreateVm();
            try
            {
                EstablishRefFrame(vm);
                var (sync, active, _, _) = CreateSync(vm);
                sync.StartDrawAfterGrab = true;
                sync.Wire();
                vm.Roi.PreviewImage = new System.Windows.Media.Imaging.WriteableBitmap(8, 8, 96, 96, PixelFormats.Bgra32, null);

                active.RoiModeStarted.Should().BeTrue();
                sync.StartDrawAfterGrab.Should().BeFalse();
            }
            finally { vm.Dispose(); }
        });
    }

    [Fact]
    public void UseRoiFalse_ClearsDetectionRect()
    {
        var vm = CreateVm();
        try
        {
            EstablishRefFrame(vm);
            var (sync, active, _, _) = CreateSync(vm);
            sync.Wire();
            sync.SyncFromRecipe();
            active.Rects.Should().NotBeEmpty();

            vm.Roi.UseRoi = false;

            active.Rects.Should().BeEmpty();
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void LiveRectAngle_IsForcedToZero()
    {
        var vm = CreateVm();
        try
        {
            EstablishRefFrame(vm);
            var (sync, active, _, _) = CreateSync(vm);
            sync.Wire();
            sync.SyncFromRecipe();

            active.Rects[0].Angle = 15;
            active.Rects[0].Angle.Should().Be(0);
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void Unwire_ClearsAllRects()
    {
        var vm = CreateVm();
        try
        {
            EstablishRefFrame(vm);
            var (sync, active, other, _) = CreateSync(vm);
            sync.Wire();
            sync.SyncFromRecipe();
            active.Rects.Should().NotBeEmpty();

            sync.Unwire();

            active.Rects.Should().BeEmpty();
            other.Rects.Should().BeEmpty();
        }
        finally { vm.Dispose(); }
    }
}
