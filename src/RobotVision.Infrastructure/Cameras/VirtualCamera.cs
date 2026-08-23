using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;

namespace RobotVision.Infrastructure.Cameras;

public enum VirtualPattern
{
    /// <summary>棋盘格（默认）：尺寸取图像的一半，逐帧平移变化姿态，可配合内参标定向导。</summary>
    Chessboard,

    /// <summary>彩色旋转矩形与圆：逐帧移动，用于链路/显示联调。</summary>
    Shapes,

    /// <summary>纯色竖条纹：颜色/噪声检查基准图。</summary>
    Bars,
}

/// <summary>
/// 虚拟相机：不依赖任何硬件与回放图片，按图案程序化生成 BGR 帧。
/// 用途：1) 无 Basler 相机/pylon 环境下联调全链路；2) 内参标定向导的棋盘格来源；
/// 3) 噪声/耗时参数模拟真实采集行为（IntervalMs 模拟曝光延时、NoiseSigma 模拟传感器噪声）。
/// 棋盘格内角点数 = (Width/CellPx/2 - 1) × (Height/CellPx/2 - 1)，默认 1280×960/40px 即 15×11。
/// 噪声/转换的中间 Mat 按字段复用（Grab 在锁内串行，无并发访问），避免每帧大块分配。
/// </summary>
public sealed class VirtualCamera : ICamera
{
    private readonly int _width;
    private readonly int _height;
    private readonly int _cellPx;
    private readonly int _intervalMs;
    private readonly double _noiseSigma;
    private readonly VirtualPattern _pattern;
    private readonly int _boardCols;
    private readonly int _boardRows;
    private readonly object _grabLock = new();
    private int _frameIndex;
    private Mat? _noiseBuffer;
    private Mat? _conversionBuffer;

    public string Id { get; }

    public CameraKind Kind => CameraKind.Virtual;

    /// <summary>棋盘图案的内角点规格（列×行）；其他图案为 0×0。标定向导据此同步棋盘参数，
    /// 避免按物理棋盘默认值（如 9×6）查询虚拟棋盘导致检测失败或子网格错位。</summary>
    public Size ChessboardInnerCorners =>
        _pattern == VirtualPattern.Chessboard ? new Size(_boardCols - 1, _boardRows - 1) : default;

    public VirtualCamera(string id, int width = 1280, int height = 960, string pattern = "Chessboard",
        int intervalMs = 0, double noiseSigma = 0, int chessCellPx = 40)
    {
        if (width <= 0 || height <= 0)
            throw new VisionException(VisionErrorCode.CameraInitFailed, $"虚拟相机 {id} 分辨率非法: {width}×{height}");
        if (chessCellPx < 8)
            throw new VisionException(VisionErrorCode.CameraInitFailed, $"虚拟相机 {id} 棋盘格单元过小: {chessCellPx}px");
        if (intervalMs < 0 || noiseSigma < 0)
            throw new VisionException(VisionErrorCode.CameraInitFailed, $"虚拟相机 {id} 延时/噪声不能为负");
        if (!Enum.TryParse<VirtualPattern>(pattern, ignoreCase: true, out var parsed))
            throw new VisionException(VisionErrorCode.CameraInitFailed,
                $"虚拟相机 {id} 图案不支持: {pattern}（可选 {string.Join("/", Enum.GetNames<VirtualPattern>())}）");

        Id = id;
        _width = width;
        _height = height;
        _cellPx = chessCellPx;
        _intervalMs = intervalMs;
        _noiseSigma = noiseSigma;
        _pattern = parsed;
        // 棋盘占图像的一半（保证有移动余量），内角点数 = 单元数 - 1
        _boardCols = Math.Max(2, width / chessCellPx / 2);
        _boardRows = Math.Max(2, height / chessCellPx / 2);
    }

