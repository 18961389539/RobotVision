using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Infrastructure;

namespace RobotVision.Infrastructure.Cameras;

/// <summary>
/// 文件夹回放相机：按文件名顺序循环读取图片。
/// 用途：1) 无相机时联调 TCP/流程；2) 算法回归测试（同一批图对比结果）。
/// IntervalMs &gt; 0 时按固定间隔回放（帧率受控，联调更接近真实采集节拍）。
/// </summary>
public sealed class FileCamera : ICamera
{
    private static readonly string[] Extensions = [".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff"];

    private readonly string[] _files;
    private readonly bool _loop;
    private readonly int _intervalMs;
    private readonly object _lock = new();
    private int _index;
    private string? _lastFile;

    public string Id { get; }

    public CameraKind Kind => CameraKind.File;

    public FileCamera(string id, string folder, bool loop = true, int intervalMs = 0)
    {
        if (!Directory.Exists(folder))
            throw new VisionException(VisionErrorCode.CameraInitFailed, $"回放目录不存在: {folder}");
        if (intervalMs < 0)
            throw new VisionException(VisionErrorCode.CameraInitFailed, $"回放间隔不能为负: {intervalMs}");

        var files = Directory.EnumerateFiles(folder)
            .Where(f => Extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (files.Length == 0)
            throw new VisionException(VisionErrorCode.CameraInitFailed, $"回放目录中没有图片: {folder}");

        Id = id;
        _files = files;
        _loop = loop;
        _intervalMs = intervalMs;
    }

    /// <summary>
    /// 目录内全部回放文件（文件名序）。配方页赛马打分走磁盘解码，不要用本列表去 <see cref="Grab"/>，
    /// 以免推进产线回放下标。
    /// </summary>
    public IReadOnlyList<string> PlaybackFiles => Array.AsReadOnly(_files);

    /// <summary>按字节流解码图片（中文路径安全）。不改变回放下标。</summary>
    public static Mat DecodeFile(string path) => ReadImage(path);

    public CameraFrame Grab(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (_intervalMs > 0)
        {
            // 可取消的等待（模拟采集曝光耗时），被取消则按取消语义处理
            ct.WaitHandle.WaitOne(_intervalMs);
            ct.ThrowIfCancellationRequested();
        }

        string file;
        lock (_lock)
        {
            if (_index >= _files.Length)
            {
                if (!_loop)
                    throw new VisionException(VisionErrorCode.CameraGrabFailed, "回放图片已用尽");
                _index = 0;
            }
            file = _files[_index++];
            _lastFile = file;
        }

        return new CameraFrame(VisionImageCv.FromMat(ReadImage(file), ownsMat: true), DateTime.UtcNow);
    }

    /// <summary>再读上次 <see cref="Grab"/> 的文件，不推进下标。示教/框选要沿用当前画面。</summary>
    public CameraFrame RepeatLast(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        string? file;
        lock (_lock)
            file = _lastFile;
        if (string.IsNullOrEmpty(file))
            return Grab(ct);
        return new CameraFrame(VisionImageCv.FromMat(ReadImage(file), ownsMat: true), DateTime.UtcNow);
    }

    /// <summary>
    /// 按字节流解码：OpenCV ImRead 对非 ASCII（中文）路径在 Windows 上常返回空图。
    /// 与 ChessboardIntrinsicCalibrator 同一口径。
    /// </summary>
    private static Mat ReadImage(string file)
    {
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new VisionException(VisionErrorCode.CameraGrabFailed, $"无法读取图像: {file}");
        }

        var mat = Cv2.ImDecode(bytes, ImreadModes.Color);
        if (mat.Empty())
            throw new VisionException(VisionErrorCode.CameraGrabFailed, $"无法读取图像: {file}");
        return mat;
    }

    public void Dispose()
    {
    }
}
