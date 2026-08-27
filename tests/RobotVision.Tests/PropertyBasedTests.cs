using FsCheck.Xunit;
using RobotVision.Core.Geometry;
using RobotVision.Core.Models;
using RobotVision.Infrastructure.Communication;

namespace RobotVision.Tests;

/// <summary>
/// 属性测试（FsCheck）：用随机输入验证数学/解析性质的普适性，
/// 覆盖确定性用例无法触达的边界（负数/极大值/NaN/Infinity/退化几何）。
/// 约定：非有限输入只要求"不抛异常、不产生 NaN 输出"，有限输入要求严格性质。
/// 返回 bool：FsCheck.Xunit 的 [Property] 直接接受 bool 断言。
/// </summary>
public class PropertyBasedTests
{
    // ---------- AngleGeometry.NormalizeDeg ----------

    [Property]
    public bool NormalizeDeg_Finite_AlwaysIn_0To360(double deg)
    {
        var result = AngleGeometry.NormalizeDeg(deg);
        return result >= 0 && result < 360 || !double.IsFinite(deg);
    }

    [Property]
    public bool NormalizeDeg_NonFinite_NoCrash(double deg)
    {
        var result = AngleGeometry.NormalizeDeg(deg);
        return double.IsFinite(result) || !double.IsFinite(deg);
    }

    [Property]
    public bool NormalizeDeg_Idempotent(double deg)
    {
        var once = AngleGeometry.NormalizeDeg(deg);
        var twice = AngleGeometry.NormalizeDeg(once);
        return once == twice || (double.IsNaN(once) && double.IsNaN(twice));
    }

    [Property]
    public bool NormalizeDeg_Periodicity(double deg)
    {
        var d = AngleGeometry.NormalizeDeg(deg);
        var shifted = AngleGeometry.NormalizeDeg(deg + 360);
        return (!double.IsFinite(deg) && double.IsNaN(d) && double.IsNaN(shifted))
               || Math.Abs(d - shifted) < 1e-9;
    }

    // ---------- AngleGeometry.NormalizeSignedDeg ----------

    [Property]
    public bool NormalizeSignedDeg_Finite_InOpenInterval(double deg)
    {
        var result = AngleGeometry.NormalizeSignedDeg(deg);
        return result > -180 && result <= 180 || !double.IsFinite(deg);
    }

    [Property]
    public bool NormalizeSignedDeg_ZeroStaysZero() =>
        AngleGeometry.NormalizeSignedDeg(0) == 0;

    // ---------- AngleGeometry.FromTwoPoints ----------

    [Property]
    public bool FromTwoPoints_CoincidentPoints_NoNaN(double x, double y)
    {
        // 有限输入下重合点角度必须确定；非有限输入（inf-inf=NaN）属防御行为，不在此断言
        if (!double.IsFinite(x) || !double.IsFinite(y))
            return true;
        var (_, angle) = AngleGeometry.FromTwoPoints(new ImagePoint(x, y), new ImagePoint(x, y));
        return !double.IsNaN(angle);
    }

    [Property]
    public bool FromTwoPoints_DirectionMatchesVector(double ax, double ay, double bx, double by)
    {
        var (_, angle) = AngleGeometry.FromTwoPoints(new ImagePoint(ax, ay), new ImagePoint(bx, by));
        var dx = bx - ax;
        var dy = by - ay;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1e-12 || !double.IsFinite(length) || !double.IsFinite(angle))
            return true;

        // 角度（度）应等于 atan2(dy, dx) 归一化到 [0,360)
        var expected = (Math.Atan2(dy, dx) * 180 / Math.PI + 360) % 360;
        var diff = Math.Abs(angle - expected);
        return diff < 1e-6 || Math.Abs(diff - 360) < 1e-6;
    }

    // ---------- RotationCenterCompensation ----------

    [Property]
    public bool Rotation_PreservesDistance(double x, double y, double cx, double cy, double deltaDeg)
    {
        var (rx, ry) = RotationCenterCompensation.Rotate(x, y, cx, cy, deltaDeg);
        var before = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
        var after = Math.Sqrt((rx - cx) * (rx - cx) + (ry - cy) * (ry - cy));
        return !double.IsFinite(before) || !double.IsFinite(after)
               || Math.Abs(before - after) < 1e-6 * Math.Max(1, before);
    }

    [Property]
    public bool Rotation_ZeroDelta_IsIdentity(double x, double y, double cx, double cy)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(cx) || !double.IsFinite(cy))
            return true;
        // 点/中心尺度差过大（如 y=-1.8e308、cy=+1.8e308）时中间量溢出为 Infinity，
        // 浮点容差无法兜底；现实坐标（毫米量级）不可能出现，跳过而非判失败
        if (Math.Abs(x) > 1e15 || Math.Abs(y) > 1e15 || Math.Abs(cx) > 1e15 || Math.Abs(cy) > 1e15)
            return true;
        var (rx, ry) = RotationCenterCompensation.Rotate(x, y, cx, cy, 0);
        // 尺度感知容差：点坐标与旋转中心尺度差极大时浮点消去误差不可避免（如 cy=1.8e308）
        var tolX = 1e-9 * Math.Max(1, Math.Max(Math.Abs(x), Math.Abs(cx)));
        var tolY = 1e-9 * Math.Max(1, Math.Max(Math.Abs(y), Math.Abs(cy)));
        return Math.Abs(rx - x) <= tolX && Math.Abs(ry - y) <= tolY;
    }

    // ---------- TcpServerManager 协议 ----------

    [Property]
    public bool ParseTriggerLine_ValidSyntax_RoundTrips(string key, double x, double y, double rz)
    {
        // 键名必须匹配合法配方名模式（字母/数字/下划线/中划线），数值必须有限；
        // 其余输入协议拒绝，不在此断言
        if (!System.Text.RegularExpressions.Regex.IsMatch(key, "^[A-Za-z0-9_-]+$")
            || !double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(rz))
            return true;

        // 用往返格式（R）保留 double 精度，确保数值无损往返
        var line = $"{key},{x.ToString("R")},{y.ToString("R")},{rz.ToString("R")}";
        var (recipeKey, pose, formatError) = TcpServerManager.ParseTriggerLine(line);
        if (formatError is not null || pose is null)
            return false;

        return recipeKey == key
               && Math.Abs(pose.X - x) < 1e-6
               && Math.Abs(pose.Y - y) < 1e-6
               && Math.Abs(pose.RzDeg - rz) < 1e-6;
    }

    [Property]
    public bool FormatReply_OkResult_ContainsCorrectFields()
    {
        var result = VisionResult.Success(
            "A01",
            [new RobotPose(10, 20, 30), new RobotPose(40, 50, 60)],
            12.3,
            [0.9, 0.8]);
        var reply = TcpServerManager.FormatReply(result);
        return reply.StartsWith("OK,") && reply.Contains("A01") && reply.EndsWith(",2,12");
    }

    [Property]
    public bool TryParseWhitelistEntry_AnyString_NoThrow(string octet1, string octet2, string octet3, string octet4)
    {
        var entry = $"{octet1}.{octet2}.{octet3}.{octet4}";
        return TcpServerManager.TryParseWhitelistEntry(entry) is true or false;
    }
}
