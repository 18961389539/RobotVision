using RobotVision.Core.Models;
using RobotVision.Infrastructure.Communication;
using Xunit;

namespace RobotVision.Tests;

public class ProtocolTests
{
    [Fact]
    public void FormatReply_SuccessWithSinglePose()
    {
        var result = VisionResult.Success("A01",
            [new RobotPose(12.3456, -7.89, 15.25)], 0);
        Assert.Equal("OK,A01,1,12.346,-7.890,15.250,0", TcpServerManager.FormatReply(result));
    }

    [Fact]
    public void FormatReply_SuccessWithMultiplePoses()
    {
        var result = VisionResult.Success("A01",
        [
            new RobotPose(1, 2, 3),
            new RobotPose(4, 5, 6),
        ], 123.4);
        Assert.Equal("OK,A01,2,1.000,2.000,3.000,4.000,5.000,6.000,123", TcpServerManager.FormatReply(result));
    }

    [Fact]
    public void FormatReply_EmptyPoseList_HasCountZero()
    {
        // count 字段存在后，空目标列表的 OK 应答不再有歧义（当前 0 目标仍走 ERR 1007，
        // 此用例固定住"非破坏性扩展"的格式契约）
        var result = VisionResult.Success("A01", [], 5);
        Assert.Equal("OK,A01,0,5", TcpServerManager.FormatReply(result));
    }

    [Fact]
    public void FormatReply_FailureSanitizesCommasAndNewlines()
    {
        var result = VisionResult.Fail("A01", VisionErrorCode.NoTargetFound, "未检出,目标\n第二行", 5);
        Assert.Equal("ERR,1007,未检出 目标 第二行", TcpServerManager.FormatReply(result));
    }

    [Fact]
    public void FormatReply_TimeoutUsesErrorCode1008()
    {
        var result = VisionResult.Fail("A01", VisionErrorCode.Timeout, "处理超时", 5000);
        Assert.StartsWith("ERR,1008,", TcpServerManager.FormatReply(result));
    }

    // ---- 改进 3：错误消息契约 ----

    [Fact]
    public void FormatReply_InternalError_DoesNotLeakDetail()
    {
        // 异常原文（可能含路径/堆栈/中文）只进日志，协议线上固定 INTERNAL_ERROR
        var result = VisionResult.Fail("A01", VisionErrorCode.InternalError,
            "C:\\secret\\model.onnx 加载失败: 磁盘 IO 异常", 5);
        Assert.Equal("ERR,1099,INTERNAL_ERROR", TcpServerManager.FormatReply(result));
    }

    [Fact]
    public void FormatReply_BusinessError_KeepsSanitizedMessage()
    {
        // 业务错误（非 InternalError）保留可读消息，逗号/换行被消毒
        var result = VisionResult.Fail("A01", VisionErrorCode.NotCalibrated, "工位未标定,请先标定\nst1", 5);
        Assert.Equal("ERR,1004,工位未标定 请先标定 st1", TcpServerManager.FormatReply(result));
    }

    // ---- 改进 6：NaN/Infinity 防御 ----

    [Fact]
    public void FormatReply_NonFinitePose_ReturnsInvalidPoseError()
    {
        var result = VisionResult.Success("A01",
            [new RobotPose(double.NaN, 88.412, 45.123)], 5);
        Assert.Equal("ERR,1099,INVALID_POSE", TcpServerManager.FormatReply(result));
    }

    [Fact]
    public void FormatReply_InfinityPose_ReturnsInvalidPoseError()
    {
        var result = VisionResult.Success("A01",
            [new RobotPose(1, 2, 3), new RobotPose(4, double.PositiveInfinity, 6)], 5);
        Assert.Equal("ERR,1099,INVALID_POSE", TcpServerManager.FormatReply(result));
    }

    // ---- 改进 4：STATUS 命令 ----

