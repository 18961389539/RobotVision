using System.IO;
using System.Windows;
using FluentAssertions;
using ImageViewer.Models;
using ImageViewer.Plugins;
using ImageViewer.Services;

namespace ImageViewerControl.Tests;

/// <summary>
/// ROI 持久化测试：内置 ROI 类型的序列化/反序列化往返一致性、
/// 文档级属性（PixelSize/PhysicalUnit/Version）、未知类型跳过策略、损坏 JSON 容错。
/// </summary>
public class RoiPersistenceServiceTests
{
    private static RoiPluginRegistry Registry => RoiPluginRegistry.Default;

    [Fact]
    public void SerializeDeserialize_Circle_RoundTrips()
    {
        var circle = new CircleRoi { Center = new Point(120.5, 80), Radius = 25, Label = "孔1" };

        var json = RoiPersistenceService.Serialize([circle], 0.05, "mm", Registry);
        var (rois, pixelSize, unit) = RoiPersistenceService.Deserialize(json, Registry);

        rois.Should().ContainSingle();
        var loaded = rois[0].Should().BeOfType<CircleRoi>().Subject;
        loaded.Center.Should().Be(new Point(120.5, 80));
        loaded.Radius.Should().Be(25);
        loaded.Label.Should().Be("孔1");
        pixelSize.Should().Be(0.05);
        unit.Should().Be("mm");
    }

    [Fact]
    public void SerializeDeserialize_MixedTypes_AllRoundTrip()
    {
        var circle = new CircleRoi { Center = new Point(0, 0), Radius = 10 };
        var polygon = new PolygonRoi();
        polygon.Points.Add(new Point(0, 0));
        polygon.Points.Add(new Point(5, 0));
        polygon.Points.Add(new Point(5, 5));
        var text = new TextAnnotationRoi { Position = new Point(9, 9), Label = "注释" };

        var json = RoiPersistenceService.Serialize([circle, polygon, text], 1.0, "px", Registry);
        var (rois, _, _) = RoiPersistenceService.Deserialize(json, Registry);

        rois.Should().HaveCount(3);
        rois[0].Should().BeOfType<CircleRoi>();
        rois[1].Should().BeOfType<PolygonRoi>();
        rois[2].Should().BeOfType<TextAnnotationRoi>();
    }

    [Fact]
    public void Deserialize_EmptyJson_ReturnsEmptyList()
    {
        var (rois, pixelSize, unit) = RoiPersistenceService.Deserialize("{}", Registry);

        rois.Should().BeEmpty();
        pixelSize.Should().Be(1.0); // ≤0 → 1.0
        unit.Should().Be("px");
    }

    [Fact]
    public void Deserialize_MissingPhysicalUnit_DefaultsToPx()
    {
        var json = RoiPersistenceService.Serialize([], 0.1, null!, Registry);

        var (_, _, unit) = RoiPersistenceService.Deserialize(json, Registry);

        unit.Should().Be("px"); // null → "px"
    }

    [Fact]
    public void Deserialize_InvalidJson_Throws()
    {
        // 当前实现：非法 JSON 直接抛 JsonException（无静默容错）
        var act = () => RoiPersistenceService.Deserialize("not json at all", Registry);

        act.Should().Throw<System.Text.Json.JsonException>();
    }

    [Fact]
    public void Serialize_UnknownRoiType_IsSkipped()
    {
        var unknown = new CustomRoi();
        var known = new CircleRoi { Center = new Point(1, 1), Radius = 2 };

        var json = RoiPersistenceService.Serialize([unknown, known], 1, "px", Registry);
        var (rois, _, _) = RoiPersistenceService.Deserialize(json, Registry);

        rois.Should().ContainSingle().Which.Should().BeOfType<CircleRoi>();
    }

    [Fact]
    public void SaveAndLoad_File_RoundTrips()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "rv_roi_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var ring = new RingRoi { Center = new Point(3, 4), InnerRadius = 5, OuterRadius = 10 };
            RoiPersistenceService.SaveToFile(path, [ring], 0.5, "cm", Registry);

            var (rois, pixelSize, unit) = RoiPersistenceService.LoadFromFile(path, Registry);

            var loaded = rois.Should().ContainSingle().Subject.Should().BeOfType<RingRoi>().Subject;
            loaded.Center.Should().Be(new Point(3, 4));
            loaded.InnerRadius.Should().Be(5);
            loaded.OuterRadius.Should().Be(10);
            pixelSize.Should().Be(0.5);
            unit.Should().Be("cm");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SaveAndLoad_Async_Works()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "rv_roi_a_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var line = new LineMeasureRoi { P1 = new Point(0, 0), P2 = new Point(10, 10) };
            await RoiPersistenceService.SaveToFileAsync(path, [line], 1, "px", Registry);

            var (rois, _, _) = await RoiPersistenceService.LoadFromFileAsync(path, Registry);

            rois.Should().ContainSingle().Which.Should().BeOfType<LineMeasureRoi>();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Serialize_NullRegistry_Throws() =>
        ((Func<string>)(() => RoiPersistenceService.Serialize([], 1, "px", null!)))
            .Should().Throw<ArgumentNullException>();

    private sealed class CustomRoi : RoiBase
    {
        public override RoiBase Clone() => new CustomRoi();

        public override void ApplyFrom(RoiBase source)
        {
        }
    }
}
