using OpenCvSharp;

namespace RobotVision.Hosting;

internal sealed class ImageFileReader : IImageFileReader
{
    public BgraImageBuffer? TryReadColorImage(string path)
    {
        try
        {
            using var mat = Cv2.ImDecode(File.ReadAllBytes(path), ImreadModes.Color);
            if (mat.Empty())
                return null;
            return BgraImageBuffer.FromBgrMat(mat);
        }
        catch
        {
            return null;
        }
    }
}
