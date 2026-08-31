using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using RobotVision.Core.Models;
using RobotVision.Infrastructure;

namespace RobotVision.Hosting;

/// <summary>
/// 成功产品现场图留存（默认关，见 <see cref="CaptureSuccessConfig"/>）：
/// 开启后成功检测也把去畸变图落盘（data/captures/yyyy-MM-dd/ 按天分目录），
/// 元数据 JSON 与图同名（配方/坐标/角度/置信度/耗时）。
/// 与 <see cref="FailureImageStore"/> 同模式：克隆在调用线程完成，PNG 编码/写盘移到后台线程池，
/// 绝不阻塞检测节拍；默认关闭避免高速节拍下磁盘暴涨，产线需要复检/工艺分析时再开。
/// </summary>
public sealed class SuccessCaptureStore
{
    private sealed record CaptureMeta(
        string Recipe, string T, double? X, double? Y, double? Angle, double? Confidence,
        int Count, double ElapsedMs, string? CameraId, string? StationId, string? AngleMode);

    private static readonly JsonSerializerOptions MetaJsonOptions = new() { WriteIndented = true };

    private readonly string _folder;
    private readonly ILogger<SuccessCaptureStore> _log;
    private readonly object _sync = new();

    public SuccessCaptureStore(CaptureSuccessConfig cfg, ILogger<SuccessCaptureStore> log)
    {
        _folder = AppConfigExtensions.ResolveFolder(cfg.Folder);
        _log = log;
        Enabled = cfg.Enabled;
        MaxWidth = cfg.MaxWidth;
        RetainedDays = cfg.RetainedDays;
    }

    /// <summary>运行时开关（热属性；管理界面可切换）。</summary>
    public bool Enabled { get; set; }

    /// <summary>缩图最大宽度；0 = 原图。</summary>
    public int MaxWidth { get; set; }

    /// <summary>按天目录保留天数；≤0 不清理。</summary>
    public int RetainedDays { get; set; }

    /// <summary>保存配置后热应用开关与保留策略（目录启动时锚定，不热切换）。</summary>
    public void ApplyConfig(CaptureSuccessConfig cfg)
    {
        Enabled = cfg.Enabled;
        MaxWidth = cfg.MaxWidth;
        RetainedDays = cfg.RetainedDays;
    }

    public string Folder => _folder;

    /// <summary>
    /// 提交一次成功留存（尽力而为）：克隆（或缩图）在调用线程完成并立即返回，
    /// PNG 编码/元数据/清理移到后台线程池，绝不在管线线程同步落盘拖累产线。
    /// </summary>
    public void Save(string recipeName, VisionImage image, IReadOnlyList<RobotPose> poses,
        VisionResult result, FailureContext? context = null)
    {
        if (!Enabled || image.IsEmpty)
            return;

        try
        {
            using var mat = VisionImageCv.AsMat(image);
            var clone = MaxWidth > 0 && mat.Width > MaxWidth
                ? Downscale(mat, MaxWidth)
                : mat.Clone();
            var savedAt = DateTime.Now;
            var first = poses.Count > 0 ? poses[0] : null;
            var meta = new CaptureMeta(
                recipeName, savedAt.ToString("O"),
                first?.X, first?.Y, first?.AngleDeg,
                result.Confidences.Count > 0 ? result.Confidences[0] : null,
                poses.Count, result.ElapsedMs,
                context?.CameraId, context?.StationId, context?.AngleMode);

            _ = Task.Run(() => WriteCore(clone, recipeName, savedAt, meta));
        }
        catch (Exception ex)
        {
            SuccessCaptureStoreLog.EnqueueFailed(_log, ex);
        }
    }

    /// <summary>后台线程实际落盘：PNG + JSON 元数据（_sync 串行）+ 超期清理。</summary>
    private void WriteCore(Mat image, string recipe, DateTime savedAt, CaptureMeta meta)
    {
        try
        {
            lock (_sync)
            {
                var dayDir = Path.Combine(_folder, savedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                Directory.CreateDirectory(dayDir);

                var baseName = $"{savedAt:yyyyMMdd_HHmmssfff}_{recipe}_OK";
                var png = Path.Combine(dayDir, baseName + ".png");
                for (var i = 1; File.Exists(png); i++)
                    png = Path.Combine(dayDir, $"{baseName}_{i}.png");

                Cv2.ImWrite(png, image);
                File.WriteAllText(Path.ChangeExtension(png, ".json"),
                    JsonSerializer.Serialize(meta, MetaJsonOptions));

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
            // 后台线程持有克隆副本的所有权，落盘后释放
            image.Dispose();
        }
    }

    /// <summary>删除超过保留天数的按天目录（目录名 yyyy-MM-dd）。</summary>
    private void Cleanup(DateTime now)
    {
        var cutoff = now.Date.AddDays(-RetainedDays);
        foreach (var dir in Directory.EnumerateDirectories(_folder))
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

    private static Mat Downscale(Mat src, int maxWidth)
    {
        var ratio = (double)maxWidth / src.Width;
        var size = new Size(maxWidth, (int)Math.Round(src.Height * ratio));
        var dst = new Mat();
        Cv2.Resize(src, dst, size, 0, 0, InterpolationFlags.Area);
        return dst;
    }
}
