using Xunit.Sdk;

namespace RobotVision.IntegrationTests;

internal static class TestSkip
{
    public static void When(bool condition, string reason)
    {
        if (condition)
            Throw(reason);
    }

    public static void Unless(bool condition, string reason) => When(!condition, reason);

    public static void Throw(string reason) => throw Xunit.Sdk.SkipException.ForSkip(reason);
}
