using System.Globalization;
using OpenCvSharp;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Lighting;
using RobotVision.Vision;
using RobotVision.Teach;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// 第一节 5 个 bug 修复的针对性回归测试（集中在新文件，避免与并发会话共用的测试文件冲突）。
/// Bug4（DI 释放进程级静态）经核实为误报：AddSingleton(实例) 的重载不会被容器 Dispose，故无对应代码改动/测试。
/// </summary>
public sealed class BugFixRegressionTests
{
    // —— Bug1：串口打开失败必须立即释放刚创建的 SerialPort，反复发送不得泄漏、不得外抛 ——

    [Fact]
    public void SerialLightController_ValidatesConstructorArguments()
    {
        Assert.Throws<ArgumentException>(() => new SerialLightController("", "COM1", 9600));
        Assert.Throws<ArgumentException>(() => new SerialLightController("s", "  ", 9600));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SerialLightController("s", "COM1", 100));
    }

    [Fact]
    public void SerialLightController_RepeatedSendsOnMissingPort_DoNotThrowOrDrift()
    {
        // 未接硬件：Open() 每次抛异常 → 修复前会泄漏 SerialPort（COM/事件句柄），修复后即时释放。
        // 断言“可重复调用、无外抛、结果稳定”，锁定 EnsurePortOpen 的失败清理契约。
        using var controller = new SerialLightController("s", "COM999", 9600, 100);
        var lighting = new LightingConfig
        {
            Channels = [new LightingChannelConfig { Channel = 1, Brightness = 128 }],
        };

        bool? first = null;
        for (var i = 0; i < 30; i++)
        {
            var result0 = Record.Exception(() => { controller.Apply(lighting); });
            Assert.Null(result0); // 打开失败被吞并返回 false，绝不外抛

            var result = controller.Apply(lighting);
            first ??= result;
            Assert.Equal(first!.Value, result); // 反复调用结果稳定，无状态漂移
        }
    }

    // —— Bug2：凸起朝向门限应按真实壳体短边缩放，而非硬编码 Math.Max(8,1) ——

    [Fact]
    public void Describe_PopulatesShortLenPxForRealContour()
    {
        using var img = new Mat(360, 480, MatType.CV_8UC3, new Scalar(240, 240, 240));
        Cv2.Rectangle(img, new Point(130, 152), new Point(350, 208), new Scalar(80, 80, 80), -1);
        Cv2.Rectangle(img, new Point(220, 208), new Point(260, 226), new Scalar(30, 30, 30), -1);
        using var mask = new Mat(360, 480, MatType.CV_8UC1, Scalar.All(0));
        Cv2.Rectangle(mask, new Point(130, 152), new Point(350, 208), Scalar.All(255), -1);
        Cv2.Rectangle(mask, new Point(220, 208), new Point(260, 226), Scalar.All(255), -1);
        Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxNone);
        var contour = contours.OrderByDescending(c => Cv2.ContourArea(c)).First()
            .Select(p => new Point2f(p.X, p.Y)).ToArray();

        var scene = ScenePlaybook.Describe(img, contour);

        // 壳体高约 56px，短边应为真实件尺寸（远大于占位常量 8），供门限按比例缩放。
        Assert.True(double.IsFinite(scene.ShortLenPx) && scene.ShortLenPx > 8,
            $"真实轮廓应带正的壳体短边，实际 {scene.ShortLenPx}");
    }

    [Fact]
    public void Describe_DegenerateContour_LeavesShortLenPxZero()
    {
        using var img = new Mat(50, 50, MatType.CV_8UC3, new Scalar(10, 10, 10));
        Point2f[] contour = [new(1, 1), new(2, 2)]; // <3 点 → 退化路径

        var scene = ScenePlaybook.Describe(img, contour);

        Assert.Equal(0, scene.ShortLenPx); // 退化默认 0，Math.Max(8,0) 保持与旧版一致的 8px 下限
    }

    // —— Bug3：人话文案的小数分隔符必须与文化无关（逗号小数 locale 也不能变成 "0,80"） ——

    [Fact]
    public void Narration_UsesInvariantDecimalSeparator_UnderCommaCulture()
    {
        var prev = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE"); // 逗号小数分隔符

            var lineFit = TeachNarrator.LineFitOk(0.8);
            Assert.Contains("0.80", lineFit, StringComparison.Ordinal);
            Assert.DoesNotContain("0,80", lineFit, StringComparison.Ordinal);

            Assert.Contains("NCC 0.80", TeachNarrator.TemplatePeak(0.8, 0.5, true), StringComparison.Ordinal);
            Assert.Contains("0.85", TeachNarrator.CentroidHoleOk(0.85), StringComparison.Ordinal);
            Assert.Contains("匹配门 0.50→0.80",
                TeachNarrator.TunerThreshold(0.5, 0.8), StringComparison.Ordinal);

            // Advisor.FormatCandidateScore 的单帧分（被现有 Contains("0.19") 断言锁定）
            var advice = new SegmentRefineAdvice(
                SegmentRefineMethod.CaliperTab, false, true, 2.2, 0, 0.43, 0, 0, "x")
            {
                Candidates =
                [
                    new(SegmentRefineMethod.ShapeMatch, true, true, 0.19, "低分"),
                    new(SegmentRefineMethod.CaliperTab, true, true, 0.86, "高"),
                ],
            };
            Assert.Contains("0.19",
                SegmentRefineAdvisor.FormatMethodScoreHint(advice, SegmentRefineMethod.ShapeMatch),
                StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = prev;
        }
    }

    [Fact]
    public void Narration_OutputIsByteIdenticalToLegacyOnDotCulture()
    {
        var prev = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Assert.Equal("直线拟合过门（残差 0.80°）", TeachNarrator.LineFitOk(0.8));
            Assert.Equal("模板过门（NCC 0.80）", TeachNarrator.TemplatePeak(0.8, 0.5, true));
            Assert.Equal("匹配门 0.50→0.80", TeachNarrator.TunerThreshold(0.5, 0.8));
        }
        finally
        {
            CultureInfo.CurrentCulture = prev;
        }
    }
}
