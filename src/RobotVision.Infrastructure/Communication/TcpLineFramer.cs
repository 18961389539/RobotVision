using System.Text;

namespace RobotVision.Infrastructure.Communication;

/// <summary>
/// TCP 请求分帧：有 <c>\n</c> / <c>\r\n</c> / <c>\r</c> 立即切帧；
/// 无结束符时由调用方在静默 <see cref="QuietMs"/> 后把缓冲区当作完整命令。
/// </summary>
public static class TcpLineFramer
{
    /// <summary>无换行时，收到最后一字节后再等这么久才提交命令。</summary>
    public const int QuietMs = 100;

    /// <summary><see cref="System.Net.Sockets.Socket.Poll"/> 的超时（微秒）。</summary>
    public const int QuietTimeoutMicroseconds = QuietMs * 1000;

    /// <summary>单帧上限；超长且无换行时强制切帧，避免恶意连接占满内存。</summary>
    public const int MaxFrameChars = 2048;

    /// <summary>
    /// 把新到的字节追加进 <paramref name="pending"/>，按 CR/LF 切出完整行写入 <paramref name="complete"/>。
    /// 空行也会进入 <paramref name="complete"/>（调用方 Trim 后跳过）。
    /// </summary>
    public static void Append(StringBuilder pending, ReadOnlySpan<byte> data, List<string> complete)
    {
        ArgumentNullException.ThrowIfNull(pending);
        ArgumentNullException.ThrowIfNull(complete);

        for (var i = 0; i < data.Length; i++)
        {
            var b = data[i];
            if (b is (byte)'\n' or (byte)'\r')
            {
                complete.Add(pending.ToString());
                pending.Clear();
                if (b == (byte)'\r' && i + 1 < data.Length && data[i + 1] == (byte)'\n')
                    i++;
                continue;
            }

            if (pending.Length >= MaxFrameChars)
            {
                complete.Add(pending.ToString());
                pending.Clear();
            }

            pending.Append((char)b);
        }
    }
}
