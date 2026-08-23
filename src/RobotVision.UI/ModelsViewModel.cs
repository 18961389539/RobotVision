using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using RobotVision.Core.Models;
using RobotVision.Infrastructure.Inference;
using System.Windows.Media;

namespace RobotVision.UI;

public sealed record ModelFileItem(
    string Name, string SizeText, string ModifiedText, string LoadedText, bool Loaded);

/// <summary>
/// 模型管理：目录浏览 + 缓存会话状态 + 单模型测试推理。
/// 测试推理不经过配方/相机/标定链路：选模型 + 选图片直接跑，
/// 用于验证模型本身（框/掩码/关键点），与产线管线共享 ModelManager 会话缓存
/// （同一模型的推理经会话信号量串行，测试与产线互不并发）。
/// </summary>
public partial class ModelsViewModel : ObservableObject
{
    private readonly ModelManager _models;

    public ObservableCollection<ModelFileItem> Files { get; } = [];

    public IReadOnlyList<InferenceTask> TaskOptions { get; } = Enum.GetValues<InferenceTask>();

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

    private CancellationTokenSource? _testCts;

    /// <summary>取消正在进行的测试推理（立即恢复 UI，推理结果丢弃）。</summary>
    [RelayCommand]
    private void CancelTest() => _testCts?.Cancel();

    public ModelsViewModel(ModelManager models)
    {
        _models = models;
        Refresh();
    }

    [RelayCommand]
    public void Refresh()
    {
        Files.Clear();
        var loaded = _models.LoadedKeys
            .GroupBy(k => k.Path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => string.Join("、", g.Select(k => TaskLabel(k.Task))), StringComparer.OrdinalIgnoreCase);

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
        Message = Directory.Exists(folder)
            ? $"{Files.Count} 个模型文件 · 已缓存会话 {_models.LoadedCount} 个"
            : $"模型目录不存在: {folder}";
    }

    [RelayCommand]
    private void OpenFolder() => RecipeViewModel.ShellOpen(_models.ModelsFolder);

    [RelayCommand]
    private void BrowseTestImage()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择测试图片",
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|所有文件|*.*",
        };
        if (dialog.ShowDialog() == true)
            TestImagePath = dialog.FileName;
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

        try
        {
            TestResult = $"推理中 · {SelectedFile.Name}（首次加载模型可能需要数秒）...";
            var model = SelectedFile.Name;
            var task = SelectedTask;
            var confidence = Confidence;
            var pixelConfidence = PixelConfidence;
            var iou = Iou;
            var path = TestImagePath;
            var stopwatch = Stopwatch.StartNew();

            var (source, count) = await Task.Run(() =>
            {
                using var mat = Cv2.ImRead(path, ImreadModes.Color);
                if (mat.Empty())
                    throw new InvalidOperationException($"图片读取失败: {path}");

                using var bitmap = MatSkiaConverter.ToSKBitmap(mat);
                var session = _models.Open(model, task);
                var detected = task switch
                {
                    InferenceTask.ObjectDetection => DrawDetections(session, bitmap, mat, confidence, iou),
                    InferenceTask.Segmentation => DrawSegmentations(session, bitmap, mat, confidence, pixelConfidence, iou),
                    InferenceTask.PoseEstimation => DrawPoses(session, bitmap, mat, confidence, iou),
                    _ => 0,
                };
                return (ImageConverter.ToBitmapSource(mat), detected);
            }, cts.Token);
            stopwatch.Stop();

            // 推理期间被取消：丢弃结果
            cts.Token.ThrowIfCancellationRequested();

            ResultImage = source;
            TestResult = $"检出 {count} 个目标 · 耗时 {stopwatch.ElapsedMilliseconds}ms · {TaskLabel(task)}任务";
            Refresh(); // 会话缓存状态可能已变化
        }
        catch (OperationCanceledException)
        {
            ResultImage = null;
            TestResult = "测试已取消";
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

    private static int DrawDetections(ModelSession session, SkiaSharp.SKBitmap bitmap, Mat mat, double confidence, double iou)
    {
        var results = session.Run(y => y.RunObjectDetection(bitmap, confidence, iou));
        ModelTestOverlay.DrawDetections(mat, results);
        return results.Count;
    }

    private static int DrawSegmentations(ModelSession session, SkiaSharp.SKBitmap bitmap, Mat mat,
        double confidence, double pixelConfidence, double iou)
    {
        var results = session.Run(y => y.RunSegmentation(bitmap, confidence, pixelConfidence, iou));
        ModelTestOverlay.DrawSegmentations(mat, results);
        return results.Count;
    }

    private static int DrawPoses(ModelSession session, SkiaSharp.SKBitmap bitmap, Mat mat, double confidence, double iou)
    {
        var results = session.Run(y => y.RunPoseEstimation(bitmap, confidence, iou));
        ModelTestOverlay.DrawPoses(mat, results);
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
