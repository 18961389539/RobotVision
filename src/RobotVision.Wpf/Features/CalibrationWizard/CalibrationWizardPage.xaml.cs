using System.Globalization;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ImageViewer.Controls;
using ImageViewer.Models;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.CalibrationWizard;

/// <summary>
/// 标定向导页 code-behind：
/// - 图像显示用完整 ImageViewer；
/// - 左键点选：TryHitImage 换算为图像像素坐标；
/// - 标定点：PointAnnotationRoi 叠加显示。
/// </summary>
public partial class CalibrationWizardPage : Page
{
    private readonly CalibrationWizardViewModel _vm;
    private readonly NotifyCollectionChangedEventHandler _pointsChanged;

    public CalibrationWizardPage(CalibrationWizardViewModel viewModel)
    {
        _vm = viewModel;
        _pointsChanged = (_, _) => SyncPointMarkers();
        ViewModelPageLifetime.Attach(this, viewModel, onUnloading: () =>
        {
            _vm.Points.CollectionChanged -= _pointsChanged;
            Viewer.ViewerState.PointAnnotationRois.Clear();
            _vm.ResetSession();
        });
        InitializeComponent();
        NumberBoxCommit.Bind(this, _vm);

        Loaded += (_, _) =>
        {
            _vm.RefreshCameras();
            _vm.Points.CollectionChanged += _pointsChanged;
            SyncPointMarkers();
        };
    }

    private void OnViewerMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_vm.Clickable)
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

        foreach (var p in _vm.Points)
        {
            rois.Add(new PointAnnotationRoi
            {
                Position = new Point(p.PixelX, p.PixelY),
                Label = p.Index.ToString(CultureInfo.InvariantCulture),
                StrokeColor = Colors.Orange,
                IsLocked = true,
            });
        }
    }
}
