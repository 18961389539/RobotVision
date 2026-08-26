using BenchmarkDotNet.Attributes;
using ImageViewer.Models;
using ImageViewer.Plugins;
using ImageViewer.Services;

namespace RobotVision.Benchmarks;

[MemoryDiagnoser]
public class RoiPersistenceBenchmarks
{
    private RoiPluginRegistry _registry = null!;
    private List<RoiBase> _rois = null!;
    private string _json = "";

    [GlobalSetup]
    public void Setup()
    {
        _registry = new RoiPluginRegistry();
        RoiPluginDiscoveryService.RegisterFromAssemblies(
            [typeof(RoiPluginRegistry).Assembly],
            _registry,
            new RoiPluginDiscoveryOptions());
        _rois =
        [
            new CircleRoi { Center = new System.Windows.Point(100, 200), Radius = 50, Label = "孔1" },
            new RingRoi { Center = new System.Windows.Point(300, 400), InnerRadius = 30, OuterRadius = 60 },
            new PolygonRoi { Label = "区域" },
            new TextAnnotationRoi { Position = new System.Windows.Point(10, 10), Label = "注释" },
            new LineMeasureRoi { P1 = new System.Windows.Point(0, 0), P2 = new System.Windows.Point(50, 50) },
        ];
        _json = RoiPersistenceService.Serialize(_rois, 0.05, "mm", _registry);
    }

    [Benchmark]
    public string Serialize() => RoiPersistenceService.Serialize(_rois, 0.05, "mm", _registry);

    [Benchmark]
    public (IReadOnlyList<RoiBase> Rois, double PixelSize, string PhysicalUnit) Deserialize() =>
        RoiPersistenceService.Deserialize(_json, _registry);
}
