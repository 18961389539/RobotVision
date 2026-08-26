using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ImageViewer.Controls;
using ImageViewer.Models;
using Microsoft.Extensions.DependencyInjection;

namespace RobotVision.WpfHost.Pages;

/// <summary>
/// 标定向导页 code-behind：
/// - 图像显示用完整 ImageViewer；
/// - 左键点选：TryHitImage 换算为图像像素坐标；
/// - 标定点：PointAnnotationRoi 叠加显示。
/// </summary>
public partial class CalibrationWizardPage : Page
{
    private readonly NotifyCollectionChangedEventHandler _pointsChanged;
    private CalibrationWizardViewModel? _vm;

    public CalibrationWizardPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService(typeof(CalibrationWizardViewModel));
        _vm = DataContext as CalibrationWizardViewModel;
        NumberBoxCommit.Bind(this, _vm);

        _pointsChanged = (_, _) => SyncPointMarkers();

        Loaded += (_, _) =>
        {
            _vm?.RefreshCameras();
            if (_vm is not null)
                _vm.Points.CollectionChanged += _pointsChanged;
            SyncPointMarkers();
        };
        Unloaded += (_, _) =>
        {
            if (_vm is not null)
                _vm.Points.CollectionChanged -= _pointsChanged;
            Viewer.ViewerState.PointAnnotationRois.Clear();
            _vm?.ResetSession();
        };
    }

    private void OnViewerMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_vm is null || !_vm.Clickable)
            return;
        if (!Viewer.TryHitImage(e, out var imagePoint))
            return;

        _vm.AddPoint(imagePoint.X, imagePoint.Y);
        e.Handled = true;
    }

    private void SyncPointMarkers()
    {
        var rois = Viewer.ViewerState.PointAnnotationRois;
        rois.Clear();
        if (_vm is null)
            return;

        foreach (var p in _vm.Points)
        {
            rois.Add(new PointAnnotationRoi
            {
                Position = new Point(p.PixelX, p.PixelY),
                Label = p.Index.ToString(),
                StrokeColor = Colors.Orange,
                IsLocked = true,
            });
        }
    }
}
