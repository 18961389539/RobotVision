using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using RobotVision.Core.Models;
using RobotVision.Infrastructure;

namespace RobotVision.Hosting;

/// <summary>
/// 成功产品现场图留存（默认关，见 <see cref="CaptureSuccessConfig"/>）：
/// 开启后成功检测也把去畸变图落盘，元数据 JSON 与图同名（配方/坐标/角度/置信度/耗时）。
/// 目录结构按配方分文件夹、内按天分目录，原图与绘制图再分子目录：
/// {留存根}\{配方}\{yyyy-MM-dd}\original\{时间戳}_{配方}_OK.png   （去畸变原图）
/// {留存根}\{配方}\{yyyy-MM-dd}\overlay\{时间戳}_{配方}_OK.png   （绘制图，十字/框/ROI）
/// 与 <see cref="FailureImageStore"/> 同模式：克隆在调用线程完成，PNG 编码/写盘移到后台线程池，
/// 绝不阻塞检测节拍；默认关闭避免高速节拍下磁盘暴涨，产线需要复检/工艺分析时再开。
/// </summary>
public sealed class SuccessCaptureStore
{
    private const string OriginalSubDir = "original";
    private const string OverlaySubDir = "overlay";

    private sealed record CaptureMeta(
        string Recipe, string T, double? X, double? Y, double? Angle, double? Confidence,
        int Count, double ElapsedMs, string? CameraId, string? StationId, string? AngleMode,
        bool Overlay = false);

    private static readonly JsonSerializerOptions MetaJsonOptions = new() { WriteIndented = true };

    private readonly string _folder;
    private readonly ILogger<SuccessCaptureStore> _log;
    private readonly object _sync = new();

    public SuccessCaptureStore(CaptureSuccessConfig cfg, ILogger<SuccessCaptureStore> log)
    {
        _folder = AppConfigExtensions.ResolveFolder(cfg.Folder);
        _log = log;
        Enabled = cfg.Enabled;
        SaveOverlay = cfg.SaveOverlay;
        MaxWidth = cfg.MaxWidth;
        RetainedDays = cfg.RetainedDays;
    }

    /// <summary>保存去畸变原图（热属性；管理界面可切换）。</summary>
    public bool Enabled { get; set; }

    /// <summary>另存绘制图（热属性）。</summary>
    public bool SaveOverlay { get; set; }

    /// <summary>缩图最大宽度；0 = 原图。</summary>
    public int MaxWidth { get; set; }

    /// <summary>按天目录保留天数；≤0 不清理。</summary>
    public int RetainedDays { get; set; }

    /// <summary>保存配置后热应用开关与保留策略（目录启动时锚定，不热切换）。</summary>
    public void ApplyConfig(CaptureSuccessConfig cfg)
    {
        Enabled = cfg.Enabled;
        SaveOverlay = cfg.SaveOverlay;
        MaxWidth = cfg.MaxWidth;
        RetainedDays = cfg.RetainedDays;
    }

    public string Folder => _folder;

    /// <summary>
    /// 提交一次成功留存（尽力而为）：克隆（或缩图）在调用线程完成并立即返回，
    /// PNG 编码/元数据/清理移到后台线程池，绝不在管线线程同步落盘拖累产线。
    /// </summary>
    public void Save(
        string recipeName,
        VisionImage image,
        IReadOnlyList<RobotPose> poses,
        VisionResult result,
        FailureContext? context = null,
        VisionImage? overlay = null)
    {
        if (image.IsEmpty)
            return;

        var wantOriginal = Enabled;
        var wantOverlay = SaveOverlay && overlay is not null && !overlay.IsEmpty;
        if (!wantOriginal && !wantOverlay)
            return;

        try
        {
            using var mat = VisionImageCv.AsMat(image);
            Mat? originalClone = null;
            Mat? overlayClone = null;
            if (wantOriginal)
                originalClone = CloneOrDownscale(mat);
            if (wantOverlay)
            {
                using var overlayMat = VisionImageCv.AsMat(overlay!);
                overlayClone = CloneOrDownscale(overlayMat);
            }

            var savedAt = DateTime.Now;
            var first = poses.Count > 0 ? poses[0] : null;
            var meta = new CaptureMeta(
                recipeName, savedAt.ToString("O"),
                first?.X, first?.Y, first?.AngleDeg,
                result.Confidences.Count > 0 ? result.Confidences[0] : null,
                poses.Count, result.ElapsedMs,
                context?.CameraId, context?.StationId, context?.AngleMode);

            _ = Task.Run(() => WriteCore(originalClone, overlayClone, recipeName, savedAt, meta));
        }
        catch (Exception ex)
        {
            SuccessCaptureStoreLog.EnqueueFailed(_log, ex);
        }
    }

