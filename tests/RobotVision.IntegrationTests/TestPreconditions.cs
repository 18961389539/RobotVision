namespace RobotVision.IntegrationTests;

internal static class TestPreconditions
{
    public const string UiTestEnvVar = "RV_UI_TEST";

    public static bool IsUiAutomationEnabled() =>
        string.Equals(Environment.GetEnvironmentVariable(UiTestEnvVar), "1", StringComparison.OrdinalIgnoreCase);

    public static void RequireUiAutomation() =>
        TestSkip.Unless(IsUiAutomationEnabled(), $"Set {UiTestEnvVar}=1 to run UI automation tests.");

    public static void RequireFile(string path, string reason) =>
        TestSkip.When(!File.Exists(path), reason);
}
