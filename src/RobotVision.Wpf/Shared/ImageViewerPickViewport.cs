using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ImageViewer.Models;
using ViewerControl = ImageViewer.Controls.ImageViewer;

namespace RobotVision.WpfHost.Shared;

/// <summary>图像点选视口：左键命中图像坐标、点标注叠加（标定向导等）。</summary>
public interface IImagePickViewport
{
    bool TryHitImage(MouseButtonEventArgs e, out Point imagePoint);

    void SyncPointMarkers(IReadOnlyList<ImagePickMarker> markers);

    void ClearPointMarkers();
}

public sealed record ImagePickMarker(double X, double Y, string Label, Color StrokeColor);

/// <summary><see cref="ImageViewer"/> 的点选/标注适配器。</summary>
public sealed class ImageViewerPickViewport(ViewerControl viewer) : IImagePickViewport
{
    private readonly ViewerControl _viewer = viewer;

    public bool TryHitImage(MouseButtonEventArgs e, out Point imagePoint) =>
        _viewer.TryHitImage(e, out imagePoint);

    public void SyncPointMarkers(IReadOnlyList<ImagePickMarker> markers)
    {
        var rois = _viewer.ViewerState.PointAnnotationRois;
        rois.Clear();
        foreach (var marker in markers)
        {
            rois.Add(new PointAnnotationRoi
            {
                Position = new Point(marker.X, marker.Y),
                Label = marker.Label,
                StrokeColor = marker.StrokeColor,
                IsLocked = true,
            });
        }
    }

    public void ClearPointMarkers() => _viewer.ViewerState.PointAnnotationRois.Clear();
}
