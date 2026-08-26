using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageViewer.Controls
{
    /// <summary>看图专用标记（图像像素坐标）。标定向导点选等轻量叠加，不进入 ROI/分析管线。</summary>
    public sealed record DisplayMarker(double X, double Y, string Label);

    /// <summary>
    /// 产线宿主用的轻量看图控件：滚轮缩放、中键平移、可选点标记。
    /// 不创建 ImageViewerHost / 插件 / 自动保存 / 分析调度。
    /// </summary>
    public partial class DisplayImageViewer : UserControl
    {
        public const double MinScale = 0.1;
        public const double MaxScale = 100;

        public static readonly DependencyProperty ImageSourceProperty =
            DependencyProperty.Register(
                nameof(ImageSource),
                typeof(ImageSource),
                typeof(DisplayImageViewer),
                new PropertyMetadata(null, OnImageSourceChanged));

        public static readonly DependencyProperty MarkersProperty =
            DependencyProperty.Register(
                nameof(Markers),
                typeof(IEnumerable),
                typeof(DisplayImageViewer),
                new PropertyMetadata(null, OnMarkersChanged));

        private ImageViewerViewportState _viewport = ImageViewerViewportState.Default;
        private Point _panOrigin;
        private ImageViewerViewportState _panStart;
        private bool _panning;

        public DisplayImageViewer()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            rootGrid.MouseWheel += OnMouseWheel;
            rootGrid.MouseDown += OnMouseDown;
            rootGrid.MouseMove += OnMouseMove;
            rootGrid.MouseUp += OnMouseUp;
            rootGrid.LostMouseCapture += OnLostMouseCapture;
            rootGrid.SizeChanged += (_, _) =>
            {
                if (ImageSource is not null)
                    FitToView();
            };
        }

        public ImageSource? ImageSource
        {
            get => (ImageSource?)GetValue(ImageSourceProperty);
            set => SetValue(ImageSourceProperty, value);
        }

        public IEnumerable? Markers
        {
            get => (IEnumerable?)GetValue(MarkersProperty);
            set => SetValue(MarkersProperty, value);
        }

        /// <summary>将鼠标位置换算为图像像素坐标；点在图像外返回 false。</summary>
        public bool TryHitImage(MouseEventArgs e, out Point imagePoint)
        {
            imagePoint = default;
            if (ImageSource is not BitmapSource bitmap)
                return false;

            var position = e.GetPosition(image);
            if (position.X < 0 || position.Y < 0 ||
                position.X >= bitmap.PixelWidth || position.Y >= bitmap.PixelHeight)
                return false;

            imagePoint = position;
            return true;
        }

        private void OnLoaded(object sender, RoutedEventArgs e) => FitToView();

        private static void OnImageSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var viewer = (DisplayImageViewer)d;
            viewer.image.Source = e.NewValue as ImageSource;
            if (viewer.IsLoaded)
                viewer.FitToView();
        }

        private static void OnMarkersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((DisplayImageViewer)d).markerHost.ItemsSource = e.NewValue as IEnumerable;
        }

        private void FitToView()
        {
            if (!ImageViewerImageSourceUtilities.TryGetSourceImageSize(ImageSource, out var imageSize) ||
                rootGrid.ActualWidth <= 0 || rootGrid.ActualHeight <= 0)
            {
                ApplyViewport(ImageViewerViewportState.Default);
                return;
            }

            var viewport = new Size(rootGrid.ActualWidth, rootGrid.ActualHeight);
            var scale = Math.Min(
                viewport.Width / Math.Max(imageSize.Width, 1),
                viewport.Height / Math.Max(imageSize.Height, 1));
            scale = Math.Clamp(scale, MinScale, MaxScale);
            var x = (viewport.Width - imageSize.Width * scale) / 2;
            var y = (viewport.Height - imageSize.Height * scale) / 2;
            ApplyViewport(new ImageViewerViewportState(scale, x, y));
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ImageSource is null)
                return;

            var imagePoint = e.GetPosition(image);
            var factor = e.Delta > 0 ? 1.1 : 1 / 1.1;
            ApplyViewport(ImageViewerViewportStateOperations.ZoomAt(
                _viewport, imagePoint, factor, MinScale, MaxScale));
            e.Handled = true;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Middle && e.ChangedButton != MouseButton.Right)
                return;

            _panning = true;
            _panOrigin = e.GetPosition(rootGrid);
            _panStart = _viewport;
            rootGrid.CaptureMouse();
            e.Handled = true;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_panning)
                return;

            var now = e.GetPosition(rootGrid);
            ApplyViewport(ImageViewerViewportStateOperations.TranslateBy(
                _panStart, now - _panOrigin));
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_panning)
                return;
            StopPan();
            e.Handled = true;
        }

        private void OnLostMouseCapture(object sender, MouseEventArgs e) => StopPan();

        private void StopPan()
        {
            if (!_panning)
                return;
            _panning = false;
            if (rootGrid.IsMouseCaptured)
                rootGrid.ReleaseMouseCapture();
        }

        private void ApplyViewport(ImageViewerViewportState state)
        {
            _viewport = ImageViewerViewportStateOperations.Normalize(state, MinScale, MaxScale, allowBelowMinScale: true);
            worldScale.ScaleX = _viewport.Scale;
            worldScale.ScaleY = _viewport.Scale;
            worldTranslate.X = _viewport.TranslateX;
            worldTranslate.Y = _viewport.TranslateY;
        }
    }
}
