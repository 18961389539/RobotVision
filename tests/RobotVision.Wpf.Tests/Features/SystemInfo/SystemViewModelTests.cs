using FluentAssertions;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Inference;
using RobotVision.WpfHost.Features.SystemInfo;

namespace RobotVision.Wpf.Tests;

/// <summary>
/// 系统总览页测试：配置摘要 / 目录一览 / 相机资产徽章（含未注册标记）/ 实时状态串。
/// 在普通线程上直接构造即可（与 SettingsViewModelTests 同构）：ctor 内即时刷新，无 UI marshal。
/// </summary>
public class SystemViewModelTests : IDisposable
{
    private readonly TestInfra.TempDir _dir = new("rv_system");
    private readonly AppConfig _cfg;
    private readonly VisionService _vision;
    private readonly CameraManager _cameras;
    private readonly FailureImageStore _failures;

    public SystemViewModelTests()
    {
        _cfg = TestInfra.CreateAppConfig(_dir.Path);
        _vision = TestInfra.CreateVisionService(_cfg.RecipesFolder);
        _cameras = new CameraManager();
        _cameras.Register(new VirtualCamera("cam_a", 64, 64, "Bars"));
        _failures = new FailureImageStore(
            new FailureImageConfig { Folder = _cfg.FailureImage.Folder, RetainedCount = 200 },
            TestLog.Null<FailureImageStore>());
    }

    public void Dispose()
    {
        _cameras.Dispose();
        _dir.Dispose();
    }

    private SystemViewModel CreateVm(ITcpRuntime? tcp = null) =>
        new(
            _cfg,
            tcp ?? new FakeTcpRuntime(),
            _vision,
            TestInfra.CameraFacade(_cameras),
            new RecipeLoader(_cfg.RecipesFolder),
            _failures,
            TestInfra.ModelFacade(new ModelManager(_dir.Path)),
            new FakeInferenceRuntime(),
            TestLog.Null<SystemViewModel>());

    [Fact]
    public void Ctor_BuildsSettingsSummary_FromConfig()
    {
        var vm = CreateVm();

        vm.Settings.Should().Contain(r => r.Key == "TCP 监听" && r.Value.Contains("127.0.0.1:0"));
        vm.Settings.Should().Contain(r => r.Key == "最大排队深度");
    }

    [Fact]
    public void Ctor_BuildsDirectoryList_IncludingRecipeFolder()
    {
        var vm = CreateVm();

        vm.Directories.Should().Contain(r =>
            r.Key == "配方目录" && r.Value == _cfg.RecipesFolder);
        vm.Directories.Should().Contain(r => r.Key == "模型目录");
    }

    [Fact]
    public void Ctor_CameraBadges_FlagUnregisteredAssets()
    {
        _cfg.Cameras = [new CameraConfig { Id = "cam_a" }, new CameraConfig { Id = "cam_ghost" }];

        var vm = CreateVm();

        vm.CameraBadges.Should().HaveCount(2);
        vm.CameraBadges.Should().ContainSingle(b => b.Id == "cam_a" && b.Registered);
        vm.CameraBadges.Should().ContainSingle(b => b.Id == "cam_ghost" && !b.Registered);
        vm.RecipeStatus.Should().Contain("相机 2 台（注册 1 台）");
    }

    [Fact]
    public void TcpStatus_WhenStopped_ShowsStoppedExplicitly()
    {
        var vm = CreateVm(new FakeTcpRuntime { IsRunning = false });

        vm.TcpStatus.Should().Be("TCP 服务已停止");
    }

    [Fact]
    public void TcpStatus_WhenRunning_ShowsEndpointAndClientCount()
    {
        var vm = CreateVm(new FakeTcpRuntime
        {
            IsRunning = true,
            ListenEndPoint = "127.0.0.1:9999",
            ConnectedClients = 3,
        });

        vm.TcpStatus.Should().Be("监听 127.0.0.1:9999 · 在线客户端 3");
    }

    [Fact]
    public void LiveStatuses_InitializedOnCtor()
    {
        var vm = CreateVm();

        vm.HealthStatus.Should().Be("尚无请求记录");
        vm.QueueStatus.Should().Contain("队列");
        vm.RecipeStatus.Should().Contain("配方 0 个");
    }

    [Fact]
    public void StartStopAndDispose_DoNotThrow()
    {
        var vm = CreateVm();

        var act = () =>
        {
            vm.StartTimer();
            vm.StopTimer();
            vm.Dispose();
        };

        act.Should().NotThrow();
    }

    /// <summary>受控推理运行时：ActiveDevice 空 + GpuUnavailable false → 摘要显示"尚未加载模型"。</summary>
    private sealed class FakeInferenceRuntime : IInferenceRuntime
    {
        public string ActiveDevice { get; set; } = "";

        public bool GpuUnavailable { get; set; }

        public InferenceTask? DetectTask(string modelPath) => null;
    }
}
