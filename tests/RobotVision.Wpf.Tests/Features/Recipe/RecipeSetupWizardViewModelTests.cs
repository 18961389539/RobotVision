using System.Windows;
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
using RobotVision.Infrastructure.Lighting;
using RobotVision.Teach;
using RobotVision.WpfHost.Features.Monitor;
using RobotVision.WpfHost.Features.Recipe;

namespace RobotVision.Wpf.Tests;

public sealed class ShellViewModelTests
{
    private readonly VisionService _vision;
    private readonly FakeTcpRuntime _tcp = new();

    public ShellViewModelTests()
    {
        _vision = TestInfra.CreateVisionService(Path.Combine(Path.GetTempPath(), "rv_shell_" + Guid.NewGuid().ToString("N")));
    }

    [Fact]
    public void Ctor_ReflectsTcpAndQueueStatus()
    {
        TestInfra.RunSta(() =>
        {
            _tcp.IsRunning = true;
            _tcp.ConnectedClients = 3;
            using var shell = new ShellViewModel(_tcp, _vision);

            shell.IsTcpRunning.Should().BeTrue();
            shell.TcpStatus.Should().Contain("3 客户端");
            shell.TcpStatus.Should().Contain($"{_vision.QueueDepth}/{_vision.MaxQueueDepth}");
        });
    }

    [Fact]
    public void TimerRefresh_UpdatesStatusWhenClientsChange()
    {
        TestInfra.RunSta(() =>
        {
            _tcp.ConnectedClients = 1;
            using var shell = new ShellViewModel(_tcp, _vision);
            var initial = shell.TcpStatus;

            _tcp.ConnectedClients = 5;
            TestInfra.PumpDispatcherFor(TimeSpan.FromSeconds(1.2));

            shell.TcpStatus.Should().NotBe(initial);
            shell.TcpStatus.Should().Contain("5 客户端");
        });
    }

    [Fact]
    public void Dispose_StopsTimerRefresh()
    {
        TestInfra.RunSta(() =>
        {
            using var shell = new ShellViewModel(_tcp, _vision);
            var snapshot = shell.TcpStatus;
            shell.Dispose();

            _tcp.ConnectedClients = 99;
            TestInfra.PumpDispatcherFor(TimeSpan.FromSeconds(1.2));

            snapshot.Should().Contain("客户端");
        });
    }
}

public sealed class RecipeSetupWizardViewModelTests : IDisposable
{
    private readonly TestInfra.TempDir _dir = new("rv_setup_wizard");
    private readonly CameraManager _cameras = new();
    private readonly VisionService _vision;
    private readonly TcpServerManager _tcp;
    private readonly CalibrationManager _calibration = new();
    private readonly ModelManager _models;
    private readonly LightingManager _lighting = new();
    private readonly AngleStrategyTypeRegistry _angleRegistry = new();
    private readonly AssetIntegrityChecker _assets;
    private readonly RecipeLoader _loader;
    private readonly string _replayFolder;

