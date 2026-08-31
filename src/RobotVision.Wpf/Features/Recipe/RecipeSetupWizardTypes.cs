using RobotVision.Core.Recipe;
using RobotVision.Teach;

namespace RobotVision.WpfHost.Features.Recipe;

internal enum SetupWizardStep
{
    Welcome = 0,
    Task = 1,
    CaptureRoi = 2,
    Analyze = 3,
    Result = 4,
    TeachVerify = 5,
}

internal sealed record BakeOffRow(
    SegmentRefineMethod MethodId, string Method, string Score, string Note, bool Ok, bool Eligible);

internal sealed record FeatureRoiRow(string Size, string Gap, bool Best);

internal sealed record ParamTuneRow(string Label, string Score, string Note, bool Best);

internal sealed record WizardAltRow(PlaybookCandidate Candidate, string Title, string Why, bool Selected);

internal sealed record WizardNavItem(SetupWizardStep Step, int Number, string Label, bool IsCurrent);
