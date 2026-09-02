namespace RobotVision.Tests;

/// <summary>统一测试前置条件：不满足时显式 Skip，避免静默 return 假绿。</summary>
internal static class TestPreconditions
{
    public const string HardwareEnvVar = "RV_HARDWARE_TEST";

    public static void RequireHardware() =>
        TestSkip.Unless(
            string.Equals(Environment.GetEnvironmentVariable(HardwareEnvVar), "1",
                StringComparison.OrdinalIgnoreCase),
            $"Set {HardwareEnvVar}=1 to run hardware tests.");

    public static void RequireOnnx(string? path) =>
        TestSkip.When(string.IsNullOrEmpty(path) || !RepoAssets.IsUsable(path),
            "No usable ONNX model found under repo models/.");

    public static void RequireFile(string path, string reason) =>
        TestSkip.When(!File.Exists(path), reason);

    public static void RequireDirectory(string path, string reason) =>
        TestSkip.When(!Directory.Exists(path), reason);
}
