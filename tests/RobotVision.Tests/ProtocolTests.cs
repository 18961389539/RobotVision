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
        Assert.Equal("OK,12.346,-7.890,15.250,A01,1,0", TcpServerManager.FormatReply(result));
    }

    [Fact]
    public void FormatReply_SuccessWithMultiplePoses()
    {
        var result = VisionResult.Success("A01",
        [
            new RobotPose(1, 2, 3),
            new RobotPose(4, 5, 6),
        ], 123.4);
        Assert.Equal("OK,1.000,2.000,3.000,4.000,5.000,6.000,A01,2,123", TcpServerManager.FormatReply(result));
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
        var result = VisionResult.Fail("A01", VisionErrorCode.NoTargetFound, "no target,second\nline", 5);
        Assert.Equal("ERR,1007,no target second line", TcpServerManager.FormatReply(result));
    }

    [Fact]
    public void FormatReply_StripsNonAsciiFromBusinessMessage()
    {
        var result = VisionResult.Fail("A01", VisionErrorCode.NoTargetFound, "未检出,目标\n第二行", 5);
        Assert.Equal("ERR,1007,  ", TcpServerManager.FormatReply(result));
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
        // 业务错误（非 InternalError）保留可读 ASCII 消息，逗号/换行被消毒，非 ASCII 被剥离
        var result = VisionResult.Fail("A01", VisionErrorCode.NotCalibrated, "station not calibrated,run first\nst1", 5);
        Assert.Equal("ERR,1004,station not calibrated run first st1", TcpServerManager.FormatReply(result));
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
    [InlineData(true, 0, 4, 122, "OK,ready,0,4,122,0,0")]
    [InlineData(false, 3, 4, 1009, "OK,busy,3,4,1009,0,0")]
    public void FormatStatus_ReflectsPipelineState(bool ready, int queue, int max, double lastMs, string expected)
    {
        var state = new TcpServerManager.TcpServerState(ready, queue, max, lastMs);
        Assert.Equal(expected, TcpServerManager.FormatStatus(state));
    }

    [Fact]
    public void FormatStatus_AppendsHealthFields()
    {
        var state = new TcpServerManager.TcpServerState(true, 0, 4, 80, ConsecutiveFails: 5, Inhibited: 1);
        Assert.Equal("OK,ready,0,4,80,5,1", TcpServerManager.FormatStatus(state));
    }

    [Fact]
    public void ParseClearInhibitRecipe_AllOrNamed()
    {
        Assert.Null(TcpServerManager.ParseClearInhibitRecipe("CLEARINHIBIT"));
        Assert.Equal("A01", TcpServerManager.ParseClearInhibitRecipe("CLEARINHIBIT,A01"));
        Assert.Equal("A01", TcpServerManager.ParseClearInhibitRecipe("clearinhibit, A01 ,extra"));
    }

    // ---- 触发行格式（配方名 / 序列号,X,Y,RZ）----

    [Fact]
    public void ParseTriggerLine_LegacyOneSegment_PosesNull()
    {
        var (key, pose, error) = TcpServerManager.ParseTriggerLine("A01");
        Assert.Equal("A01", key);
        Assert.Null(pose);
        Assert.Null(error);
    }

    [Fact]
    public void ParseTriggerLine_SerialWithHashPrefix()
    {
        var (key, pose, error) = TcpServerManager.ParseTriggerLine("#3");
        Assert.Equal("3", key);
        Assert.Null(pose);
        Assert.Null(error);
    }

    [Fact]
    public void ParseTriggerLine_FourSegments_PosesParsed()
    {
        var (key, pose, error) = TcpServerManager.ParseTriggerLine("A01, 100.25 , -50.5, 45.0");
        Assert.Equal("A01", key);
        Assert.Null(error);
        Assert.NotNull(pose);
        Assert.Equal(100.25, pose!.X, 9);
        Assert.Equal(-50.5, pose.Y, 9);
        Assert.Equal(45.0, pose.RzDeg, 9);
    }

    [Theory]
    [InlineData("A01,1.0")]
    [InlineData("A01,1.0,2.0")]
    [InlineData("A01,1.0,2.0,3.0,4.0")]
    public void ParseTriggerLine_WrongSegmentCount_ReturnsError(string line)
    {
        var (_, _, error) = TcpServerManager.ParseTriggerLine(line);
        Assert.Equal("TRIGGER_ARGUMENT_COUNT", error);
    }

    [Theory]
    [InlineData("A01,NaN,2.0,3.0")]
    [InlineData("A01,1.0,Infinity,3.0")]
    [InlineData("A01,1.0,2.0,abc")]
    public void ParseTriggerLine_NonFiniteOrNonNumeric_ReturnsError(string line)
    {
        var (_, _, error) = TcpServerManager.ParseTriggerLine(line);
        Assert.Equal("INVALID_POSE_NUMBER", error);
    }

    [Fact]
    public void ParseTriggerLine_EmptyRecipeInFourSegments_ReturnsError()
    {
        var (_, _, error) = TcpServerManager.ParseTriggerLine(",1.0,2.0,3.0");
        Assert.Equal("MISSING_RECIPE", error);
    }

    [Fact]
    public void ParseTriggerLine_EmptyLine_ReturnsMissingRecipe()
    {
        var (_, _, error) = TcpServerManager.ParseTriggerLine("");
        Assert.Equal("MISSING_RECIPE", error);
    }

    [Fact]
    public void ParseTriggerLine_LegacyTriggerPrefix_ReturnsSegmentError()
    {
        var (_, _, error) = TcpServerManager.ParseTriggerLine("TRIGGER,A01");
        Assert.Equal("TRIGGER_ARGUMENT_COUNT", error);
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
