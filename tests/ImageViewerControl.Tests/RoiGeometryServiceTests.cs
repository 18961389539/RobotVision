using System.Windows;
using System.Windows.Media;
using FluentAssertions;
using ImageViewer.Models;
using ImageViewer.Services;

namespace ImageViewerControl.Tests;

/// <summary>
/// ROI 几何服务测试：环形内径钳制、多边形闭合判定、自由手绘点追加、
/// 各类 ROI 边界框（Bounds）与代表点（RepresentativePoints）。
/// </summary>
public class RoiGeometryServiceTests
{
    // ---------- 环形半径 ----------

    [Theory]
    [InlineData(50, 10, true)]
    [InlineData(10, 10, false)] // 恰好等于最小尺寸不可绘制
    [InlineData(5, 10, false)]
    public void IsRingDrawable_ComparesAgainstMinimum(double outer, double minimum, bool expected) =>
        RoiGeometryService.IsRingDrawable(outer, minimum).Should().Be(expected);

    [Fact]
    public void ClampRingInnerRadius_KeepsInnerWithinDrawableBounds()
    {
        RoiGeometryService.ClampRingInnerRadius(5, 50, 10).Should().Be(5);    // 合法
        RoiGeometryService.ClampRingInnerRadius(45, 50, 10).Should().Be(40);  // 上限 = 外径 - 最小尺寸
        RoiGeometryService.ClampRingInnerRadius(-5, 50, 10).Should().Be(-5);  // 只收紧过大值，负数原样（实现语义）
    }

    [Fact]
    public void GetAdjustedRingInnerRadius_DelegatesToClamp()
    {
        RoiGeometryService.GetAdjustedRingInnerRadius(50, 60, 10)
            .Should().Be(RoiGeometryService.ClampRingInnerRadius(60, 50, 10));
    }

    // ---------- 多边形闭合 / 自由线追加 ----------

    [Theory]
    [InlineData(10, 10, 5, 2, true)]   // 距离 0 < 5 → 闭合
    [InlineData(100, 100, 5, 2, false)] // 距离远 → 不闭合
    public void ShouldClosePolygon_AppliesScaleAwareTolerance(
        double x, double y, double tolerance, double scale, bool expected) =>
        RoiGeometryService.ShouldClosePolygon(new Point(10, 10), new Point(x, y), tolerance, scale)
            .Should().Be(expected);

    [Fact]
    public void ShouldAppendFreehandPolylinePoint_FirstPointAlwaysAppends() =>
        RoiGeometryService.ShouldAppendFreehandPolylinePoint([], new Point(0, 0)).Should().BeTrue();

    [Fact]
    public void ShouldAppendFreehandPolylinePoint_RequiresMinimumDistance()
    {
        var points = new[] { new Point(0, 0) };
        RoiGeometryService.ShouldAppendFreehandPolylinePoint(points, new Point(0.5, 0), 1).Should().BeFalse();
        RoiGeometryService.ShouldAppendFreehandPolylinePoint(points, new Point(1.0, 0), 1).Should().BeTrue();
    }

    // ---------- GetBounds ----------

    [Fact]
    public void GetBounds_Circle_IsCenteredSquare()
    {
        var circle = new CircleRoi { Center = new Point(100, 100), Radius = 50 };

        var bounds = RoiGeometryService.GetBounds(circle);

        bounds.Should().Be(new Rect(50, 50, 100, 100));
    }

    [Fact]
    public void GetBounds_Ring_UsesOuterRadius()
    {
        var ring = new RingRoi { Center = new Point(0, 0), OuterRadius = 40, InnerRadius = 10 };

        var bounds = RoiGeometryService.GetBounds(ring);

        bounds.Should().Be(new Rect(-40, -40, 80, 80));
    }

    [Fact]
    public void GetBounds_Polygon_IsBoundingBox()
    {
        var polygon = new PolygonRoi();
        polygon.Points.Add(new Point(0, 0));
        polygon.Points.Add(new Point(10, 0));
        polygon.Points.Add(new Point(10, 20));

        var bounds = RoiGeometryService.GetBounds(polygon);

        bounds.Should().Be(new Rect(0, 0, 10, 20));
    }

    [Fact]
    public void GetBounds_Line_IsSegmentBox()
    {
        var line = new LineMeasureRoi { P1 = new Point(5, 5), P2 = new Point(15, 5) };

        var bounds = RoiGeometryService.GetBounds(line);

        bounds.Should().Be(new Rect(5, 5, 10, 0)); // 水平线：高度 0
    }

    [Fact]
    public void GetBounds_RotatedRect_AccountsForRotation()
    {
        // 100×40 矩形旋转 90°：包围盒应为 40×100（近似，含旋转精度）
        var rect = new RotatedRect { Center = new Point(50, 50), Width = 100, Height = 40, Angle = 90 };

        var bounds = RoiGeometryService.GetBounds(rect);

        bounds.Width.Should().BeApproximately(40, 0.01);
        bounds.Height.Should().BeApproximately(100, 0.01);
    }

    [Fact]
    public void GetBounds_TextAnnotation_IsUnitPoint()
    {
        var text = new TextAnnotationRoi { Position = new Point(7, 8) };

        var bounds = RoiGeometryService.GetBounds(text);

        bounds.Should().Be(new Rect(7, 8, 1, 1));
    }

    [Fact]
    public void GetBounds_UnknownType_ReturnsEmpty()
    {
        // 定义一个未注册的自定义 ROI：默认分支返回 Rect.Empty
        var custom = new CustomRoi();

        RoiGeometryService.GetBounds(custom).Should().Be(Rect.Empty);
    }

    // ---------- GetRepresentativePoints ----------

    [Fact]
    public void GetRepresentativePoints_Circle_IsCenter() =>
        RoiGeometryService.GetRepresentativePoints(new CircleRoi { Center = new Point(1, 2) })
            .Should().ContainSingle().Which.Should().Be(new Point(1, 2));

    [Fact]
    public void GetRepresentativePoints_Polygon_AreVertices()
    {
        var polygon = new PolygonRoi();
        polygon.Points.Add(new Point(1, 1));
        polygon.Points.Add(new Point(2, 2));

        RoiGeometryService.GetRepresentativePoints(polygon).Should().HaveCount(2);
    }

    [Fact]
    public void GetRepresentativePoints_UnknownType_IsEmpty() =>
        RoiGeometryService.GetRepresentativePoints(new CustomRoi()).Should().BeEmpty();

    [Fact]
    public void GetRepresentativePoints_Null_Throws() =>
        ((Func<IEnumerable<Point>>)(() => RoiGeometryService.GetRepresentativePoints(null!)))
            .Should().Throw<ArgumentNullException>();

    [Fact]
    public void GetBounds_Null_Throws() =>
        ((Func<Rect>)(() => RoiGeometryService.GetBounds(null!)))
            .Should().Throw<ArgumentNullException>();

    private sealed class CustomRoi : RoiBase
    {
        public override RoiBase Clone() => new CustomRoi();

        public override void ApplyFrom(RoiBase source)
        {
        }
    }
}
