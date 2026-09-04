using FluentAssertions;
using RobotVision.Core.Models;
using RobotVision.WpfHost.Features.Recipe;

namespace RobotVision.Wpf.Tests.Features.Recipe;

public sealed class RecipeTestNextStepTests
{
    [Fact]
    public void Badge_Null_IsEmpty()
    {
        RecipeTestNextStep.Badge(null).Should().BeEmpty();
    }

    [Fact]
    public void Badge_Ok_IncludesPoseCount()
    {
        var ok = VisionResult.Success("r", [new RobotPose(1, 2, 0)], 12);
        RecipeTestNextStep.Badge(ok).Should().Be("OK · 1 件");
    }

    [Fact]
    public void Badge_Fail_ShowsErrorCode()
    {
        var fail = VisionResult.Fail("r", VisionErrorCode.RefineFailed, "精修失败", 8);
        RecipeTestNextStep.Badge(fail).Should().Be("ERR 1019");
    }

    [Fact]
    public void For_OkUnsaved_RemindsSave()
    {
        var ok = VisionResult.Success("r", [], 1);
        RecipeTestNextStep.For(ok, null, unsaved: true)
            .Should().Contain("保存后才上产线");
        RecipeTestNextStep.For(ok, null, unsaved: false).Should().BeEmpty();
    }

    [Fact]
    public void For_RefineFailed_SuggestsThresholdOrTeach()
    {
        var fail = VisionResult.Fail("r", VisionErrorCode.RefineFailed, "精修失败", 8);
        RecipeTestNextStep.For(fail, "第二峰过高", unsaved: false)
            .Should().Contain("第二峰过高")
            .And.Contain("匹配阈值")
            .And.Contain("角度范围");
    }

    [Fact]
    public void For_NoTarget_SuggestsRoi()
    {
        var fail = VisionResult.Fail("r", VisionErrorCode.NoTargetFound, "未检出", 8);
        RecipeTestNextStep.For(fail, null, unsaved: false)
            .Should().Contain("检测区域");
    }
}
