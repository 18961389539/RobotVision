using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.CalibrationWizard;

/// <summary>标定向导图像交互：点选坐标与点表标记同步。</summary>
public sealed class CalibrationWizardImageHost
{
    private readonly CalibrationWizardViewModel _vm;
    private readonly IImagePickViewport _viewport;
    private readonly NotifyCollectionChangedEventHandler _pointsChanged;
    private bool _wired;

    public CalibrationWizardImageHost(CalibrationWizardViewModel vm, IImagePickViewport viewport)
    {
        _vm = vm;
        _viewport = viewport;
        _pointsChanged = (_, _) => SyncPointMarkers();
    }

    public void Wire()
    {
        if (_wired)
            return;
        _wired = true;
        _vm.Points.CollectionChanged += _pointsChanged;
        SyncPointMarkers();
    }

    public void Unwire()
    {
        if (!_wired)
            return;
        _wired = false;
        _vm.Points.CollectionChanged -= _pointsChanged;
        _viewport.ClearPointMarkers();
    }

    public void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (!_vm.Clickable)
            return;
        if (!_viewport.TryHitImage(e, out var imagePoint))
            return;

        _vm.AddPoint(imagePoint.X, imagePoint.Y);
        e.Handled = true;
    }

    private void SyncPointMarkers()
    {
        var markers = _vm.Points
            .Select(p => new ImagePickMarker(
                p.PixelX,
                p.PixelY,
                p.Index.ToString(CultureInfo.InvariantCulture),
                Colors.Orange))
            .ToList();
        _viewport.SyncPointMarkers(markers);
    }
}
