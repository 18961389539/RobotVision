using FluentAssertions;
using Microsoft.Extensions.Logging;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Communication;
using RobotVision.WpfHost.Features.Monitor;

namespace RobotVision.Wpf.Tests;

/// <summary>
/// 主监控页测试：相机/配方下拉初始化、默认相机选择策略、面板折叠、
/// 日志汇聚与级别过滤、手动触发失败路径（配方不存在 → 1001 横幅）、日志清空。
/// </summary>
public class MainViewModelTests : IDisposable
{
    private readonly TestInfra.TempDir _dir = new("rv_main");
    private readonly AppConfig _cfg;
    private readonly CameraManager _cameras = new();
    private readonly RecipeLoader _recipes;
    private readonly VisionService _vision;
    private readonly TcpServerManager _tcp;
    private readonly LogSink _sink;

    public MainViewModelTests()
    {
        var recipeFolder = _dir.CreateSub("recipes");
        File.WriteAllText(System.IO.Path.Combine(recipeFolder, "A01.json"),
            """{"cameraId": "cam_file", "angleMode": "KeyPointLine", "models": ["no_such.onnx"], "keypointIndexA": 0, "keypointIndexB": 1}""");

        var replay = _dir.CreateSub("replay");
        using (var img = new OpenCvSharp.Mat(64, 64, OpenCvSharp.MatType.CV_8UC3, OpenCvSharp.Scalar.All(80)))
            OpenCvSharp.Cv2.ImWrite(System.IO.Path.Combine(replay, "f.bmp"), img);

        _cameras.Register(new VirtualCamera("cam_virtual", 64, 64, "Bars"));
        _cameras.Register(new FileCamera("cam_file", replay));

        _cfg = TestInfra.CreateAppConfig(_dir.Path);
        _cfg.Cameras =
        [
            new CameraConfig { Id = "cam_virtual", Type = "Virtual" },
            new CameraConfig { Id = "cam_file", Type = "File", Folder = replay },
        ];

        _recipes = new RecipeLoader(recipeFolder);
        _vision = TestInfra.CreateVisionService(recipeFolder, _cameras);
        _tcp = TestInfra.CreateTcp();
        _sink = new LogSink();
    }

    public void Dispose()
    {
        _cameras.Dispose();
        _sink.Dispose();
        _tcp.Dispose();
        _dir.Dispose();
    }

    private MainViewModel CreateVm() =>
        new(_vision, _cfg, TestInfra.CameraFacade(_cameras),
            TestInfra.CalibrationFacade(new RobotVision.Infrastructure.Calibration.CalibrationManager()),
            _recipes, TestInfra.TcpFacade(_tcp), _sink, TestLog.Null<MainViewModel>());

    [Fact]
    public void Ctor_LoadsCamerasAndRecipes_SelectsVirtualCameraByDefault()
    {
        var vm = CreateVm();
        try
        {
            vm.CameraOptions.Select(o => o.Id).Should().Equal("cam_virtual", "cam_file");
            // 默认相机优先 Virtual（无硬件时不默认 Basler）
            vm.SelectedCamera.Should().Be("cam_virtual");
            vm.Recipes.Should().Equal("A01");
            vm.SelectedRecipe.Should().Be("A01");
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void RefreshRecipes_KeepsSelection_WhenStillExists()
    {
        var vm = CreateVm();
        try
        {
            vm.SelectedRecipe.Should().Be("A01");
            vm.RefreshRecipesCommand.Execute(null);
            vm.SelectedRecipe.Should().Be("A01");
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void ToggleCommands_FlipPanelVisibility()
    {
        var vm = CreateVm();
        try
        {
            var param = vm.IsParamPanelVisible;
            vm.ToggleParamPanelCommand.Execute(null);
            vm.IsParamPanelVisible.Should().Be(!param);

            var log = vm.IsLogPanelVisible;
            vm.ToggleLogPanelCommand.Execute(null);
            vm.IsLogPanelVisible.Should().Be(!log);
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void LogSink_Entries_AppearInLogs()
    {
        var vm = CreateVm();
        try
        {
            var logger = _sink.CreateLogger("Test.Category");
            MainViewModelTestsLog.HelloInfo(logger);
            MainViewModelTestsLog.HelloWarn(logger);

            vm.Logs.Should().HaveCount(2);
            vm.Logs[0].Message.Should().Contain("hello info");
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void LogFilter_OnlyErrors_FiltersDisplayedLogs()
    {
        var vm = CreateVm();
        try
        {
            var logger = _sink.CreateLogger("Test.Category");
            MainViewModelTestsLog.InfoLine(logger);
            MainViewModelTestsLog.ErrorLine(logger);

            vm.Logs.Should().HaveCount(2);

            vm.LogFilter = "仅错误";
            vm.Logs.Should().ContainSingle();
            vm.Logs[0].Message.Should().Contain("error line");

            vm.LogFilter = "全部";
            vm.Logs.Should().HaveCount(2);
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void LogCapacity_IsBounded()
    {
        var vm = CreateVm();
        try
        {
            var logger = _sink.CreateLogger("Test.Category");
            for (var i = 0; i < 600; i++)
                MainViewModelTestsLog.LineIndex(logger, i);

            vm.Logs.Count.Should().BeLessThanOrEqualTo(500); // LogCapacity = 500
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void ClearLog_EmptiesBothBuffers()
    {
        var vm = CreateVm();
        try
        {
            MainViewModelTestsLog.SimpleX(_sink.CreateLogger("T"));
            vm.Logs.Should().NotBeEmpty();

            vm.ClearLogCommand.Execute(null);

            vm.Logs.Should().BeEmpty();
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public async Task Trigger_UnknownRecipe_Shows1001Banner()
    {
        var vm = CreateVm();
        try
        {
            vm.SelectedRecipe = "NOPE";

            await vm.TriggerCommand.ExecuteAsync(null);

            vm.StatusText.Should().Contain("配方不存在");
            vm.TriggerBannerText.Should().Contain("配方不存在");
            vm.TriggerSuccess.Should().BeFalse();
            vm.IsBusy.Should().BeFalse();
            vm.CanTrigger.Should().BeTrue();
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public async Task Trigger_WhileBusy_IsRejected()
    {
        var vm = CreateVm();
        try
        {
            // 用一个会阻塞的请求占住管线：配方指向不存在模型（1005）但相机取图是瞬时路径；
            // 并发槽位 = 1 时第二个请求排队 → 直接测连点保护：先手动置 IsBusy
            vm.IsBusy = true;
            vm.CanTrigger.Should().BeFalse();

            await vm.TriggerCommand.ExecuteAsync(null); // IsBusy 时直接 return

            vm.StatusText.Should().NotContain("ERR"); // 未被触发
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void LogLine_ClipboardText_JoinsTimeLevelMessage()
    {
        var line = new LogLine("14:11:03", "Error", "相机 cam_basler 采集失败");
        line.ClipboardText.Should().Be("14:11:03 Error 相机 cam_basler 采集失败");
    }
}
