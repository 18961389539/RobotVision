using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace RobotVision.Infrastructure.Lighting;

/// <summary>
/// 东冠（OSE PWD 系列）多通道数字电源 RS232 协议。
/// 波特率 9600、8N1；直通线 2-2、3-3、5-5；控制器应答回显所发命令。
/// 帧长 5 字节：<c>00 | (通道号&lt;&lt;4 | 功能) | 数据… | FF…</c>。
/// 通道 1–9；功能 1 查亮度 / 2 设亮度（0x01–0x64 = 1%–100%）/
/// 3 查频闪状态 / 4 设频闪状态 / 5 查脉宽 / 6 设脉宽（0.1ms）。
/// <para>
/// ⚠️ 频闪状态字节按《多通道数字电源通讯协议--东冠协议》附一定义：
/// <b>0x55 = 频闪打开（面板红灯 FLA，需外触发才亮）、0xAA = 频闪关闭（面板红灯 CON，常亮）</b>。
/// 两者语义与直觉相反——发 0x55 会让通道进入频闪，无 Trigger 信号时看起来像没点亮。
/// </para>
/// </summary>
public static class DongguanLightProtocol
{
    public const byte Header = 0x00;
    public const byte Pad = 0xFF;
    public const int FrameLength = 5;
    public const int MinChannel = 1;
    public const int MaxChannel = 9;
    public const int DefaultChannelCount = 4;
    public const int MaxPercent = 100;

    public const int FnQueryBrightness = 1;
    public const int FnSetBrightness = 2;
    public const int FnQueryStrobe = 3;
    public const int FnSetStrobe = 4;
    public const int FnQueryPulse = 5;
    public const int FnSetPulse = 6;

    /// <summary>频闪打开（面板 FLA，需外触发）。协议原文：0x55 说明频闪打开。</summary>
    public const byte StrobeEnabled = 0x55;

    /// <summary>频闪关闭 = 常亮（面板 CON，恒流连续输出）。协议原文：0xAA 说明频闪关闭。</summary>
    public const byte StrobeDisabled = 0xAA;

    /// <summary>配方亮度 0–255 映射到控制器 0–100%。</summary>
    public static byte ToPercent(int brightness255)
    {
        if (brightness255 <= 0)
            return 0;
        if (brightness255 >= 255)
            return MaxPercent;
        return (byte)Math.Clamp((int)Math.Round(brightness255 * 100.0 / 255.0), 1, MaxPercent);
    }

    public static byte EncodeCommand(int channel, int function)
    {
        var ch = Math.Clamp(channel, MinChannel, MaxChannel);
        return (byte)((ch << 4) | (function & 0x0F));
    }

    /// <summary>单字节数据帧（亮度、频闪状态）：<c>00 CMD DATA FF FF</c>。</summary>
    public static byte[] BuildDataFrame(int channel, int function, byte data) =>
        [Header, EncodeCommand(channel, function), data, Pad, Pad];

    /// <summary>查询帧：数据位填 0xFF。</summary>
    public static byte[] BuildQueryFrame(int channel, int function) =>
        [Header, EncodeCommand(channel, function), Pad, Pad, Pad];

    /// <summary>双字节数据帧（脉宽 HH LL）：<c>00 CMD HH LL FF</c>。</summary>
    public static byte[] BuildWordFrame(int channel, int function, ushort value) =>
        [Header, EncodeCommand(channel, function), (byte)(value >> 8), (byte)(value & 0xFF), Pad];

    public static byte[] BuildSetBrightness(int channel, int brightness255) =>
        BuildDataFrame(channel, FnSetBrightness, ToPercent(brightness255));

    public static byte[] BuildSetPercent(int channel, int percent) =>
        BuildDataFrame(channel, FnSetBrightness, (byte)Math.Clamp(percent, 0, MaxPercent));

    /// <summary>
    /// 关闭频闪、切常亮：<c>00 (通道&lt;&lt;4|4) AA FF FF</c>。
    /// 常亮必须先发本帧——否则通道可能停留在频闪态，只发亮度帧不会亮。
    /// </summary>
    public static byte[] BuildSetConstantOn(int channel) =>
        BuildDataFrame(channel, FnSetStrobe, StrobeDisabled);

    /// <summary>打开频闪（外触发）：<c>00 (通道&lt;&lt;4|4) 55 FF FF</c>。</summary>
    public static byte[] BuildSetFlash(int channel) =>
        BuildDataFrame(channel, FnSetStrobe, StrobeEnabled);

    /// <summary>脉宽单位 0.1ms，取值 0–999（0–99.9ms）。</summary>
    public static byte[] BuildSetPulseTenths(int channel, int tenths) =>
        BuildWordFrame(channel, FnSetPulse, (ushort)Math.Clamp(tenths, 0, 999));

    /// <summary>
    /// 解析协议调试输入为裸字节。接受空格/逗号/短横分隔，或连续十六进制（可带 0x 前缀）。
    /// 无法解析时返回 false，调用方按 ASCII 发送。
    /// </summary>
    public static bool TryParseHexBytes(string command, [NotNullWhen(true)] out byte[]? bytes)
    {
        bytes = null;
        if (string.IsNullOrWhiteSpace(command))
            return false;

        var tokens = command
            .Split([' ', '\t', ',', ';', '-', ':'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
            return false;

        if (tokens.Length == 1)
            return TryParsePackedHex(tokens[0], out bytes);

        var parsed = new byte[tokens.Length];
        for (var i = 0; i < tokens.Length; i++)
        {
            if (!TryParseByteToken(tokens[i], out parsed[i]))
                return false;
        }

        bytes = parsed;
        return true;
    }

    private static bool TryParsePackedHex(string token, [NotNullWhen(true)] out byte[]? bytes)
    {
        bytes = null;
        var hex = StripHexPrefix(token);
        if (hex.Length < 2 || (hex.Length & 1) != 0)
            return false;
        foreach (var c in hex)
        {
            if (!IsHexChar(c))
                return false;
        }

        var n = hex.Length / 2;
        var parsed = new byte[n];
        for (var i = 0; i < n; i++)
        {
            if (!byte.TryParse(hex.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed[i]))
                return false;
        }

        bytes = parsed;
        return true;
    }

    private static bool TryParseByteToken(string token, out byte value)
    {
        var hex = StripHexPrefix(token);
        return byte.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)
               && hex.Length is 1 or 2
               && hex.All(IsHexChar);
    }

    private static string StripHexPrefix(string token) =>
        token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? token[2..] : token;

    private static bool IsHexChar(char c) =>
        c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
}
