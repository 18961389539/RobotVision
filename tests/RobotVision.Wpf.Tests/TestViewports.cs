using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ImageViewer.Models;
using RobotVision.WpfHost.Shared;

namespace RobotVision.Wpf.Tests;

internal sealed class FakeRoiViewport : IRoiViewport
{
    public ObservableCollection<RotatedRect> Rects { get; } = [];

    public ObservableCollection<LineMeasureRoi> Lines { get; } = [];

    public event NotifyCollectionChangedEventHandler? RectRoisChanged
    {
        add => Rects.CollectionChanged += value;
        remove => Rects.CollectionChanged -= value;
    }

    public event NotifyCollectionChangedEventHandler? LineRoisChanged
    {
        add => Lines.CollectionChanged += value;
        remove => Lines.CollectionChanged -= value;
    }

    public bool RoiModeStarted { get; private set; }

    public void StartRoiMode() => RoiModeStarted = true;

    public void AddRoi(RotatedRect rect) => Rects.Add(rect);

    public void RemoveRoi(RotatedRect rect) => Rects.Remove(rect);

    public void AddLineRoi(LineMeasureRoi line) => Lines.Add(line);

    public void RemoveLineRoi(LineMeasureRoi line) => Lines.Remove(line);
}

internal sealed class FakePickViewport : IImagePickViewport
{
    public List<ImagePickMarker> Markers { get; } = [];

    public Point? NextHit { get; set; }

    public bool TryHitImage(MouseButtonEventArgs e, out Point imagePoint)
    {
        if (NextHit is { } hit)
        {
            imagePoint = hit;
            return true;
        }

        imagePoint = default;
        return false;
    }

    public void SyncPointMarkers(IReadOnlyList<ImagePickMarker> markers)
    {
        Markers.Clear();
        Markers.AddRange(markers);
    }

    public void ClearPointMarkers() => Markers.Clear();
}
