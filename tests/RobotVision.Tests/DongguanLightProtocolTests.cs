using RobotVision.Hosting;
using RobotVision.Hosting.Lighting;
using RobotVision.Infrastructure.Lighting;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// 东冠数字电源 RS232 帧格式：与《多通道数字电源通讯协议--东冠协议》示例一致。
/// </summary>
public class DongguanLightProtocolTests
{
    [Fact]
    public void BuildSetBrightness_Channel1_FullScale_MatchesDocExamplePattern()
    {
        var frame = DongguanLightProtocol.BuildSetBrightness(1, 255);
        Assert.Equal([0x00, 0x12, 0x64, 0xFF, 0xFF], frame);
    }

    [Fact]
    public void BuildSetBrightness_Channel2_FullScale_MatchesDocExample()
    {
        var frame = DongguanLightProtocol.BuildSetBrightness(2, 255);
        Assert.Equal([0x00, 0x22, 0x64, 0xFF, 0xFF], frame);
    }

    [Fact]
    public void BuildSetPercent_Channel1_Off()
    {
        Assert.Equal([0x00, 0x12, 0x00, 0xFF, 0xFF], DongguanLightProtocol.BuildSetPercent(1, 0));
    }

    /// <summary>
    /// 常亮 = 频闪关闭 = 0xAA（协议原文「0xXX = 0x55 说明频闪打开；0xXX = 0xAA 说明频闪关闭」）。
    /// 曾经写成 0x55，导致「开灯」把通道切进频闪、无外触发时灯不亮（能关不能开）。
    /// </summary>
    [Fact]
    public void BuildSetConstantOn_Uses0xAA_StrobeOff()
    {
        Assert.Equal([0x00, 0x14, 0xAA, 0xFF, 0xFF], DongguanLightProtocol.BuildSetConstantOn(1));
        Assert.Equal([0x00, 0x24, 0xAA, 0xFF, 0xFF], DongguanLightProtocol.BuildSetConstantOn(2));
    }

    [Fact]
    public void BuildSetFlash_Uses0x55_StrobeOn()
    {
        Assert.Equal([0x00, 0x14, 0x55, 0xFF, 0xFF], DongguanLightProtocol.BuildSetFlash(1));
        Assert.Equal([0x00, 0x24, 0x55, 0xFF, 0xFF], DongguanLightProtocol.BuildSetFlash(2));
    }

    /// <summary>频闪状态字节语义防呆：0x55/0xAA 与协议文档一致，不得互换。</summary>
    [Fact]
    public void StrobeBytes_MatchProtocolDocument()
    {
        Assert.Equal(0x55, DongguanLightProtocol.StrobeEnabled);
        Assert.Equal(0xAA, DongguanLightProtocol.StrobeDisabled);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(128, 50)]
    [InlineData(255, 100)]
    public void ToPercent_MapsRecipeScale(int brightness, int percent) =>
        Assert.Equal(percent, DongguanLightProtocol.ToPercent(brightness));

    [Fact]
    public void EncodeCommand_Channels1To4()
    {
        Assert.Equal(0x12, DongguanLightProtocol.EncodeCommand(1, DongguanLightProtocol.FnSetBrightness));
        Assert.Equal(0x22, DongguanLightProtocol.EncodeCommand(2, DongguanLightProtocol.FnSetBrightness));
        Assert.Equal(0x32, DongguanLightProtocol.EncodeCommand(3, DongguanLightProtocol.FnSetBrightness));
        Assert.Equal(0x42, DongguanLightProtocol.EncodeCommand(4, DongguanLightProtocol.FnSetBrightness));
    }

    [Fact]
    public void BuildQueryBrightness_FillsDataWithPad()
    {
        Assert.Equal(
            [0x00, 0x11, 0xFF, 0xFF, 0xFF],
            DongguanLightProtocol.BuildQueryFrame(1, DongguanLightProtocol.FnQueryBrightness));
        Assert.Equal(
            [0x00, 0x21, 0xFF, 0xFF, 0xFF],
            DongguanLightProtocol.BuildQueryFrame(2, DongguanLightProtocol.FnQueryBrightness));
    }

    [Fact]
    public void BuildSetPulse_10ms_Is100Tenths()
    {
        Assert.Equal(
            [0x00, 0x16, 0x00, 0x64, 0xFF],
            DongguanLightProtocol.BuildSetPulseTenths(1, 100));
    }

    [Fact]
    public void TryParseHexBytes_AcceptsSpacedAndPacked()
    {
        Assert.True(DongguanLightProtocol.TryParseHexBytes("00 12 64 FF FF", out var spaced));
        Assert.Equal([0x00, 0x12, 0x64, 0xFF, 0xFF], spaced);

        Assert.True(DongguanLightProtocol.TryParseHexBytes("0x00 0x22 0x64 0xFF 0xFF", out var prefixed));
        Assert.Equal([0x00, 0x22, 0x64, 0xFF, 0xFF], prefixed);

        Assert.True(DongguanLightProtocol.TryParseHexBytes("001264FFFF", out var packed));
        Assert.Equal([0x00, 0x12, 0x64, 0xFF, 0xFF], packed);
    }

