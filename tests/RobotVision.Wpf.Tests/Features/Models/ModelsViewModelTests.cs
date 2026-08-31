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
    private readonly YoloDotNetEngineFactory _engineFactory = new();

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
        new(TestInfra.ModelFacade(_models), _engineFactory, new TestDialogService(), TestLog.Null<ModelsViewModel>());

    [Fact]
    public void Ctor_ListsOnnxFilesInFolder()
    {
        var vm = CreateVm();

        vm.Files.Should().ContainSingle();
        vm.Files[0].Name.Should().Be("demo.onnx");
        vm.Files[0].LoadedText.Should().Be("未加载");
        vm.Message.Should().Contain("1 个模型文件");
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
    public void Refresh_PreservesSelection_WhenFileStillExists()
    {
        var vm = CreateVm();
        vm.SelectedFile = vm.Files[0];

        vm.RefreshCommand.Execute(null);

        vm.SelectedFile?.Name.Should().Be("demo.onnx");
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