    public CameraFrame Grab(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock (_grabLock)
        {
            if (_intervalMs > 0)
            {
                // 可取消的等待（模拟采集曝光耗时），被取消则按取消语义处理
                ct.WaitHandle.WaitOne(_intervalMs);
                ct.ThrowIfCancellationRequested();
            }

            var frame = _pattern switch
            {
                VirtualPattern.Chessboard => DrawChessboard(_frameIndex),
                VirtualPattern.Shapes => DrawShapes(_frameIndex),
                _ => DrawBars(),
            };
            _frameIndex++;

            if (_noiseSigma > 0)
            {
                // 8U 上的 Randn 会把负值截断为 0（单极性），白/黑条纹会整体饱和；
                // 经 16S 有符号中间量相加再饱和回 8U，得到真实传感器的双极性噪声。
                // 中间 Mat 按字段复用，减少每帧大块分配。
                _noiseBuffer ??= new Mat(frame.Size(), MatType.CV_16SC3);
                _conversionBuffer ??= new Mat();
                Cv2.Randn(_noiseBuffer, Scalar.All(0), Scalar.All(_noiseSigma));
                frame.ConvertTo(_conversionBuffer, MatType.CV_16SC3);
                Cv2.Add(_conversionBuffer, _noiseBuffer, _conversionBuffer);
                _conversionBuffer.ConvertTo(frame, MatType.CV_8UC3);
            }

            return new CameraFrame(frame, DateTime.UtcNow);
        }
    }

    /// <summary>白底黑格棋盘；帧间按不同频率正弦/余弦平移，产生多姿态（标定采集需要）。</summary>
    private Mat DrawChessboard(int i)
    {
        var mat = new Mat(_height, _width, MatType.CV_8UC3, Scalar.All(255));

        var boardW = _boardCols * _cellPx;
        var boardH = _boardRows * _cellPx;
        var marginX = (_width - boardW) / 2;
        var marginY = (_height - boardH) / 2;
        var ampX = Math.Max(0, marginX - 2);
        var ampY = Math.Max(0, marginY - 2);
        var ox = marginX + (int)(ampX * Math.Sin(i * 0.4));
        var oy = marginY + (int)(ampY * Math.Cos(i * 0.33));

        for (var r = 0; r < _boardRows; r++)
            for (var c = 0; c < _boardCols; c++)
                if ((r + c) % 2 == 0)
                    Cv2.Rectangle(mat,
                        new Rect(ox + c * _cellPx, oy + r * _cellPx, _cellPx, _cellPx),
                        Scalar.Black, -1);
        return mat;
    }

    /// <summary>深底彩色旋转矩形与圆，帧间缓动。</summary>
    private Mat DrawShapes(int i)
    {
        var mat = new Mat(_height, _width, MatType.CV_8UC3, Scalar.All(40));

        DrawRotatedRect(mat, new RotatedRect(
            new Point2f(_width / 4f + 40 * (float)Math.Sin(i * 0.3), _height / 4f),
            new Size2f(180, 100), 30), new Scalar(0, 200, 0));
        DrawRotatedRect(mat, new RotatedRect(
            new Point2f(_width * 3f / 4, _height / 4f + 30 * (float)Math.Cos(i * 0.25)),
            new Size2f(120, 120), -20), new Scalar(255, 140, 0));
        Cv2.Circle(mat,
            new Point(_width / 2 + (int)(60 * Math.Sin(i * 0.2)), (int)(_height * 0.75)),
            _height / 8, new Scalar(60, 60, 255), -1);
        return mat;
    }

    private static void DrawRotatedRect(Mat mat, RotatedRect box, Scalar color)
    {
        var points = Array.ConvertAll(Cv2.BoxPoints(box), p => new Point(Math.Round(p.X), Math.Round(p.Y)));
        Cv2.FillConvexPoly(mat, points, color);
    }

    private static readonly Scalar[] BarsColors =
    {
        new(255, 255, 255), new(255, 255, 0), new(0, 255, 255),
        new(0, 255, 0), new(255, 0, 255), new(255, 0, 0),
        new(0, 0, 255), new(0, 0, 0),
    };

    /// <summary>8 色竖条纹（静态基准图）。色表为 static readonly，避免每帧重复分配数组。</summary>
    private Mat DrawBars()
    {
        var mat = new Mat(_height, _width, MatType.CV_8UC3);
        var barWidth = Math.Max(1, _width / BarsColors.Length);
        for (var i = 0; i < BarsColors.Length; i++)
            Cv2.Rectangle(mat, new Rect(i * barWidth, 0, barWidth, _height), BarsColors[i], -1);
        return mat;
    }

    public void Dispose()
    {
        // 与 Grab 的 _grabLock 互斥：防止 Grab 正使用 _noiseBuffer/_conversionBuffer 时
        // 被释放（悬垂访问），Basler/FileCamera 均采用锁内清理，保持一致
        lock (_grabLock)
        {
            _noiseBuffer?.Dispose();
            _noiseBuffer = null;
            _conversionBuffer?.Dispose();
            _conversionBuffer = null;
        }
    }
}
