using FluentAssertions;
using RobotVision.Hosting;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// 「取图后自动熄灯」临时调试开关的真值表（<see cref="ServiceCollectionExtensions.ResolveKeepLightOn"/>）。
/// <para>
/// 默认关闭（Debug 与 Release 行为一致，均正常熄灯）；仅当环境变量 <c>ROBOTVISION_KEEP_LIGHT_ON=1</c>
/// 时屏蔽自动熄灯（现场排查光源/亮度用），置 0 或删除即恢复产线行为。
/// </para>
/// 环境变量两种构建都可覆盖，故这里是"覆盖逻辑"与"默认值"的分界验证。
/// </summary>
/// <remarks>
/// 标 <c>[Collection("Serial")]</c>：本用例写进程级环境变量，必须与同样建 DI 容器
/// （会经 RegisterLighting 读该变量）的用例互斥，沿用本仓 <c>ApplicationPathsTests</c> /
/// <c>CameraConfigStoreTests</c> 的同款约定。
/// </remarks>
[Collection("Serial")]
public sealed class LightingDebugSwitchTests : IDisposable
{
    private const string EnvName = "ROBOTVISION_KEEP_LIGHT_ON";

    /// <summary>环境变量是进程级共享状态，用例结束后必须还原，否则污染同进程其他测试。</summary>
    private readonly string? _original = Environment.GetEnvironmentVariable(EnvName);

    public void Dispose() => Environment.SetEnvironmentVariable(EnvName, _original);

    /// <summary>默认不屏蔽自动熄灯：Debug 与 Release 行为一致（产线行为），仅环境变量可临时开启。</summary>
    private const bool BuildDefault = false;

    [Fact]
    public void Unset_FallsBackToBuildDefault()
    {
        Environment.SetEnvironmentVariable(EnvName, null);

        ServiceCollectionExtensions.ResolveKeepLightOn().Should().Be(BuildDefault);
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("True", true)]
    [InlineData("0", false)]
    [InlineData("false", false)]
    // 空串在 Windows 上等同删除变量、纯空白视为未设置 —— 两者都回退构建默认
    [InlineData("", null)]
    [InlineData("   ", null)]
    // 只认 1/true；其他"看起来像真"的写法一律不算开，避免误开导致灯常亮
    [InlineData("yes", false)]
    [InlineData("on", false)]
    [InlineData("2", false)]
    public void EnvironmentVariable_OverridesBuildDefault(string raw, bool? expected)
    {
        Environment.SetEnvironmentVariable(EnvName, raw);

        ServiceCollectionExtensions.ResolveKeepLightOn().Should().Be(expected ?? BuildDefault);
    }
}
