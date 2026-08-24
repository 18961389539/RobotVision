using OpenCvSharp;
using RobotVision.Infrastructure.Inference.Strategies;
using Xunit;
using Xunit.Abstractions;

namespace RobotVision.Tests;

/// <summary>
/// 质心-内孔连线精修（CentroidHoleLine）单元测试：合成带孔位掩码
/// （YoloDotNet BitPackedPixelMask 格式：BoundingBox 尺寸、LSB-first 位打包），
/// 验证孔中心提取、连线角度（指向孔）、质心位置与兜底路径。
/// </summary>
public class CentroidHoleLineTests(ITestOutputHelper output)
{
    private const int W = 240;
    private const int H = 120;

    [Fact]
    public void HoleOffsetFromCentroid_RecoversDirection()
    {
        // 孔放在本体坐标 (-70, +25) 处（相对掩码中心）：连线角 = atan2(25, -70) ≈ 160.3°
        var (mask, width, height) = BuildMaskWithHole(holeDx: -70, holeDy: 25, holeR: 12);
        var result = MaskTemplateMatcher.RefineByCentroidHoleLine(mask, width, height);

        Assert.NotNull(result);
        var expected = Norm(Math.Atan2(25.0, -70.0) * 180.0 / Math.PI);
        // 孔挖除使实际质心略偏离几何中心（孔往反向拉），连线角相应微偏——±1° 内
        Assert.InRange(result.AngleDeg, expected - 1.0, expected + 1.0);
        Assert.InRange(result.Centroid.X, width / 2.0 - 15, width / 2.0 + 15);
        Assert.InRange(result.Centroid.Y, height / 2.0 - 10, height / 2.0 + 10);
        output.WriteLine($"角度={result.AngleDeg:0.00}° 期望≈{expected:0.00}° 质心=({result.Centroid.X:0.0},{result.Centroid.Y:0.0})");
    }

    [Fact]
    public void HoleOnCentroid_ReturnsNull_Fallback()
    {
        // 孔在掩码中心：基线不足 → null（策略走粗角度兜底）
        var (mask, width, height) = BuildMaskWithHole(holeDx: 0, holeDy: 0, holeR: 12);
        Assert.Null(MaskTemplateMatcher.RefineByCentroidHoleLine(mask, width, height));
    }

    [Fact]
    public void NoHole_ReturnsNull()
    {
        // 实心矩形无孔 → null
        var mask = PackMask((x, y) => true);
        Assert.Null(MaskTemplateMatcher.RefineByCentroidHoleLine(mask, W, H));
    }

    [Fact]
    public void MultipleHoles_LargestWins()
    {
        // 两个孔：大孔在 (60, -20)（r=14）、小孔在 (-60, 0)（r=5，面积 < 30px² 阈值内部分通过）
        var hole1 = ((X: W / 2 + 60, Y: H / 2 - 20), R: 14);
        var hole2 = ((X: W / 2 - 60, Y: H / 2), R: 5);
        var mask = PackMask((x, y) =>
            !InCircle(x, y, hole1.Item1.X, hole1.Item1.Y, hole1.R) &&
            !InCircle(x, y, hole2.Item1.X, hole2.Item1.Y, hole2.R));
        var result = MaskTemplateMatcher.RefineByCentroidHoleLine(mask, W, H);

        Assert.NotNull(result);
        // 大孔面积 π·14²≈615 远超小孔 π·5²≈79，应取大孔；质心受双孔挖除微偏，±1° 容差
        var expected = Norm(Math.Atan2(-20.0, 60.0) * 180.0 / Math.PI);
        Assert.InRange(result.AngleDeg, expected - 1.0, expected + 1.0);
        output.WriteLine($"多孔取最大：角度={result.AngleDeg:0.00}°（期望指向大孔 ≈{expected:0.00}°）");
    }

    [Fact]
    public void RotatedScenario_AngleFollowsRotation()
    {
        // 同一几何配置，孔绕掩码中心转 90°：连线角应同步转 90°（方向语义验证）。
        // 基线 40px（H=120 内留边），孔 r=12
        var (mask1, w1, h1) = BuildMaskWithHole(holeDx: 40, holeDy: 0, holeR: 12);
        var (mask2, w2, h2) = BuildMaskWithHole(holeDx: 0, holeDy: -40, holeR: 12);
        var r1 = MaskTemplateMatcher.RefineByCentroidHoleLine(mask1, w1, h1);
        var r2 = MaskTemplateMatcher.RefineByCentroidHoleLine(mask2, w2, h2);

        Assert.NotNull(r1);
        Assert.NotNull(r2);
        Assert.InRange(r1.AngleDeg, -1.0, 1.0);     // 指向 +x
        Assert.InRange(r2.AngleDeg, -91.0, -89.0);  // 指向 -y（图像系 y 向下）
    }

    // ---- 槽场景：长轴定角 + 偏置侧定头尾 ----