    /// <summary>后台线程实际落盘：PNG + JSON 元数据（_sync 串行）+ 超期清理。
    /// 按配方分文件夹、内按天分目录，原图与绘制图分入 original/overlay 子目录。</summary>
    private void WriteCore(Mat? original, Mat? overlay, string recipe, DateTime savedAt, CaptureMeta meta)
    {
        try
        {
            lock (_sync)
            {
                var dayDir = Path.Combine(
                    _folder, recipe, savedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

                var baseName = $"{savedAt:yyyyMMdd_HHmmssfff}_{recipe}_OK";
                if (original is not null)
                    WritePair(dayDir, OriginalSubDir, original, baseName, meta with { Overlay = false });
                if (overlay is not null)
                    WritePair(dayDir, OverlaySubDir, overlay, baseName, meta with { Overlay = true });

                if (RetainedDays > 0)
                    Cleanup(DateTime.Now);
            }
        }
        catch (Exception ex)
        {
            SuccessCaptureStoreLog.SaveFailed(_log, ex);
        }
        finally
        {
            original?.Dispose();
            overlay?.Dispose();
        }
    }

    private static void WritePair(string dayDir, string subDir, Mat image, string baseName, CaptureMeta meta)
    {
        var dir = Path.Combine(dayDir, subDir);
        Directory.CreateDirectory(dir);
        var png = Path.Combine(dir, baseName + ".png");
        for (var i = 1; File.Exists(png); i++)
            png = Path.Combine(dir, $"{baseName}_{i}.png");

        Cv2.ImWrite(png, image);
        File.WriteAllText(Path.ChangeExtension(png, ".json"),
            JsonSerializer.Serialize(meta, MetaJsonOptions));
    }

    private Mat CloneOrDownscale(Mat mat) =>
        MaxWidth > 0 && mat.Width > MaxWidth ? Downscale(mat, MaxWidth) : mat.Clone();

    /// <summary>删除超过保留天数的按天目录（位于 {配方}\{yyyy-MM-dd} 二级目录，递归删除原图/绘制图）。</summary>
    private void Cleanup(DateTime now)
    {
        var cutoff = now.Date.AddDays(-RetainedDays);
        foreach (var recipeDir in Directory.EnumerateDirectories(_folder))
        {
            foreach (var dir in Directory.EnumerateDirectories(recipeDir))
            {
                var name = Path.GetFileName(dir);
                if (DateTime.TryParseExact(name, "yyyy-MM-dd",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var day) && day < cutoff)
                {
                    try { Directory.Delete(dir, recursive: true); }
                    catch (Exception ex) { SuccessCaptureStoreLog.CleanupDirFailed(_log, ex, dir); }
                }
            }
        }
    }

    private static Mat Downscale(Mat src, int maxWidth)
    {
        var ratio = (double)maxWidth / src.Width;
        var size = new Size(maxWidth, (int)Math.Round(src.Height * ratio));
        var dst = new Mat();
        Cv2.Resize(src, dst, size, 0, 0, InterpolationFlags.Area);
        return dst;
    }
}
