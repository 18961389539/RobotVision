using FluentAssertions;
using RobotVision.Core.Recipe;
using RobotVision.WpfHost.Features.Recipe;
using RobotVision.WpfHost.Shared;
using Xunit;

namespace RobotVision.Wpf.Tests;

public sealed class RefineMethodDetailsViewModelTests
{
    [Fact]
    public void RestoreSnapshot_RevertsTemplateEdits()
    {
        using var dir = new TestInfra.TempDir("rv_refine_dlg");
        var folder = dir.CreateSub("recipes");
        File.WriteAllText(Path.Combine(folder, "R01.json"),
            """{"cameraId":"cam","angleMode":"MaskTemplate","models":["m.onnx"],"template":{"refineMethod":"Template","matchThreshold":0.6}}""");

        var cfg = TestInfra.CreateAppConfig(dir.Path);
        var cameras = new RobotVision.Infrastructure.Cameras.CameraManager();
        var loader = new RecipeLoader(folder);
        var models = new RobotVision.Infrastructure.Inference.ModelManager(dir.Path);
        var calibration = new RobotVision.Infrastructure.Calibration.CalibrationManager();
        var vision = TestInfra.CreateVisionService(folder, cameras);
        var lighting = new RobotVision.Infrastructure.Lighting.LightingManager();
        var registry = new RobotVision.Infrastructure.Inference.Strategies.AngleStrategyTypeRegistry();
        var tcp = TestInfra.CreateTcp();
        var assets = new RobotVision.Hosting.AssetIntegrityChecker(
            cfg, models, calibration, Microsoft.Extensions.Logging.Abstractions.NullLogger<RobotVision.Hosting.AssetIntegrityChecker>.Instance);

        var vm = new RecipeViewModel(
            loader, cfg, TestInfra.CameraFacade(cameras), TestInfra.ModelFacade(models),
            TestInfra.CalibrationFacade(calibration), vision, TestInfra.LightingFacade(lighting),
            TestInfra.AngleCatalog(registry), assets, new TestDialogService(), new NullRecipeWindowService(), TestLog.Null<RecipeViewModel>());
        try
        {
            vm.Editor.Template.MatchThreshold = 0.42;
            vm.Editor.Template.RefineRangeDeg = 12;
            var dialog = new RefineMethodDetailsViewModel(vm);
            vm.Editor.Template.MatchThreshold = 0.11;
            vm.Editor.Template.RefineRangeDeg = 3;

            dialog.RestoreSnapshot();

            vm.Editor.Template.MatchThreshold.Should().Be(0.42);
            vm.Editor.Template.RefineRangeDeg.Should().Be(12);
        }
        finally
        {
            vm.Dispose();
            tcp.Dispose();
        }
    }
}
