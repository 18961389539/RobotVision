using System.Globalization;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Models;
using RobotVision.Hosting;
using RobotVision.Infrastructure;
using RobotVision.Infrastructure.Inference;
using RobotVision.WpfHost.Shared;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RobotVision.WpfHost.Features.Models;

public sealed record ModelFileItem(
    string Name, string SizeText, string ModifiedText, string LoadedText, bool Loaded);

/// <summary>推理任务下拉项（中文显示，Value 保持枚举便于绑定 SelectedTask）。</summary>
public sealed record TaskOption(InferenceTask Value, string Label);

/// <summary>
/// 模型管理：目录浏览 + 缓存会话状态 + 单模型测试推理。
/// 测试推理不经过配方/相机/标定链路：选模型 + 选图片直接跑，用于验证模型本身
/// （框/掩码/关键点）。测试走 <see cref="ModelManager"/> 会话（与产线同锁、同 GPU 会话），
/// 避免另开引擎抢核显；探测任务类型仍用工厂短加载。
/// 测试参数（模型/任务/阈值/图片目录）持久化到 exe 旁 model-test.prefs.json。
/// </summary>
public partial class ModelsViewModel : ObservableObject, ICommitPendingEdits, IDisposable
{
    /// <summary>测试参数持久化文件（exe 旁，随部署目录走）。</summary>
    private static readonly string PrefsPath =
        Path.Combine(AppContext.BaseDirectory, "model-test.prefs.json");

    private sealed record TestPrefs(
        string? Model, string? TestImageFolder, InferenceTask Task,
        double Confidence, double PixelConfidence, double Iou,
        string? TestImagePath = null);

    private static readonly string[] TestImageExtensions =
        [".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff"];

    private sealed record FolderTestResult(
        string FileName, ImageSource? Image, int Count, int Width, int Height, string? Error);

    private List<FolderTestResult> _folderResults = [];

    public Action? FlushPendingEdits { get; set; }

    private readonly IModelRuntime _models;
    private readonly IInferenceEngineFactory _engineFactory;
    private readonly IDialogService _dialogs;
    private readonly ILogger<ModelsViewModel> _log;

    public ObservableCollection<ModelFileItem> Files { get; } = [];

    /// <summary>推理任务下拉（中文：检测/分割/关键点），SelectedValue 绑定枚举。</summary>
    public IReadOnlyList<TaskOption> TaskOptions { get; } =
        Enum.GetValues<InferenceTask>().Select(t => new TaskOption(t, TaskLabel(t))).ToArray();

    [ObservableProperty]
    private string _message = "";

