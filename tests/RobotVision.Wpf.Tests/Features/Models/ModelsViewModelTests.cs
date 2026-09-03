using FluentAssertions;
using RobotVision.Core;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Inference;
using RobotVision.WpfHost.Features.Models;
using RobotVision.WpfHost.Shared;

namespace RobotVision.Wpf.Tests;

public sealed class ModelsViewModelTests : IDisposable
{
    private readonly TestInfra.TempDir _dir = new("rv_models");
    private readonly ModelManager _models;

    public ModelsViewModelTests()
    {
        var modelsFolder = _dir.CreateSub("models");
        File.WriteAllText(Path.Combine(modelsFolder, "demo.onnx"), "not-a-real-onnx");
        _models = new ModelManager(modelsFolder);
    }

    public void Dispose()
    {
        _models.Dispose();
        _dir.Dispose();
    }

    private ModelsViewModel CreateVm() =>
        new(TestInfra.ModelFacade(_models), new YoloDotNetEngineFactory().AsInferenceRuntime(),
            TestInfra.ModelTest(_models), TestInfra.ImageFiles(), new TestDialogService(),
            TestLog.Null<ModelsViewModel>());

    [Fact]
    public async Task Ctor_LoadsPrefs_AndRefreshListsOnnxFiles()
    {
        var vm = CreateVm();
        await vm.RefreshAsync();

        vm.Files.Should().ContainSingle();
        vm.Files[0].Name.Should().Be("demo.onnx");
        vm.Files[0].LoadedText.Should().Be("未加载");
        vm.Message.Should().Contain("1 个模型文件");
    }

    [Fact]
    public void SavePrefs_PersistsTaskAndThresholds()
    {
        var prefsPath = Path.Combine(AppContext.BaseDirectory, "model-test.prefs.json");
        var backup = File.Exists(prefsPath) ? File.ReadAllText(prefsPath) : null;
        try
        {
            var vm = CreateVm();
            vm.SelectedTask = InferenceTask.Segmentation;
            vm.Confidence = 0.42;
            vm.PixelConfidence = 0.55;
            vm.Iou = 0.61;
            vm.FlushPrefsForTests();

            var json = File.ReadAllText(prefsPath);
            json.Should().Contain("\"Task\":1");
            json.Should().Contain("0.42");
        }
        finally
        {
            if (backup is null)
                File.Delete(prefsPath);
            else
                File.WriteAllText(prefsPath, backup);
        }
    }

    /// <summary>
    /// 回归：切换到「模型管理」页报错的根因是构造函数在 _prefsSaveTimer 初始化前调用
    /// LoadPrefs；当 model-test.prefs.json 存在时，LoadPrefs 设置 SelectedTask 会经
    /// ScheduleSavePrefs 触碰到 null 定时器抛 NRE，导致整页 DI 解析失败。
    /// </summary>
    [Fact]
    public void Ctor_WithExistingPrefsFile_DoesNotThrow_AndRestoresTask()
    {
        var prefsPath = Path.Combine(AppContext.BaseDirectory, "model-test.prefs.json");
        var backup = File.Exists(prefsPath) ? File.ReadAllText(prefsPath) : null;
        try
        {
            File.WriteAllText(
                prefsPath,
                "{\"Model\":null,\"TestImageFolder\":\"data/replay\",\"Task\":1," +
                "\"Confidence\":0.5,\"PixelConfidence\":0.65,\"Iou\":0.7}");

            var vm = CreateVm();

            vm.SelectedTask.Should().Be(InferenceTask.Segmentation);
        }
        finally
        {
            if (backup is null)
                File.Delete(prefsPath);
            else
                File.WriteAllText(prefsPath, backup);
        }
    }

    [Fact]
    public void TaskOptions_ContainDetectionSegmentationPose()
    {
        var vm = CreateVm();

        vm.TaskOptions.Select(o => o.Value).Should().Contain(InferenceTask.ObjectDetection);
        vm.TaskOptions.Select(o => o.Value).Should().Contain(InferenceTask.Segmentation);
        vm.TaskOptions.Select(o => o.Value).Should().Contain(InferenceTask.PoseEstimation);
        vm.TaskOptions.Should().OnlyContain(o => o.Label.Length > 0);
    }

    [Fact]
    public async Task Refresh_PreservesSelection_WhenFileStillExists()
    {
        var vm = CreateVm();
        await vm.RefreshAsync();
        vm.SelectedFile = vm.Files[0];

        await vm.RefreshAsync();

        vm.SelectedFile?.Name.Should().Be("demo.onnx");
    }

    [Fact]
    public void ThresholdSetters_RejectNonFinite_KeepPreviousValue()
    {
        var vm = CreateVm();
        vm.Confidence = 0.42;
        vm.PixelConfidence = 0.55;
        vm.Iou = 0.61;

        vm.Confidence = double.NaN;
        vm.PixelConfidence = double.PositiveInfinity;
        vm.Iou = double.NegativeInfinity;

        vm.Confidence.Should().Be(0.42);
        vm.PixelConfidence.Should().Be(0.55);
        vm.Iou.Should().Be(0.61);
    }

    [Fact]
    public void ThresholdSetters_AcceptFiniteRange_AndRaisePropertyChanged()
    {
        var vm = CreateVm();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.Confidence = 0.9;

        vm.Confidence.Should().Be(0.9);
        changed.Should().Contain(nameof(vm.Confidence));
    }

    [Fact]
    public void Unload_WithoutSelection_ShowsHint()
    {
        var vm = CreateVm();
        vm.SelectedFile = null;

        vm.UnloadCommand.Execute(null);

        vm.Message.Should().Contain("请先选择");
    }
}
