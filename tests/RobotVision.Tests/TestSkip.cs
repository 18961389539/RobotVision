using Xunit.Sdk;

namespace RobotVision.Tests;

/// <summary>动态跳过（xUnit 2.9 无 Assert.SkipUnless，使用 SkipException）。</summary>
internal static class TestSkip
{
    public static void When(bool condition, string reason)
    {
        if (condition)
            Throw(reason);
    }

    public static void Unless(bool condition, string reason) => When(!condition, reason);

    public static void Throw(string reason) => Xunit.Skip.If(true, reason);
}
