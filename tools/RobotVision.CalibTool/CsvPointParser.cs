using System.Globalization;
using OpenCvSharp;

namespace RobotVision.CalibTool;

/// <summary>
/// 标定 CSV 解析（可单元测试）：
/// - 空行与 # 注释跳过；首个数据行为非数字文本 → 视为表头跳过；
/// - 列数不符、含非数字或 NaN/Infinity → FormatException（含真实文件行号），供调用方友好提示。
/// </summary>
public static class CsvPointParser
{
    /// <summary>外参点对 CSV：像素x,像素y,机器人x,机器人y。返回两组坐标（长度相等）。</summary>
    public static (Point2f[] Pixel, Point2f[] Robot) ParsePairs(IEnumerable<string> lines)
    {
        var pixels = new List<Point2f>();
        var robots = new List<Point2f>();
        var fileLine = 0;
        var headerHandled = false;

        foreach (var raw in lines)
        {
            fileLine++;
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (!headerHandled && !IsNumericLine(line))
            {
                headerHandled = true;
                continue;
            }
            headerHandled = true;

            var parts = line.Split(',').Select(p => p.Trim()).ToArray();
            if (parts.Length != 4)
                throw new FormatException($"无法解析第 {fileLine} 行（需要 4 列）: {raw}");
            if (parts.Any(p => !TryParseFinite(p, out _)))
                throw new FormatException($"无法解析第 {fileLine} 行（含非数字或非有限数，拒绝 NaN/Infinity）: {raw}");

            pixels.Add(new Point2f(
                float.Parse(parts[0], CultureInfo.InvariantCulture),
                float.Parse(parts[1], CultureInfo.InvariantCulture)));
            robots.Add(new Point2f(
                float.Parse(parts[2], CultureInfo.InvariantCulture),
                float.Parse(parts[3], CultureInfo.InvariantCulture)));
        }

        return ([.. pixels], [.. robots]);
    }

    /// <summary>旋转中心点 CSV：像素x,像素y。返回坐标数组。</summary>
    public static Point2f[] ParsePoints(IEnumerable<string> lines)
    {
        var points = new List<Point2f>();
        var fileLine = 0;
        var headerHandled = false;

        foreach (var raw in lines)
        {
            fileLine++;
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (!headerHandled && !IsNumericLine(line))
            {
                headerHandled = true;
                continue;
            }
            headerHandled = true;

            var parts = line.Split(',').Select(p => p.Trim()).ToArray();
            if (parts.Length != 2)
                throw new FormatException($"无法解析第 {fileLine} 行（需要 2 列）: {raw}");
            if (parts.Any(p => !TryParseFinite(p, out _)))
                throw new FormatException($"无法解析第 {fileLine} 行（含非数字或非有限数，拒绝 NaN/Infinity）: {raw}");

            points.Add(new Point2f(
                float.Parse(parts[0], CultureInfo.InvariantCulture),
                float.Parse(parts[1], CultureInfo.InvariantCulture)));
        }

        return [.. points];
    }

    /// <summary>行内是否全部为可解析的有限数字（表头/说明行检测；NaN/Infinity 视为非法）。</summary>
    public static bool IsNumericLine(string line) =>
        line.Split(',').Select(p => p.Trim()).All(p => TryParseFinite(p, out _));

    /// <summary>CSV 数值解析：可解析且为有限实数（NaN/Infinity 会污染标定结果，一律拒绝）。</summary>
    public static bool TryParseFinite(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
}
