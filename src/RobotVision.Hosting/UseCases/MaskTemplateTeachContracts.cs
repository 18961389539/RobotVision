using RobotVision.Core.Recipe;

namespace RobotVision.Hosting;

public interface IMaskTemplateTeachService
{
    BgraImageBuffer? TryDecodePreview(string templatePngBase64);

    string GetTeachDiagnostics(string templatePngBase64, SegmentRefineMethod method);
}
