namespace RobotVision.Hosting;

/// <summary>磁盘彩色图解码（供模型页缩略图等 WPF 展示，OpenCV 仅在此层）。</summary>
public interface IImageFileReader
{
  BgraImageBuffer? TryReadColorImage(string path);
}
