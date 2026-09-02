using FluentAssertions;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Vision.Inference.Strategies;
using RobotVision.Teach;
using RobotVision.WpfHost.Features.Recipe;

namespace RobotVision.Wpf.Tests.Features.Recipe;

public sealed class RecipeTestSessionAdviceTests
{
    [Fact]
    public void Reminder_DoesNotTreatExpectedCountZeroAsDifference()
    {
        var editor = MaskTemplateEditor();
        editor.Template.ExpectedCount = 0;
        var advice = Advice(SegmentRefineMethod.Template) with { TeachAreaPx = 1200 };

        RecipeTestSession.ShouldShowRefineAdviceReminder(advice, editor).Should().BeFalse();
    }

    [Fact]
    public void Reminder_DoesNotPushDifferentRefineMethod()
    {
        var editor = MaskTemplateEditor();
        var advice = Advice(SegmentRefineMethod.CaliperTab);

        RecipeTestSession.ShouldShowRefineAdviceReminder(advice, editor).Should().BeFalse();
    }

    [Fact]
    public void Reminder_SameMethodThresholdDiff_IsTrue()
    {
        var editor = MaskTemplateEditor();
        editor.Template.MatchThreshold = 0.40;
        var advice = Advice(SegmentRefineMethod.Template) with { SuggestedMatchThreshold = 0.72 };

        RecipeTestSession.ShouldShowRefineAdviceReminder(advice, editor).Should().BeTrue();
    }

    private static RecipeConfig MaskTemplateEditor() => new()
    {
        Name = "P",
        CameraId = "cam",
        AngleMode = AngleMode.MaskTemplate,
        Models = ["m.onnx"],
        Template =
        {
            RefineMethod = SegmentRefineMethod.Template,
            MatchThreshold = 0.60,
        },
    };

    private static SegmentRefineAdvice Advice(SegmentRefineMethod recommended) => new(
        Recommended: recommended,
        RecommendEdgeMatch: false,
        CanResolveOrientation: true,
        Aspect: 2,
        TextureEntropy: 1,
        Separability: 0.4,
        HoleAreaPx: 0,
        ProtrusionPx: 8,
        Summary: "test");
}
