using System.Security.Cryptography;
using System.Text;

namespace RobotVision.Core.Assets;

/// <summary>SHA-256 十六进制（小写、无连字符），供模型文件与标定档案钉扎比对。</summary>
public static class FileSha256
{
    public static string ComputeFile(string path)
    {
        // ReadWrite：推理引擎可能正打开同一 ONNX；Delete：允许替换中的文件仍可读完。
        using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return ToHex(SHA256.HashData(stream));
    }

    public static string ComputeUtf8(string text) =>
        ToHex(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    public static string Normalize(string? hex) =>
        (hex ?? "").Trim().Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();

    public static bool EqualsHex(string? a, string? b) =>
        Normalize(a).Length > 0 &&
        string.Equals(Normalize(a), Normalize(b), StringComparison.Ordinal);

    public static bool IsHex(string? hex)
    {
        var n = Normalize(hex);
        return n.Length == 64 && n.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    }

    private static string ToHex(byte[] hash) => Convert.ToHexString(hash).ToLowerInvariant();
}
