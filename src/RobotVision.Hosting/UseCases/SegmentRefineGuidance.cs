using RobotVision.Core.Recipe;
using RobotVision.Teach;

namespace RobotVision.Hosting;

public interface ISegmentRefineGuidance
{
    string FormatBriefAdvice(SegmentRefineAdvice advice, SegmentRefineMethod currentMethod);

    string FormatMethodScoreHint(SegmentRefineAdvice advice, SegmentRefineMethod currentMethod);

    string MethodLabel(SegmentRefineMethod method);
}

internal sealed class SegmentRefineGuidance : ISegmentRefineGuidance
{
    public string FormatBriefAdvice(SegmentRefineAdvice advice, SegmentRefineMethod currentMethod) =>
        SegmentRefineAdvisor.FormatBriefAdvice(advice, currentMethod);

    public string FormatMethodScoreHint(SegmentRefineAdvice advice, SegmentRefineMethod currentMethod) =>
        SegmentRefineAdvisor.FormatMethodScoreHint(advice, currentMethod);

    public string MethodLabel(SegmentRefineMethod method) => SegmentRefineAdvisor.MethodLabel(method);
}
