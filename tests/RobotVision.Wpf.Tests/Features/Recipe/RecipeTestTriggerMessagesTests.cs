using FluentAssertions;
using RobotVision.Core.Models;
using RobotVision.WpfHost.Features.Recipe;

namespace RobotVision.Wpf.Tests.Features.Recipe;

public sealed class RecipeTestTriggerMessagesTests
{
    [Fact]
    public void FormatException_OperationCanceled_ShowsTimeoutNotGenericFault()
    {
        var msg = RecipeTestTriggerMessages.FormatException(new OperationCanceledException(), 30_000);
        msg.Should().StartWith("测试超时：");
        msg.Should().Contain("30");
        msg.Should().NotStartWith("测试异常");
    }

    [Fact]
    public void FormatException_TaskCanceled_ShowsTimeout()
    {
        var msg = RecipeTestTriggerMessages.FormatException(new TaskCanceledException(), 12_000);
        msg.Should().StartWith("测试超时：");
    }

    [Fact]
    public void FormatException_OtherException_ShowsFault()
    {
        var msg = RecipeTestTriggerMessages.FormatException(new InvalidOperationException("boom"), 30_000);
        msg.Should().Be("测试异常：boom");
    }

    [Fact]
    public void FormatPreviewResult_TimeoutCodes_UseTimeoutWording()
    {
        var timeout = VisionResult.Fail("r", VisionErrorCode.Timeout, "处理超时", 30_000);
        RecipeTestTriggerMessages.FormatPreviewResult(timeout, false)
            .Should().StartWith("测试超时：ERR 1008");

        var queue = VisionResult.Fail("r", VisionErrorCode.QueueTimeout, "排队超时", 0);
        RecipeTestTriggerMessages.FormatPreviewResult(queue, false)
            .Should().StartWith("测试超时：ERR 1010");
    }
}
