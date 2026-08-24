using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using RobotVision.Core.Models;
using RobotVision.Infrastructure.Inference;
using System.Windows.Media;

namespace RobotVision.WpfHost;

public sealed record ModelFileItem(
    string Name, string SizeText, string ModifiedText, string LoadedText, bool Loaded);

/// <summary>推理任务下拉项（中文显示，Value 保持枚举便于绑定 SelectedTask）。</summary>
public sealed record TaskOption(InferenceTask Value, string Label);

/// <summary>
/// 模型管理：目录浏览 + 缓存会话状态 + 单模型测试推理。
/// 测试推理不经过配方/相机/标定链路：选模型 + 选图片直接跑，用于验证模型本身
/// （框/掩码/关键点）。测试使用 IInferenceEngineFactory 创建的独立引擎实例
/// （用完即释放）：不占用产线会话与信号量，长耗时测试不会阻塞产线 TRIGGER；
/// 代价是每次测试重新加载模型（一次性内存峰值，调试场景可接受）。
/// 测试参数（模型/任务/阈值/图片路径）持久化到 exe 旁 model-test.prefs.json。
/// </summary>
public partial class ModelsViewModel : ObservableObject
{
    /// <summary>测试参数持久化文件（exe 旁，随部署目录走）。</summary>
    private static readonly string PrefsPath =
        Path.Combine(AppContext.BaseDirectory, "model-test.prefs.json");

    private sealed record TestPrefs(
        string? Model, string? TestImagePath, InferenceTask Task,
        double Confidence, double PixelConfidence, double Iou);

    private readonly ModelManager _models;
    private readonly IInferenceEngineFactory _engineFactory;

    public ObservableCollection<ModelFileItem> Files { get; } = [];

    /// <summary>推理任务下拉（中文：检测/分割/关键点），SelectedValue 绑定枚举。</summary>
    public IReadOnlyList<TaskOption> TaskOptions { get; } =
        Enum.GetValues<InferenceTask>().Select(t => new TaskOption(t, TaskLabel(t))).ToArray();

    [ObservableProperty]
    private string _message = "";

    [ObservableProperty]
    private ModelFileItem? _selectedFile;

    [ObservableProperty]
    private InferenceTask _selectedTask = InferenceTask.PoseEstimation;

    [ObservableProperty]
    private double _confidence = 0.5;

    /// <summary>分割掩码的像素置信度阈值（仅分割任务使用）。</summary>
    [ObservableProperty]
    private double _pixelConfidence = 0.65;

    [ObservableProperty]
    private double _iou = 0.7;

    [ObservableProperty]
    private string _testImagePath = "";

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

    /// <summary>参数浮动面板是否可见（方案 A：图像主导，面板可收起，收起后点右上角"参数"重新打开）。</summary>
    [ObservableProperty]
    private bool _isParamPanelVisible = true;

    /// <summary>收起/展开参数浮动面板。</summary>
    [RelayCommand]
    private void ToggleParamPanel() => IsParamPanelVisible = !IsParamPanelVisible;

    private CancellationTokenSource? _testCts;