    [Fact]
    public void RadialSlot_AxisAngle_OffsetSideDecidesDirection()
    {
        // 径向槽：水平长槽（长 70、r=5，轴比 ≈ 8 > 2.5 走槽分支），中点在质心右侧 50px。
        // 角度 = 长轴 0°；偏置沿轴向且为正 → 方向指向右侧 → 0°
        var mask = BuildMaskWithSlot(W / 2 + 50, H / 2, halfLen: 35, r: 5);
        var result = MaskTemplateMatcher.RefineByCentroidHoleLine(mask, W, H);

        Assert.NotNull(result);
        Assert.True(AxisIsHorizontal(result.AngleDeg), $"径向槽角度应贴近水平轴，实际 {result.AngleDeg:0.00}°");
        output.WriteLine($"径向槽（右偏置）：{result.AngleDeg:0.00}°");
    }

    [Fact]
    public void RadialSlot_180Flip_FlipsDirection()
    {
        // 同一槽移到左侧：轴向分量为负 → 输出翻转 180°（方向语义）
        var right = MaskTemplateMatcher.RefineByCentroidHoleLine(
            BuildMaskWithSlot(W / 2 + 50, H / 2, 35, 5), W, H);
        var left = MaskTemplateMatcher.RefineByCentroidHoleLine(
            BuildMaskWithSlot(W / 2 - 50, H / 2, 35, 5), W, H);

        Assert.NotNull(right);
        Assert.NotNull(left);
        var diff = Math.Abs(Norm(right.AngleDeg - left.AngleDeg));
        Assert.InRange(diff, 179.0, 181.0); // 恰好翻转 180°
        output.WriteLine($"右偏置 {right.AngleDeg:0.00}° vs 左偏置 {left.AngleDeg:0.00}°（Δ180°）");
    }

    [Fact]
    public void TangentialSlot_NormalSideDecidesDirection()
    {
        // 切向槽：水平长槽在中点上方 40px（偏置沿法向）。角度 = 长轴 0°（精度不依赖偏置），
        // 头尾由法向分量符号决定；翻转后（槽移到下方）输出应相差 180°
        var above = MaskTemplateMatcher.RefineByCentroidHoleLine(
            BuildMaskWithSlot(W / 2, H / 2 - 40, 35, 5), W, H);
        var below = MaskTemplateMatcher.RefineByCentroidHoleLine(
            BuildMaskWithSlot(W / 2, H / 2 + 40, 35, 5), W, H);

        Assert.NotNull(above);
        Assert.NotNull(below);
        // 两输出都应贴近水平轴（0°/180° 任一表示），且互补（差 180°）
        Assert.True(AxisIsHorizontal(above.AngleDeg), $"切向槽角度应贴近水平轴，实际 {above.AngleDeg:0.00}°");
        Assert.True(AxisIsHorizontal(below.AngleDeg), $"切向槽角度应贴近水平轴，实际 {below.AngleDeg:0.00}°");
        var diff = Math.Abs(Norm(above.AngleDeg - below.AngleDeg));
        Assert.InRange(diff, 179.0, 181.0);
        output.WriteLine($"切向槽：上 {above.AngleDeg:0.00}° vs 下 {below.AngleDeg:0.00}°（Δ180°）");
    }

    // ---- 合成工具 ----

    /// <summary>合成带孔掩码：W×H 实心矩形（留边 5px）挖一个圆孔，位打包为 YoloDotNet 格式。</summary>
    private static (byte[] Mask, int Width, int Height) BuildMaskWithHole(int holeDx, int holeDy, int holeR)
    {
        var cx = W / 2 + holeDx;
        var cy = H / 2 + holeDy;
        var mask = PackMask((x, y) =>
            x >= 5 && x < W - 5 && y >= 5 && y < H - 5 &&
            !InCircle(x, y, cx, cy, holeR));
        return (mask, W, H);
    }

    private static bool InCircle(int x, int y, int cx, int cy, int r) =>
        (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;

    /// <summary>合成带水平胶囊槽的掩码：实心矩形挖一条水平槽（距中心点 ±halfLen、半径 r）。</summary>
    private static byte[] BuildMaskWithSlot(int cx, int cy, int halfLen, int r) =>
        PackMask((x, y) =>
            x >= 5 && x < W - 5 && y >= 5 && y < H - 5 &&
            !(Math.Abs(x - cx) <= halfLen && Math.Abs(y - cy) <= r));

    /// <summary>谓词位图 → LSB-first 位打包字节数组（与 YoloDotNet IsPixelSet 解码格式互逆）。</summary>
    private static byte[] PackMask(Func<int, int, bool> isSet)
    {
        var bits = new byte[(W * H + 7) / 8];
        for (var y = 0; y < H; y++)
            for (var x = 0; x < W; x++)
                if (isSet(x, y))
                {
                    var idx = y * W + x;
                    bits[idx >> 3] |= (byte)(1 << (idx & 7));
                }
        return bits;
    }

    private static double Norm(double deg)
    {
        var d = ((deg + 180.0) % 360.0 + 360.0) % 360.0 - 180.0;
        return d == -180.0 ? 180.0 : d;
    }

    /// <summary>角度是否贴近水平轴（0° 或 180°，两种表示等价，容差 1°）。</summary>
    private static bool AxisIsHorizontal(double deg)
    {
        var d = Math.Abs(Norm(deg));
        return d <= 1.0 || d >= 179.0;
    }
}
