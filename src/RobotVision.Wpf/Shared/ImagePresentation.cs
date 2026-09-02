using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RobotVision.Core.Models;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Shared;

/// <summary>WPF 展示适配：应用层字节/图像 → 冻结的 <see cref="ImageSource"/>。</summary>
public static class ImagePresentation
{
    public static ImageSource FromVisionImage(VisionImage image) =>
        ImageConverter.ToBitmapSource(image);

    public static ImageSource FromPngBytes(ReadOnlySpan<byte> png)
    {
        using var stream = new MemoryStream(png.ToArray());
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        frame.Freeze();
        return frame;
    }
}