    public ModelsViewModel(ModelManager models, IInferenceEngineFactory engineFactory)
    {
        _models = models;
        _engineFactory = engineFactory;
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
                    file.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                    loadedText,
                    loaded.ContainsKey(file.FullName)));
            }
        }

        SelectedFile = Files.FirstOrDefault(f => f.Name == SelectedFile?.Name) ?? Files.FirstOrDefault();

        // 同一文件被多个任务打开时各占一份会话内存（缓存键含任务），提示用户代价
        var multiTask = groups.Count(g => g.Count() > 1);
        Message = Directory.Exists(folder)
            ? $"{Files.Count} 个模型文件 · 已缓存会话 {_models.LoadedCount} 个"
              + (multiTask > 0 ? $" · {multiTask} 个文件多任务双份缓存" : "")
            : $"模型目录不存在: {folder}";
    }

    [RelayCommand]
    private void OpenFolder() => RecipeViewModel.ShellOpen(_models.ModelsFolder);

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
    private void BrowseTestImage()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择测试图片",
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|所有文件|*.*",
        };
        // 默认打开上次图片所在目录；无历史则打开模型目录，避免每次从头导航
        var dir = Path.GetDirectoryName(TestImagePath);
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            dir = Directory.Exists(_models.ModelsFolder) ? _models.ModelsFolder : "";
        if (dir.Length > 0)
            dialog.InitialDirectory = dir;
        if (dialog.ShowDialog() == true)
        {
            TestImagePath = dialog.FileName;
            SavePrefs();
        }
    }

    [RelayCommand]
    private async Task RunTestAsync()
    {
        if (SelectedFile is null)
        {
            TestResult = "请先在列表中选择模型";
            return;
        }
        if (!File.Exists(TestImagePath))
        {
            TestResult = "请先选择测试图片";
            return;
        }

        // 取消上一轮测试；新测试可被「取消」按钮中断（ONNX 推理本身不可中断，
        // 取消后立即恢复 UI 并丢弃结果）
        _testCts?.Cancel();
        _testCts?.Dispose();
        var cts = _testCts = new CancellationTokenSource();
        IsTesting = true;
        SavePrefs();

        try
        {
            TestResult = $"推理中 · {SelectedFile.Name}（独立引擎加载模型可能需要数秒）...";
            var model = SelectedFile.Name;
            _lastModel = model;
            var task = SelectedTask;
            var confidence = Confidence;
            var pixelConfidence = PixelConfidence;
            var iou = Iou;
            var path = TestImagePath;
            var stopwatch = Stopwatch.StartNew();

            var (source, count, width, height) = await Task.Run(() =>
            {
                // imdecode 按字节流解码：imread 对非 ASCII（中文）路径支持不稳
                using var mat = Cv2.ImDecode(File.ReadAllBytes(path), ImreadModes.Color);
                if (mat.Empty())
                    throw new InvalidOperationException($"图片读取失败: {path}");

                // 独立引擎：不占用产线会话缓存与信号量，测试不阻塞产线 TRIGGER
                using var engine = _engineFactory.Create(_models.ResolvePath(model));
                using var bitmap = MatSkiaConverter.ToSKBitmap(mat);
                var detected = task switch
                {
                    InferenceTask.ObjectDetection => DrawDetections(engine, bitmap, mat, confidence, iou),
                    InferenceTask.Segmentation => DrawSegmentations(engine, bitmap, mat, confidence, pixelConfidence, iou),
                    InferenceTask.PoseEstimation => DrawPoses(engine, bitmap, mat, confidence, iou),
                    _ => 0,
                };
                return (ImageConverter.ToBitmapSource(mat), detected, mat.Width, mat.Height);
            }, cts.Token);
            stopwatch.Stop();

            // 推理期间被取消：丢弃结果
            cts.Token.ThrowIfCancellationRequested();

            ResultImage = source;
            TestResult = $"检出 {count} 个目标 · 图片 {width}×{height} · 耗时 {stopwatch.ElapsedMilliseconds}ms · {TaskLabel(task)}任务";
            Refresh(); // 会话缓存状态可能已变化
        }
        catch (OperationCanceledException)
        {
            ResultImage = null;
            TestResult = "已取消：本次结果丢弃（后台推理不可中断，完成后自动退出）";
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
            var prefs = new TestPrefs(SelectedFile?.Name, TestImagePath, SelectedTask,
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
            TestImagePath = prefs.TestImagePath ?? "";
            SelectedTask = prefs.Task;
            Confidence = Math.Clamp(prefs.Confidence, 0.01, 0.99);
            PixelConfidence = Math.Clamp(prefs.PixelConfidence, 0.01, 0.99);
            Iou = Math.Clamp(prefs.Iou, 0.01, 0.99);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
        }
    }

    private static int DrawDetections(IInferenceEngine engine, SkiaSharp.SKBitmap bitmap, Mat mat, double confidence, double iou)
    {
        var results = engine.RunObjectDetection(bitmap, confidence, iou);
        ModelTestOverlay.DrawDetections(mat, results);
        return results.Count;
    }

    private static int DrawSegmentations(IInferenceEngine engine, SkiaSharp.SKBitmap bitmap, Mat mat,
        double confidence, double pixelConfidence, double iou)
    {
        var results = engine.RunSegmentation(bitmap, confidence, pixelConfidence, iou);
        ModelTestOverlay.DrawSegmentations(mat, results);
        return results.Count;
    }

    private static int DrawPoses(IInferenceEngine engine, SkiaSharp.SKBitmap bitmap, Mat mat, double confidence, double iou)
    {
        var results = engine.RunPoseEstimation(bitmap, confidence, iou);
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
}