    [Fact]
    public void TryParseHexBytes_RejectsAsciiCommands()
    {
        Assert.False(DongguanLightProtocol.TryParseHexBytes("SET 1 128", out _));
        Assert.False(DongguanLightProtocol.TryParseHexBytes("OFF ALL", out _));
    }

    [Fact]
    public void EncodeRaw_PrefersHexThenAscii()
    {
        Assert.Equal([0x00, 0x12, 0x64, 0xFF, 0xFF], SerialLightController.EncodeRaw("00 12 64 FF FF"));
        Assert.Equal("SET 1 128\r\n"u8.ToArray(), SerialLightController.EncodeRaw("SET 1 128\\r\\n"));
    }

    /// <summary>
    /// 帧间隔下限护栏：低于实测阈值时控制器会**静默吃掉整帧**（不报错），
    /// 表现为"UI 说开灯成功、灯却不亮""通道 2 关不掉"。
    /// 2026-09-14 本机实测（同一条 4 帧序列各 3 次）：10ms→0/3、20ms→0/3、30/40/50/60/80ms→各 3/3。
    /// **别为了省时间把 FrameGapMs 调小**。
    /// </summary>
    [Fact]
    public void FrameGapMs_KeepsMarginOverMeasuredControllerThreshold()
    {
        Assert.True(
            SerialLightController.FrameGapMs >= SerialLightController.FrameGapMsMinimum,
            $"FrameGapMs={SerialLightController.FrameGapMs}ms 低于东冠电源实测阈值"
            + $"（{SerialLightController.FrameGapMsMinimum}ms），帧会被控制器静默丢弃");
    }

    /// <summary>
    /// 跨命令的帧间隔护栏：写入前必须与上一帧至少间隔 FrameGapMs。
    /// 控制器是缓慢的字节流解析器，两帧紧贴会被当一帧丢弃 —— 2026-09-14 硬件实测：
    /// 「关灯」最后一帧（通道 2 亮度 0）紧接着「开灯」第一帧时被吃掉，表现为"通道 2 关不掉"。
    /// </summary>
    [Theory]
    [InlineData(0, 1000, 20, 0)]
    [InlineData(1000, 1000, 20, 20)]
    [InlineData(1000, 1005, 20, 15)]
    [InlineData(1000, 1019, 20, 1)]
    [InlineData(1000, 1020, 20, 0)]
    [InlineData(1000, 1100, 20, 0)]
    public void FrameGapRemainingMs_EnforcesGapEvenAcrossCommands(
        long lastWriteTicks, long nowTicks, int gapMs, int expected) =>
        Assert.Equal(expected, SerialLightController.FrameGapRemainingMs(lastWriteTicks, nowTicks, gapMs));

    /// <summary>
    /// 串口打不开时必须返回 false **并留下失败原因**（ILightDiagnostics）。
    /// 只返回 false 时上层只能报「指令发送失败」，现场分不清是没插线、被占用还是协议不对。
    /// </summary>
    [Fact]
    public void ApplyAndTurnOff_PortUnavailable_ReturnFalse_AndRecordTransportError()
    {
        using var light = new SerialLightController("dg", "COM999", 9600, timeoutMs: 100, channelCount: 2);
        Assert.Null(light.LastTransportError);

        var ok = light.Apply(new RobotVision.Core.Models.LightingConfig
        {
            Channels = [new RobotVision.Core.Models.LightingChannelConfig { Channel = 1, Brightness = 128 }],
        });

        Assert.False(ok);
        Assert.NotNull(light.LastTransportError);
        Assert.Contains("COM999", light.LastTransportError, StringComparison.Ordinal);

        Assert.False(light.TurnOff());
        Assert.NotNull(light.LastTransportError);
    }

    [Fact]
    public void Factory_CreatesSerialController_OnCom5()
    {
        var light = LightControllerTypeRegistry.Default.Create(new LightControllerConfig
        {
            Id = "dongguan",
            Type = "Serial",
            Port = "COM5",
            BaudRate = 9600,
        });

        var serial = Assert.IsType<SerialLightController>(light);
        Assert.Equal("dongguan", serial.Id);
        Assert.Equal(RobotVision.Core.Abstractions.LightControllerKind.Serial, serial.Kind);
        serial.Dispose();
    }

    [Fact]
    public void Factory_MissingPort_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            LightControllerTypeRegistry.Default.Create(new LightControllerConfig
            {
                Id = "dongguan",
                Type = "Serial",
            }));
    }
}