    [Fact]
    public void FormatStatus_NullProvider_ReturnsReadyDefaults()
    {
        Assert.Equal("OK,ready,0,0,0", TcpServerManager.FormatStatus(null));
    }

    [Theory]
    [InlineData(true, 0, 4, 122, "OK,ready,0,4,122")]
    [InlineData(false, 3, 4, 1009, "OK,busy,3,4,1009")]
    public void FormatStatus_ReflectsPipelineState(bool ready, int queue, int max, double lastMs, string expected)
    {
        var state = new TcpServerManager.TcpServerState(ready, queue, max, lastMs);
        Assert.Equal(expected, TcpServerManager.FormatStatus(state));
    }

    // ---- TRIGGER 扩展格式（TRIGGER,配方名,X,Y,RZ）----

    [Fact]
    public void ParseTriggerArgument_LegacyOneSegment_PosesNull()
    {
        var (name, pose, error) = TcpServerManager.ParseTriggerArgument("A01");
        Assert.Equal("A01", name);
        Assert.Null(pose); // 旧格式：无位姿（OnArm 已记录示教位姿时由管线返回 1014）
        Assert.Null(error);
    }

    [Fact]
    public void ParseTriggerArgument_FourSegments_PosesParsed()
    {
        var (name, pose, error) = TcpServerManager.ParseTriggerArgument("A01, 100.25 , -50.5, 45.0");
        Assert.Equal("A01", name);
        Assert.Null(error);
        Assert.NotNull(pose);
        Assert.Equal(100.25, pose!.X, 9);
        Assert.Equal(-50.5, pose.Y, 9);
        Assert.Equal(45.0, pose.RzDeg, 9);
    }

    [Theory]
    [InlineData("A01,1.0")]                 // 2 段
    [InlineData("A01,1.0,2.0")]             // 3 段
    [InlineData("A01,1.0,2.0,3.0,4.0")]     // 5 段
    public void ParseTriggerArgument_WrongSegmentCount_ReturnsError(string argument)
    {
        var (_, _, error) = TcpServerManager.ParseTriggerArgument(argument);
        Assert.Equal("TRIGGER_ARGUMENT_COUNT", error);
    }

    [Theory]
    [InlineData("A01,NaN,2.0,3.0")]
    [InlineData("A01,1.0,Infinity,3.0")]
    [InlineData("A01,1.0,2.0,abc")]
    public void ParseTriggerArgument_NonFiniteOrNonNumeric_ReturnsError(string argument)
    {
        var (_, _, error) = TcpServerManager.ParseTriggerArgument(argument);
        Assert.Equal("INVALID_POSE_NUMBER", error);
    }

    [Fact]
    public void ParseTriggerArgument_EmptyRecipeInFourSegments_ReturnsError()
    {
        var (_, _, error) = TcpServerManager.ParseTriggerArgument(",1.0,2.0,3.0");
        Assert.Equal("MISSING_RECIPE", error);
    }

    [Fact]
    public void FormatReply_PoseMismatchUses1012()
    {
        var result = VisionResult.Fail("A01", VisionErrorCode.PoseMismatch,
            "拍照位姿不一致: 上报与标定偏差超容差", 5);
        Assert.StartsWith("ERR,1012,", TcpServerManager.FormatReply(result));
    }

    [Fact]
    public void FormatReply_InvalidTriggerArgumentUses1013()
    {
        var result = VisionResult.Fail("A01", VisionErrorCode.InvalidTriggerArgument,
            "TRIGGER_ARGUMENT_COUNT", 0);
        Assert.Equal("ERR,1013,TRIGGER_ARGUMENT_COUNT", TcpServerManager.FormatReply(result));
    }

    [Fact]
    public void FormatReply_PoseRequiredUses1014()
    {
        var result = VisionResult.Fail("A01", VisionErrorCode.PoseRequired,
            "OnArm 工位必须使用 TRIGGER,配方名,X,Y,RZ", 0);
        Assert.StartsWith("ERR,1014,", TcpServerManager.FormatReply(result));
    }
}
