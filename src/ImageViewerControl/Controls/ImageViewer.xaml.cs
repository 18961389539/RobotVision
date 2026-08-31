using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ImageViewer.Services;

namespace ImageViewer.Controls
{
    public partial class ImageViewer : UserControl, IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// ImageViewer 控件：主要入口类
        /// Chinese: 该类实现了图像查看器控件的 UI 互动绑定、依赖属性以及键盘/鼠标事件的初始化。
        /// English: Main ImageViewer control partial class that wires up dependency properties and input handlers.
        /// </summary>
        private readonly ImageViewerInteractionManipulationState _interactionManipulationState = new();
        private readonly ImageViewerHostState _hostState;
        private readonly ImageViewerHost? _ownedHost;
        private const double MinScale = 0.1;
        private const double MaxScale = 100;
        private readonly ImageViewerControlComposition _controlComposition;
        private readonly ImageViewerAnalysisState _analysisState = new();
        private readonly IImageViewerLatestTaskScheduler _infoPanelStatisticsScheduler;
        private readonly ImageViewerLifetime _lifetime;
        private int _imageRotation;
        private bool _flipImageHorizontally;
        private bool _flipImageVertically;

        public ImageViewer()
            : this(ImageViewerHost.CreateDefault())
        {
        }

        private ImageViewer(ImageViewerHost host)
            : this(host.Dependencies)
        {
            _ownedHost = host ?? throw new ArgumentNullException(nameof(host));
        }

        public ImageViewer(ImageViewerDependencies dependencies)
        {
            ArgumentNullException.ThrowIfNull(dependencies);
            ImageViewerBootstrapState bootstrapState = CreateBootstrapState(dependencies);
            _hostState = bootstrapState.HostState;
            _viewportOverlayRefreshScheduler = bootstrapState.ViewportOverlayRefreshScheduler;
            _analysisRefreshScheduler = bootstrapState.AnalysisRefreshScheduler;
            _infoPanelStatisticsScheduler = bootstrapState.InfoPanelStatisticsScheduler;
            _controlComposition = dependencies.CreateControlComposition(this);
            _lifetime = new ImageViewerLifetime(CreateLifetimeRegistrations(_controlComposition));
            CompleteBootstrap();
        }

        private void ResetView() => _viewCommandController.Execute(ImageViewerViewCommand.ResetView);

        private void RotateImageLeft() => SetImageRotation(_imageRotation - 90);

        private void RotateImageRight() => SetImageRotation(_imageRotation + 90);

        private void FlipImageHorizontal()
        {
            _flipImageHorizontally = !_flipImageHorizontally;
            ApplyImageOrientation();
        }

        private void FlipImageVertical()
        {
            _flipImageVertically = !_flipImageVertically;
            ApplyImageOrientation();
        }

        private void SetImageRotation(int angle)
        {
            _imageRotation = ((angle % 360) + 360) % 360;
            ApplyImageOrientation();
        }

        private void ResetImageOrientation()
        {
            _imageRotation = 0;
            _flipImageHorizontally = false;
            _flipImageVertically = false;
            ApplyImageOrientation();
        }

        internal void ApplyImageOrientation()
        {
            // 修复：旋转/翻转中心改为按“图像源尺寸 × 当前缩放”实时计算的可见区域中心，
            // 替代原 imageContainer.Width/2（resize 后漂移，且 Canvas.Width 常为 NaN 导致中心失效）。
            Size imageSize = ImageViewerImageSourceUtilities.TryGetSourceImageSize(ImageSource, out Size sourceSize) ? sourceSize : default;
            double centerX = (imageSize.Width * Scale) / 2;
            double centerY = (imageSize.Height * Scale) / 2;
            orientationScaleTransform.CenterX = centerX;
            orientationScaleTransform.CenterY = centerY;
            orientationScaleTransform.ScaleX = _flipImageHorizontally ? -1 : 1;
            orientationScaleTransform.ScaleY = _flipImageVertically ? -1 : 1;
            orientationRotateTransform.CenterX = centerX;
            orientationRotateTransform.CenterY = centerY;
            orientationRotateTransform.Angle = _imageRotation;
        }

        public void SetImage(ImageSource source)
        {
            ImageSource = source;
        }

        internal void SetImageLoadState(bool isLoading, string statusText, double progress, bool canRetry)
        {
            IsImageLoading = isLoading;
            ImageLoadStatusText = statusText;
            ImageLoadProgress = Math.Clamp(progress, 0, 100);
            CanRetryImageLoad = canRetry;
            ImageLoadHasError = canRetry && isLoading;
        }

        public Task RetryLastImageLoadAsync() => _controlComposition.DialogWorkflowService.RetryLastImageLoadAsync();

        public void Dispose()
        {
            _lifetime.Dispose();
            _controlComposition.Dispose();
            _ownedHost?.Dispose();
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            _lifetime.Dispose();
            if (_ownedHost != null)
            {
                await _ownedHost.DisposeAsync().ConfigureAwait(false);
            }

            GC.SuppressFinalize(this);
        }
    }
}
