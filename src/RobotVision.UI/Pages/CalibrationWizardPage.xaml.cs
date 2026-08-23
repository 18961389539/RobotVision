using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Extensions.DependencyInjection;

namespace RobotVision.UI.Pages;

/// <summary>
/// 标定向导页：图像点选坐标换算（Stretch=Uniform 的信箱映射）
/// 与标记层布局在此处理，逻辑在 CalibrationWizardViewModel。
/// </summary>
public partial class CalibrationWizardPage : Page
{
    private readonly NotifyCollectionChangedEventHandler _pointsChanged;
    private readonly PropertyChangedEventHandler _frameChanged;
    private CalibrationWizardViewModel? _vm;

    public CalibrationWizardPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService(typeof(CalibrationWizardViewModel));
        _vm = DataContext as CalibrationWizardViewModel;

        _pointsChanged = (_, _) => LayoutMarkers();
        _frameChanged = (_, e) =>
        {
            if (e.PropertyName == nameof(CalibrationWizardViewModel.FrameImage))
                LayoutMarkers();
        };

        // 订阅统一在 Loaded 建立、Unloaded 拆除（见下方注释），构造不订阅避免双份
        // 页面可能被导航缓存复用：订阅统一在 Loaded 建立、Unloaded 拆除，
        // 避免再次进入时标记层不再更新
        Loaded += (_, _) =>
        {
            _vm?.RefreshCameras();
            AttachHandlers();
        };
        FrameDisplay.SizeChanged += (_, _) => LayoutMarkers();
        Unloaded += (_, _) => DetachHandlers();
    }

    private void AttachHandlers()
    {
        if (_vm is null)
            return;
        _vm.Points.CollectionChanged += _pointsChanged;
        _vm.PropertyChanged += _frameChanged;
    }

    private void DetachHandlers()
    {
        if (_vm is null)
            return;
        _vm.Points.CollectionChanged -= _pointsChanged;
        _vm.PropertyChanged -= _frameChanged;
    }

    private void OnImageMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_vm is null || !_vm.Clickable || FrameDisplay.Source is not BitmapSource source)
            return;

        var position = e.GetPosition(FrameDisplay);
        if (!TryToImagePixel(position, source, out var pixelX, out var pixelY))
            return;

        _vm.AddPoint(pixelX, pixelY);
    }

    /// <summary>显示坐标 → 图像像素坐标（Uniform 拉伸含信箱留黑边，先扣除偏移再除以缩放）。</summary>
    private bool TryToImagePixel(Point position, BitmapSource source, out double pixelX, out double pixelY)
    {
        pixelX = pixelY = 0;
        if (source.PixelWidth == 0 || source.PixelHeight == 0)
            return false;

        var scale = Math.Min(
            FrameDisplay.ActualWidth / source.PixelWidth,
            FrameDisplay.ActualHeight / source.PixelHeight);
        if (scale <= 0)
            return false;

        var displayWidth = source.PixelWidth * scale;
        var displayHeight = source.PixelHeight * scale;
        var offsetX = (FrameDisplay.ActualWidth - displayWidth) / 2;
        var offsetY = (FrameDisplay.ActualHeight - displayHeight) / 2;

        if (position.X < offsetX || position.X > offsetX + displayWidth ||
            position.Y < offsetY || position.Y > offsetY + displayHeight)
            return false;

        pixelX = (position.X - offsetX) / scale;
        pixelY = (position.Y - offsetY) / scale;
        return true;
    }

    private void LayoutMarkers()
    {
        MarkerCanvas.Children.Clear();
        if (_vm is null || FrameDisplay.Source is not BitmapSource source)
            return;

        var scale = Math.Min(
            FrameDisplay.ActualWidth / source.PixelWidth,
            FrameDisplay.ActualHeight / source.PixelHeight);
        if (double.IsNaN(scale) || scale <= 0)
            return;

        var displayWidth = source.PixelWidth * scale;
        var displayHeight = source.PixelHeight * scale;
        var offsetX = (FrameDisplay.ActualWidth - displayWidth) / 2;
        var offsetY = (FrameDisplay.ActualHeight - displayHeight) / 2;

        foreach (var point in _vm.Points)
        {
            var x = point.PixelX * scale + offsetX;
            var y = point.PixelY * scale + offsetY;

            var marker = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = Brushes.Orange,
                Stroke = Brushes.White,
                StrokeThickness = 1.5,
            };
            Canvas.SetLeft(marker, x - 5);
            Canvas.SetTop(marker, y - 5);
            MarkerCanvas.Children.Add(marker);

            var label = new TextBlock
            {
                Text = point.Index.ToString(),
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
            };
            Canvas.SetLeft(label, x + 7);
            Canvas.SetTop(label, y - 16);
            MarkerCanvas.Children.Add(label);
        }
    }
}