    public RecipeSetupWizardViewModelTests()
    {
        var recipeFolder = _dir.CreateSub("recipes");
        _replayFolder = _dir.CreateSub("replay");
        File.WriteAllText(Path.Combine(recipeFolder, "R1.json"),
            """{"cameraId":"cam_file","angleMode":"MaskTemplate","models":["a.onnx"],"template":{"refineMethod":"Template"}}""");
        using (var img = new OpenCvSharp.Mat(32, 32, OpenCvSharp.MatType.CV_8UC3, OpenCvSharp.Scalar.All(90)))
            OpenCvSharp.Cv2.ImWrite(Path.Combine(_replayFolder, "f.bmp"), img);
        _cameras.Register(new FileCamera("cam_file", _replayFolder));
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

    private RecipeViewModel CreateHost()
    {
        var cfg = TestInfra.CreateAppConfig(_dir.Path);
        cfg.Cameras.Add(new CameraConfig { Id = "cam_file", Type = "File", Folder = _replayFolder });
        return new RecipeViewModel(
            _loader, cfg, TestInfra.CameraFacade(_cameras), TestInfra.ModelFacade(_models),
            TestInfra.CalibrationFacade(_calibration), TestInfra.LightingFacade(_lighting),
            TestInfra.AngleCatalog(_angleRegistry), _assets, new TestDialogService(), new NullRecipeWindowService(),
            TestInfra.CreateRecipeTestService(_vision, _cameras, _models, _calibration, _lighting),
            TestInfra.MaskTeach(), TestInfra.RefineGuidance(), TestInfra.Overlay(),
            TestLog.Null<RecipeViewModel>());
    }

    private RecipeSetupWizardViewModel CreateWizard(RecipeViewModel host, IRecipeSetupAnalysisService analysis) =>
        new(host, TestInfra.CameraFacade(_cameras), TestInfra.ModelFacade(_models),
            TestInfra.CalibrationFacade(_calibration), TestInfra.LightingFacade(_lighting),
            analysis, host.Roi, host.Test);

    private static RecipeSetupAnalysisResult SampleAnalysisResult() => new()
    {
        Scene = new SceneDescriptor(
            SceneKind.Silhouette, LightingClass.DarkField, 2.2, 0.75, 4.1, 0.18, false, 0, 120, "test scene"),
        BakeOff =
        [
            new SegmentRefineCandidate(SegmentRefineMethod.Template, true, true, 0.92, "winner"),
        ],
        Message = "analysis ok",
        Preview = new BgraImageBuffer(8, 8, 32, new byte[8 * 32]),
    };

    private static async Task RunOnUiAsync(Func<Task> action)
    {
        TestInfra.EnsureWpfApp();
        await Application.Current!.Dispatcher.InvokeAsync(action).Task.Unwrap();
    }

    private static void RunOnUi(Action action)
    {
        TestInfra.EnsureWpfApp();
        Application.Current!.Dispatcher.Invoke(action);
    }

    private static void PrepareHostForWizard(RecipeViewModel host)
    {
        if (string.IsNullOrWhiteSpace(host.Editor.CameraId))
            host.Editor.CameraId = "cam_file";
    }

    [Fact]
    public async Task AnalyzeAsync_AppliesResult_AndAdvancesToResultStep()
    {
        var host = CreateHost();
        var analysis = new RecordingRecipeSetupAnalysis { NextResult = SampleAnalysisResult() };
        try
        {
            await RunOnUiAsync(async () =>
            {
                using var wizard = CreateWizard(host, analysis);
                PrepareHostForWizard(host);
                wizard.ScoreAllPlayback = false;
                wizard.Step = SetupWizardStep.Analyze;
                wizard.AnalyzeCommand.CanExecute(null).Should().BeTrue();

                await wizard.AnalyzeCommand.ExecuteAsync(null);

                wizard.IsResult.Should().BeTrue();
                wizard.Preview.Should().NotBeNull();
                wizard.SceneSummary.Should().Contain("剪影");
                analysis.AnalyzeGrabCalls.Should().Be(1);
                analysis.LastUsedPlayback.Should().BeFalse();
            });
        }
        finally { host.Dispose(); }
    }

    [Fact]
    public void RecordingAnalysis_PlaybackPath_SetsPlaybackFlag()
    {
        var analysis = new RecordingRecipeSetupAnalysis();
        var request = new RecipeSetupAnalysisRequest(
            new RecipeConfig { CameraId = "cam_file", AngleMode = AngleMode.MaskTemplate },
            new TaskConstraints(),
            null,
            false,
            true,
            "cam_file");

        analysis.AnalyzePlayback(request);

        analysis.LastUsedPlayback.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_DisposeDuringRun_DoesNotApplyStaleResult()
    {
        var host = CreateHost();
        var analysis = new DelayedRecipeSetupAnalysis { DelayMs = 800 };
        try
        {
            await RunOnUiAsync(async () =>
            {
                var wizard = CreateWizard(host, analysis);
                PrepareHostForWizard(host);
                wizard.ScoreAllPlayback = false;
                wizard.Step = SetupWizardStep.Analyze;
                var analyzeTask = wizard.AnalyzeCommand.ExecuteAsync(null);
                await Task.Delay(50);
                wizard.Dispose();
                await analyzeTask;

                wizard.Preview.Should().BeNull();
                wizard.IsResult.Should().BeFalse();
            });
        }
        finally { host.Dispose(); }
    }

    [Fact]
    public async Task Dispose_ReleasesPreviewBuffer()
    {
        var host = CreateHost();
        var analysis = new RecordingRecipeSetupAnalysis { NextResult = SampleAnalysisResult() };
        try
        {
            await RunOnUiAsync(async () =>
            {
                var wizard = CreateWizard(host, analysis);
                PrepareHostForWizard(host);
                wizard.Step = SetupWizardStep.Analyze;
                await wizard.AnalyzeCommand.ExecuteAsync(null);
                wizard.Preview.Should().NotBeNull();

                wizard.Dispose();
                wizard.Preview.Should().BeNull();
            });
        }
        finally { host.Dispose(); }
    }

    [Fact]
    public async Task Apply_WritesRecommendationToEditor_AndCloses()
    {
        var host = CreateHost();
        var analysis = new RecordingRecipeSetupAnalysis { NextResult = SampleAnalysisResult() };
        try
        {
            await RunOnUiAsync(async () =>
            {
                using var wizard = CreateWizard(host, analysis);
                PrepareHostForWizard(host);
                wizard.Step = SetupWizardStep.Analyze;
                await wizard.AnalyzeCommand.ExecuteAsync(null);

                var closed = false;
                wizard.RequestClose += () => closed = true;
                wizard.ApplyCommand.Execute(null);

                closed.Should().BeTrue();
                wizard.Applied.Should().BeTrue();
                host.Message.Should().Contain("配置工作台");
            });
        }
        finally { host.Dispose(); }
    }

    [Fact]
    public void GoTo_Result_WithoutAnalysis_SetsGuardMessage()
    {
        var host = CreateHost();
        try
        {
            using var wizard = CreateWizard(host, new NoopRecipeSetupAnalysis());
            wizard.GoToCommand.Execute(SetupWizardStep.Result);

            wizard.IsResult.Should().BeFalse();
            wizard.Message.Should().Contain("分析");
        }
        finally { host.Dispose(); }
    }

    [Fact]
    public void DrawDetectionRoi_RaisesRequest_AndEnablesRoi()
    {
        var host = CreateHost();
        try
        {
            using var wizard = CreateWizard(host, new NoopRecipeSetupAnalysis());
            var requested = false;
            wizard.RequestBeginDetectionRoiDraw += () => requested = true;

            wizard.DrawDetectionRoiCommand.Execute(null);

            requested.Should().BeTrue();
            host.Roi.UseRoi.Should().BeTrue();
        }
        finally { host.Dispose(); }
    }
}