    [ObservableProperty]
    private ModelFileItem? _selectedFile;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDetectionTask))]
    [NotifyPropertyChangedFor(nameof(IsSegmentationTask))]
    [NotifyPropertyChangedFor(nameof(IsPoseTask))]
    [NotifyPropertyChangedFor(nameof(ShowPixelConfidence))]
    [NotifyPropertyChangedFor(nameof(TaskParamsHint))]
    private InferenceTask _selectedTask = InferenceTask.ObjectDetection;

    [ObservableProperty]
    private double _confidence = 0.5;

    /// <summary>分割掩码的像素置信度阈值（仅分割任务使用）。</summary>
    [ObservableProperty]
    private double _pixelConfidence = 0.65;

    [ObservableProperty]
    private double _iou = 0.7;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TestImageFileCount))]
    [NotifyPropertyChangedFor(nameof(TestImageFolderHint))]
    private string _testImageFolder = "data/replay";

    /// <summary>当前测试目录内可推理的图片数量。</summary>
    public int TestImageFileCount => ListTestImages(ResolveTestFolder()).Count;

    public string TestImageFolderHint => TestImageFileCount > 0
        ? $"目录内 {TestImageFileCount} 张图片（bmp/jpg/png/tif）"
        : "目录为空或不存在";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanShowPrevFolderResult))]
    [NotifyPropertyChangedFor(nameof(CanShowNextFolderResult))]
    [NotifyPropertyChangedFor(nameof(FolderResultPosition))]
    private int _folderResultIndex;

    public bool CanShowPrevFolderResult => _folderResults.Count > 0 && FolderResultIndex > 0;

    public bool CanShowNextFolderResult =>
        _folderResults.Count > 0 && FolderResultIndex < _folderResults.Count - 1;

    public string FolderResultPosition => _folderResults.Count == 0
        ? ""
        : $"{FolderResultIndex + 1} / {_folderResults.Count}";

    [ObservableProperty]
    private ImageSource? _resultImage;

    [ObservableProperty]
    private string _testResult = "选择模型与图片后开始测试";

    /// <summary>是否正在测试推理（控制「取消」按钮可见性）。</summary>
    [ObservableProperty]
    private bool _isTesting;

    /// <summary>测试期间禁用「开始推理」，避免重复启动（推理本身不可中断）。</summary>
    [ObservableProperty]
    private bool _canTest = true;

    partial void OnIsTestingChanged(bool value) => CanTest = !value;

    public bool IsDetectionTask => SelectedTask == InferenceTask.ObjectDetection;
    public bool IsSegmentationTask => SelectedTask == InferenceTask.Segmentation;
    public bool IsPoseTask => SelectedTask == InferenceTask.PoseEstimation;

    public bool ShowPixelConfidence => IsSegmentationTask;

    public string TaskParamsHint => SelectedTask switch
    {
        InferenceTask.ObjectDetection => "检测任务：置信度过滤框；IoU 用于同类框 NMS。",
        InferenceTask.Segmentation => "分割任务：置信度过滤实例；掩码像素置信度过滤 mask；IoU 用于实例 NMS。",
        InferenceTask.PoseEstimation => "关键点任务：置信度过滤实例与关键点；IoU 用于实例 NMS。",
        _ => "",
    };

    /// <summary>模型列表浮动面板是否可见（与右侧参数栏同构，图像主导布局）。</summary>
    [ObservableProperty]
    private bool _isListPanelVisible = true;

    [RelayCommand]
    private void ToggleListPanel() => IsListPanelVisible = !IsListPanelVisible;

    /// <summary>参数浮动面板是否可见（方案 A：图像主导，面板可收起，收起后点右上角"参数"重新打开）。</summary>
    [ObservableProperty]
    private bool _isParamPanelVisible = true;

    /// <summary>收起/展开参数浮动面板。</summary>
    [RelayCommand]
    private void ToggleParamPanel() => IsParamPanelVisible = !IsParamPanelVisible;

    private CancellationTokenSource? _testCts;
    private CancellationTokenSource? _taskDetectCts;

    /// <summary>模型任务探测缓存（路径 → 文件版本 + 任务），避免重复加载 ONNX。</summary>
    private readonly Dictionary<string, TaskDetectCache> _taskDetectCache = new(StringComparer.OrdinalIgnoreCase);

    private sealed record TaskDetectCache(DateTime StampUtc, long Size, InferenceTask? Task);

    partial void OnSelectedFileChanged(ModelFileItem? value)
    {
        if (value is null)
            return;
        UiFireAndForget.Run(ApplyAutoTaskForSelectedModelAsync, _log);
    }

    /// <summary>选中模型后自动匹配推理任务（已缓存会话 → 文件名启发式 → ONNX 元数据）。</summary>
    private async Task ApplyAutoTaskForSelectedModelAsync()
    {
        if (SelectedFile is null)
            return;

        _taskDetectCts?.Cancel();
        _taskDetectCts?.Dispose();
        var cts = _taskDetectCts = new CancellationTokenSource();
        var file = SelectedFile;
        var path = _models.ResolvePath(file.Name);

        // 1) 产线已缓存且仅一种任务：直接采用
        var loadedTasks = _models.LoadedKeys
            .Where(k => string.Equals(k.Path, path, StringComparison.OrdinalIgnoreCase))
            .Select(k => k.Task)
            .Distinct()
            .ToList();
        if (loadedTasks.Count == 1)
        {
            SelectedTask = loadedTasks[0];
            return;
        }

        // 2) 文件名启发式（即时）
        var guess = InferenceTaskDetector.GuessFromFileName(file.Name);
        if (guess is InferenceTask guessed)
            SelectedTask = guessed;

        if (!File.Exists(path))
            return;

        var info = new FileInfo(path);
        if (_taskDetectCache.TryGetValue(path, out var cached)
            && cached.StampUtc == info.LastWriteTimeUtc
            && cached.Size == info.Length)
        {
            if (cached.Task is InferenceTask cachedTask)
                SelectedTask = cachedTask;
            return;
        }

        try
        {
            if (guess is null)
                Message = $"正在识别 {file.Name} 的模型类型...";

            var detected = await Task.Run(() =>
            {
                cts.Token.ThrowIfCancellationRequested();
                return _engineFactory.DetectTask(path);
            }, cts.Token);

            if (cts.IsCancellationRequested || !ReferenceEquals(SelectedFile, file))
                return;

            _taskDetectCache[path] = new TaskDetectCache(info.LastWriteTimeUtc, info.Length, detected);
            if (detected is InferenceTask task)
            {
                SelectedTask = task;
                if (guess is null || guess != task)
                    Message = $"已自动选择「{TaskLabel(task)}」任务（{file.Name}）";
            }
            else if (guess is null)
                Message = $"{file.Name}：未能识别任务类型，请手动选择推理任务";
        }
        catch (OperationCanceledException)
        {
            // 用户快速切换模型：丢弃本次探测
        }
        catch (Exception ex) when (ex is VisionException or IOException)
        {
            if (ReferenceEquals(SelectedFile, file) && guess is null)
                Message = $"识别模型类型失败: {ex.Message}";
        }
    }

    public ModelsViewModel(
        IModelRuntime models,
        IInferenceEngineFactory engineFactory,
        IDialogService dialogs,
        ILogger<ModelsViewModel> log)
    {
        _models = models;
        _engineFactory = engineFactory;
        _dialogs = dialogs;
        _log = log;
        LoadPrefs();
        Refresh();
        // 恢复上次选择的模型（不存在时保持 Refresh 选中的第一个）
        var last = _lastModel;
        if (!string.IsNullOrEmpty(last))
            SelectedFile = Files.FirstOrDefault(f => string.Equals(f.Name, last, StringComparison.OrdinalIgnoreCase))
                           ?? SelectedFile;
    }

    private string? _lastModel;

    [RelayCommand]
    public void Refresh()
    {
        // Clear 会使 ListBox 绑定把 SelectedFile 置空，须先记下名称再恢复选中项
        var selectedName = SelectedFile?.Name;

        Files.Clear();
        var groups = _models.LoadedKeys
            .GroupBy(k => k.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var loaded = groups.ToDictionary(
            g => g.Key,
            g => string.Join("、", g.Select(k => TaskLabel(k.Task))),
            StringComparer.OrdinalIgnoreCase);

        var folder = _models.ModelsFolder;
        if (Directory.Exists(folder))
        {
            foreach (var file in new DirectoryInfo(folder)
                         .EnumerateFiles("*.onnx")
                         .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
            {
                var loadedText = loaded.TryGetValue(file.FullName, out var tasks) ? tasks : "未加载";
                Files.Add(new ModelFileItem(
                    file.Name,
                    $"{file.Length / 1024.0 / 1024.0:0.0} MB",
                    file.LastWriteTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                    loadedText,
                    loaded.ContainsKey(file.FullName)));
            }
        }

        SelectedFile = ListSelection.Restore(Files, selectedName, f => f.Name);

        // 同一文件被多个任务打开时各占一份会话内存（缓存键含任务），提示用户代价
        var multiTask = groups.Count(g => g.Count() > 1);
        Message = Directory.Exists(folder)
            ? $"{Files.Count} 个模型文件 · 已缓存会话 {_models.LoadedCount} 个"
              + (multiTask > 0 ? $" · {multiTask} 个文件多任务双份缓存" : "")
            : $"模型目录不存在: {folder}";
    }

    [RelayCommand]
    private void OpenFolder() => Explorer.OpenFolder(_models.ModelsFolder);

    /// <summary>卸载选中模型的所有推理会话（模型文件被替换后需重新加载新会话）。</summary>
    [RelayCommand]
    private void Unload()
    {
        if (SelectedFile is null)
        {
            Message = "请先选择要卸载的模型";
            return;
        }
        var name = SelectedFile.Name;
        _models.UnloadAll(name);
        Refresh();
        Message = $"已卸载 {name} 的推理会话（下次推理时重新加载）";
    }

    /// <summary>卸载全部推理会话，释放 ONNX 内存（模型目录整体更换时用）。</summary>
    [RelayCommand]
    private void UnloadAll()
    {
        var count = _models.LoadedCount;
        _models.UnloadAll();
        Refresh();
        Message = count > 0
            ? $"已卸载全部 {count} 个推理会话"
            : "当前没有已加载的推理会话";
    }

    [RelayCommand]
    private void BrowseTestImageFolder()
    {
        var resolved = ResolveTestFolder();
        string? initial = Directory.Exists(resolved) ? resolved
            : Directory.Exists(_models.ModelsFolder) ? _models.ModelsFolder
            : null;
        var picked = _dialogs.PickFolder("选择测试图片目录", initial);
        if (picked is null)
            return;
        TestImageFolder = picked;
        _folderResults = [];
        FolderResultIndex = 0;
        ResultImage = null;
        SavePrefs();
    }

    [RelayCommand(CanExecute = nameof(CanShowPrevFolderResult))]
    private void ShowPrevFolderResult()
    {
        if (FolderResultIndex > 0)
            FolderResultIndex--;
    }

    [RelayCommand(CanExecute = nameof(CanShowNextFolderResult))]
    private void ShowNextFolderResult()
    {
        if (FolderResultIndex < _folderResults.Count - 1)
            FolderResultIndex++;
    }

    partial void OnTestImageFolderChanged(string value)
    {
        OnPropertyChanged(nameof(TestImageFileCount));
        OnPropertyChanged(nameof(TestImageFolderHint));
    }

    partial void OnFolderResultIndexChanged(int value) => ApplyFolderResultAtIndex();

    [RelayCommand]
    private void CancelTest() => _testCts?.Cancel();

    [RelayCommand]
    private void OpenTestImageFolder()
    {
        var resolved = ResolveTestFolder();
        if (Directory.Exists(resolved))
            Explorer.OpenFolder(resolved);
        else
            Message = $"目录不存在: {resolved}";
    }

    [RelayCommand]
    private async Task RunTestAsync()
    {
        this.Commit();
        if (SelectedFile is null)
        {
            TestResult = "请先在列表中选择模型";
            return;
        }
        var folder = ResolveTestFolder();
        if (!Directory.Exists(folder))
        {
            TestResult = "请先选择有效的测试图片目录";
            return;
        }

        var images = ListTestImages(folder);
        if (images.Count == 0)
        {
            TestResult = "目录内没有可识别的图片（支持 bmp/jpg/png/tif）";
            return;
        }

        // 取消上一轮测试；新测试可被「取消」按钮中断（ONNX 推理本身不可中断，
        // 取消后立即恢复 UI 并丢弃结果）
        _testCts?.Cancel();
        _testCts?.Dispose();
        var cts = _testCts = new CancellationTokenSource();
        IsTesting = true;
        _folderResults = [];
        ResultImage = TryLoadPreviewImage(images[0]);

        try
        {
            TestResult = $"推理中 · {SelectedFile.Name} · 0/{images.Count}（加载模型可能需要数秒）...";
            var model = SelectedFile.Name;
            _lastModel = model;
            var task = SelectedTask;
            var confidence = Confidence;
            var pixelConfidence = PixelConfidence;
            var iou = Iou;
            var stopwatch = Stopwatch.StartNew();

            var batch = await Task.Run(() =>
            {
                var results = new List<FolderTestResult>(images.Count);
                var totalDetections = 0;
                var session = _models.Open(model, task);
                session.Run(engine =>
                {
                    InferenceTaskValidation.EnsureSupported(engine, task);
                    return 0;
                });

                for (var i = 0; i < images.Count; i++)
                {
                    cts.Token.ThrowIfCancellationRequested();
                    var path = images[i];
                    var fileName = Path.GetFileName(path);
                    try
                    {
                        using var mat = Cv2.ImDecode(File.ReadAllBytes(path), ImreadModes.Color);
                        if (mat.Empty())
                            throw new InvalidOperationException("图片解码失败");

                        using var image = VisionImageCv.FromMat(mat, ownsMat: false);
                        var detected = session.Run(engine => task switch
                        {
                            InferenceTask.ObjectDetection => DrawDetections(engine, image, mat, confidence, iou),
                            InferenceTask.Segmentation => DrawSegmentations(engine, image, mat, confidence, pixelConfidence, iou),
                            InferenceTask.PoseEstimation => DrawPoses(engine, image, mat, confidence, iou),
                            _ => 0,
                        });
                        var source = ImageConverter.ToBitmapSource(mat);
                        if (source.CanFreeze)
                            source.Freeze();
                        results.Add(new FolderTestResult(fileName, source, detected, mat.Width, mat.Height, null));
                        totalDetections += detected;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        results.Add(new FolderTestResult(fileName, null, 0, 0, 0, ex.Message));
                    }
                }

                return (results, totalDetections);
            }, cts.Token);
            stopwatch.Stop();

            cts.Token.ThrowIfCancellationRequested();

            _folderResults = batch.results;
            FolderResultIndex = 0;
            ApplyFolderResultAtIndex();
            var ok = _folderResults.Count(r => r.Error is null);
            Message = $"批量完成：{images.Count} 张 · 成功 {ok} · 总检出 {batch.totalDetections} 个"
                      + $" · 耗时 {stopwatch.ElapsedMilliseconds}ms";
        }
        catch (OperationCanceledException)
        {
            ResultImage = null;
            TestResult = "已取消：本次结果丢弃（后台推理不可中断，完成后自动退出）";
        }
        catch (VisionException ex)
        {
            ResultImage = null;
            TestResult = $"推理失败: {ex.Message}";
        }
        catch (InvalidCastException ex)
        {
            ResultImage = null;
            TestResult = $"推理失败: 模型任务与所选「{TaskLabel(SelectedTask)}」不匹配（{ex.Message}）";
        }
        catch (Exception ex)
        {
            ResultImage = null;
            TestResult = $"推理失败: {ex.Message}";
        }
        finally
        {
            IsTesting = false;
        }
    }

    /// <summary>保存测试参数（尽力而为：失败只忽略，不影响功能）。</summary>
    private void SavePrefs()
    {
        try
        {
            var prefs = new TestPrefs(SelectedFile?.Name, TestImageFolder, SelectedTask,
                Confidence, PixelConfidence, Iou);
            File.WriteAllText(PrefsPath, JsonSerializer.Serialize(prefs));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>恢复上次测试参数（阈值钳回合法区间，防手改文件后绑定越界）。</summary>
    private void LoadPrefs()
    {
        try
        {
            if (!File.Exists(PrefsPath))
                return;
            var prefs = JsonSerializer.Deserialize<TestPrefs>(File.ReadAllText(PrefsPath));
            if (prefs is null)
                return;
            _lastModel = prefs.Model;
            var folder = prefs.TestImageFolder;
            if (string.IsNullOrWhiteSpace(folder) && !string.IsNullOrWhiteSpace(prefs.TestImagePath))
            {
                // 兼容旧版单张图片路径：改为所在目录
                folder = File.Exists(prefs.TestImagePath)
                    ? Path.GetDirectoryName(prefs.TestImagePath)
                    : prefs.TestImagePath;
            }
            TestImageFolder = string.IsNullOrWhiteSpace(folder) ? "data/replay" : folder;
            SelectedTask = prefs.Task;
            Confidence = Math.Clamp(prefs.Confidence, 0.01, 0.99);
            PixelConfidence = Math.Clamp(prefs.PixelConfidence, 0.01, 0.99);
            Iou = Math.Clamp(prefs.Iou, 0.01, 0.99);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
        }
    }

    private string ResolveTestFolder() =>
        AppConfigExtensions.ResolveFolder(TestImageFolder.Trim());

    private static List<string> ListTestImages(string folder) =>
        !Directory.Exists(folder)
            ? []
            : Directory.EnumerateFiles(folder)
                .Where(f => TestImageExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

    private static BitmapSource? TryLoadPreviewImage(string path)
    {
        try
        {
            using var mat = Cv2.ImDecode(File.ReadAllBytes(path), ImreadModes.Color);
            if (mat.Empty())
                return null;

            var source = ImageConverter.ToBitmapSource(mat);
            if (source.CanFreeze)
                source.Freeze();
            return source;
        }
        catch
        {
            return null;
        }
    }

    private void ApplyFolderResultAtIndex()
    {
        if (_folderResults.Count == 0)
            return;

        var index = Math.Clamp(FolderResultIndex, 0, _folderResults.Count - 1);
        if (index != FolderResultIndex)
            FolderResultIndex = index;

        var item = _folderResults[index];
        ResultImage = item.Image;
        if (item.Error is not null)
            TestResult = $"{item.FileName} 失败: {item.Error}";
        else
            TestResult = $"{item.FileName} · 检出 {item.Count} 个 · {item.Width}×{item.Height}"
                         + $" · {FolderResultPosition}";

        ShowPrevFolderResultCommand.NotifyCanExecuteChanged();
        ShowNextFolderResultCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanShowPrevFolderResult));
        OnPropertyChanged(nameof(CanShowNextFolderResult));
        OnPropertyChanged(nameof(FolderResultPosition));
    }

    private static int DrawDetections(IInferenceEngine engine, VisionImage image, Mat mat, double confidence, double iou)
    {
        var results = engine.RunObjectDetection(image, confidence, iou);
        ModelTestOverlay.DrawDetections(mat, results);
        return results.Count;
    }

    private static int DrawSegmentations(IInferenceEngine engine, VisionImage image, Mat mat,
        double confidence, double pixelConfidence, double iou)
    {
        var results = engine.RunSegmentation(image, confidence, pixelConfidence, iou);
        ModelTestOverlay.DrawSegmentations(mat, results);
        return results.Count;
    }

    private static int DrawPoses(IInferenceEngine engine, VisionImage image, Mat mat, double confidence, double iou)
    {
        var results = engine.RunPoseEstimation(image, confidence, iou);
        ModelTestOverlay.DrawPoses(mat, results, confidence);
        return results.Count;
    }

    private static string TaskLabel(InferenceTask task) => task switch
    {
        InferenceTask.ObjectDetection => "检测",
        InferenceTask.Segmentation => "分割",
        InferenceTask.PoseEstimation => "关键点",
        _ => task.ToString(),
    };

    public void Dispose()
    {
        _testCts?.Cancel();
        _testCts?.Dispose();
        _testCts = null;
        _taskDetectCts?.Cancel();
        _taskDetectCts?.Dispose();
        _taskDetectCts = null;
    }
}
