using BenchmarkDotNet.Attributes;
using RobotVision.Core.Models;
using RobotVision.Infrastructure.Communication;

namespace RobotVision.Benchmarks;

[MemoryDiagnoser]
public class ProtocolBenchmarks
{
    private const string TriggerLine = "A01,123.456,789.012,-45.5";
    private const string TriggerKeyOnly = "A01";

    [Benchmark]
    public (string RecipeKey, TcpClientPose? Pose, string? FormatError) ParseTriggerLine_WithPose() =>
        TcpServerManager.ParseTriggerLine(TriggerLine);

    [Benchmark]
    public (string RecipeKey, TcpClientPose? Pose, string? FormatError) ParseTriggerLine_KeyOnly() =>
        TcpServerManager.ParseTriggerLine(TriggerKeyOnly);

    [Benchmark]
    public (string RecipeName, TcpClientPose? Pose, string? FormatError) ParseTriggerArgument() =>
        TcpServerManager.ParseTriggerArgument("A01,1,2,3");

    private static readonly VisionResult OkResult = VisionResult.Success(
        "A01", [new RobotPose(123.456, 789.012, 45.5), new RobotPose(12, 34, -90)], 12.3, [0.99, 0.85]);

    private static readonly VisionResult ErrResult = VisionResult.Fail(
        "A01", VisionErrorCode.NoTargetFound, "未检出目标", 5.6);

    [Benchmark]
    public string FormatReply_Ok() => TcpServerManager.FormatReply(OkResult);

    [Benchmark]
    public string FormatReply_Error() => TcpServerManager.FormatReply(ErrResult);

    [Benchmark]
    public string FormatStatus() => TcpServerManager.FormatStatus(
        new TcpServerManager.TcpServerState(true, 0, 4, 123.456));

    [Benchmark]
    public bool TryParseWhitelistEntry_Exact() => TcpServerManager.TryParseWhitelistEntry("192.168.1.10");

    [Benchmark]
    public bool TryParseWhitelistEntry_Wildcard() => TcpServerManager.TryParseWhitelistEntry("192.168.*");

    [Benchmark]
    public bool TryParseWhitelistEntry_Invalid() => TcpServerManager.TryParseWhitelistEntry("not-an-ip");
}
