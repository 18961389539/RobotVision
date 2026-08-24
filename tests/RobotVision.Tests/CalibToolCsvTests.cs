using RobotVision.CalibTool;
using Xunit;

namespace RobotVision.Tests;

/// <summary>CalibTool CSV 解析测试（表头/注释/行号/NaN 拒绝）。</summary>
public class CalibToolCsvTests
{
    [Fact]
    public void ParsePairs_SkipsCommentsAndHeader()
    {
        var (pixel, robot) = CsvPointParser.ParsePairs([
            "# 注释",
            "pixel_x,pixel_y,robot_x,robot_y",
            "  ",
            "100.0,200.0,10.0,20.0",
            "300.0,400.0,30.0,40.0",
        ]);

        Assert.Equal(2, pixel.Length);
        Assert.Equal(2, robot.Length);
        Assert.Equal(100f, pixel[0].X);
        Assert.Equal(200f, pixel[0].Y);
        Assert.Equal(10f, robot[0].X);
        Assert.Equal(40f, robot[1].Y);
    }

    [Fact]
    public void ParsePairs_WrongColumnCount_ThrowsWithLineNumber()
    {
        var ex = Assert.Throws<FormatException>(() => CsvPointParser.ParsePairs([
            "1.0,2.0,3.0", // 3 列
        ]));
        Assert.Contains("第 1 行", ex.Message);
        Assert.Contains("4 列", ex.Message);
    }

    [Fact]
    public void ParsePairs_NaN_Rejected()
    {
        // NaN 放在第 2 行：第 1 行是数字（非表头），NaN 行必须报错
        Assert.Throws<FormatException>(() => CsvPointParser.ParsePairs([
            "1.0,2.0,3.0,4.0",
            "5.0,6.0,7.0,NaN",
        ]));
    }

    [Fact]
    public void ParsePairs_NonNumericLaterLine_Throws()
    {
        // 表头只允许出现在首行；后续非数字行必须报错
        var ex = Assert.Throws<FormatException>(() => CsvPointParser.ParsePairs([
            "1.0,2.0,3.0,4.0",
            "abc,2.0,3.0,4.0",
        ]));
        Assert.Contains("第 2 行", ex.Message);
    }

    [Fact]
    public void ParsePoints_SkipsHeaderAndComments()
    {
        var (points, angles) = CsvPointParser.ParsePoints([
            "pixel_x,pixel_y",
            "# 说明",
            "600.0,350.0",
            "750.0,420.0",
        ]);

        Assert.Null(angles); // 2 列 = 无角度记录
        Assert.Equal(2, points.Length);
        Assert.Equal(600f, points[0].X);
        Assert.Equal(420f, points[1].Y);
    }

    [Fact]
    public void ParsePoints_ThreeColumns_ParsesAngles()
    {
        var (points, angles) = CsvPointParser.ParsePoints([
            "pixel_x,pixel_y,rz_deg",
            "600.0,350.0,0",
            "750.0,420.0,45",
            "680.0,540.0,90",
        ]);

        Assert.NotNull(angles);
        Assert.Equal(3, points.Length);
        Assert.Equal(3, angles.Length);
        Assert.Equal(0, angles[0]);
        Assert.Equal(45, angles[1]);
        Assert.Equal(90, angles[2]);
    }

    [Fact]
    public void ParsePoints_MixedColumnCounts_TreatedAsNoAngles()
    {
        // 混合 2/3 列：角度与点数不一一对应，按无角度处理（自检要求成对）
        var (points, angles) = CsvPointParser.ParsePoints([
            "600.0,350.0,0",
            "750.0,420.0",
        ]);

        Assert.Null(angles);
        Assert.Equal(2, points.Length);
    }

    [Fact]
    public void ParsePoints_WrongColumnCount_Throws()
    {
        Assert.Throws<FormatException>(() => CsvPointParser.ParsePoints(["1.0,2.0,3.0,4.0"]));
    }

    [Fact]
    public void IsNumericLine_DetectsHeadersAndNumbers()
    {
        Assert.False(CsvPointParser.IsNumericLine("pixel_x,pixel_y"));
        Assert.False(CsvPointParser.IsNumericLine("1.0,NaN"));
        Assert.True(CsvPointParser.IsNumericLine("1.0,2.5"));
        Assert.True(CsvPointParser.IsNumericLine("-3.25,4"));
    }
}
