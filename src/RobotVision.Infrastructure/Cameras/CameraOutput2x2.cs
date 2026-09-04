using System.Diagnostics.CodeAnalysis;
using OpenCvSharp;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;

namespace RobotVision.Infrastructure.Cameras;

/// <summary>
/// 真实相机经 <see cref="CameraManager"/> 的出图为 2×2：
/// 硬件已 binning/decimation 且尺寸吻合则直通；否则软件减半。
/// File / Virtual 保持原分辨率。
/// </summary>
internal static class CameraOutput2x2
{
    private const int SizeSlackPx = 16;

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "新帧所有权交给调用方；被替换的原帧在本方法内释放。")]
    public static CameraFrame Ensure(ICamera camera, CameraFrame frame)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(frame);

        if (camera.Kind is CameraKind.File or CameraKind.Virtual)
            return frame;

        if (camera is IHardware2x2Output hardware &&
            hardware.HasHardware2x2 &&
            MatchesExpected(frame.Image, hardware.ExpectedWidth, hardware.ExpectedHeight))
            return frame;

        if (camera.Kind == CameraKind.Real)
            return Half(frame);

        return frame;
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "新帧所有权交给调用方；被替换的原帧在本方法内释放。")]
    public static CameraFrame Half(CameraFrame source)
    {
        var src = source.Image;
        var width = Math.Max(1, src.Width / 2);
        var height = Math.Max(1, src.Height / 2);
        if (width == src.Width && height == src.Height)
            return source;

        try
        {
            using var mat = VisionImageCv.AsMat(src);
            var dst = new Mat();
            try
            {
                Cv2.Resize(mat, dst, new Size(width, height), 0, 0, InterpolationFlags.Area);
                var image = VisionImageCv.Adopt(dst);
                dst = null;
                return new CameraFrame(image, source.CapturedAtUtc, source.AcquireMs, source.ConvertMs);
            }
            finally
            {
                dst?.Dispose();
            }
        }
        finally
        {
            source.Dispose();
        }
    }

    private static bool MatchesExpected(VisionImage image, int expectedWidth, int expectedHeight) =>
        expectedWidth > 0 && expectedHeight > 0 &&
        Math.Abs(image.Width - expectedWidth) <= SizeSlackPx &&
        Math.Abs(image.Height - expectedHeight) <= SizeSlackPx;
}
