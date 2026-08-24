using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageViewer.Controls;
using ImageViewer.Models;
using Microsoft.Extensions.DependencyInjection;

namespace RobotVision.WpfHost.Pages;

/// <summary>
/// 标定向导页 code-behind：
/// - 图像显示用 ImageViewerControl（滚轮缩放 / 中键平移 / 右键菜单），参数为浮动可折叠面板（参照相机管理页）；
/// - 左键点选：经内部 image 元素（承载缩放/平移 RenderTransform）的 GetPosition 直接得到图像像素坐标；
/// - 标定点标记：同步为 ViewerState 的 PointAnnotationRoi（锁定防拖动），随缩放/平移自动跟随重绘。
/// </summary>
public partial class CalibrationWizardPage : Page
{
    private readonly NotifyCollectionChangedEventHandler _pointsChanged;
    private CalibrationWizardViewModel? _vm;
    private System.Windows.Controls.Image? _imageElement;

    public CalibrationWizardPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService(typeof(CalibrationWizardViewModel));
        _vm = DataContext as CalibrationWizardViewModel;

        _pointsChanged = (_, _) => SyncPointAnnotations();

        // 页面可能被导航缓存复用：订阅统一在 Loaded 建立、Unloaded 拆除
        Loaded += (_, _) =>
        {
            _vm?.RefreshCameras();
            if (_vm is not null)
                _vm.Points.CollectionChanged += _pointsChanged;
        };
        Unloaded += (_, _) =>
        {
            if (_vm is not null)
                _vm.Points.CollectionChanged -= _pointsChanged;
            _vm?.ResetSession();
        };
    }

    /// <summary>
    /// 左键点选取点：ImageViewer 内部 image 元素带缩放/平移变换，GetPosition 相对它
    /// 即图像像素坐标（与控件内部交互同一换算路径）；越界（信箱区）忽略。
    /// </summary>
    private void OnViewerMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_vm is null || !_vm.Clickable)
            return;
        if (Viewer.ImageSource is not BitmapSource bitmap)
            return;

        _imageElement ??= FindImageElement(Viewer);
        if (_imageElement is null)
            return;

        var position = e.GetPosition(_imageElement);
        if (position.X < 0 || position.Y < 0 ||
            position.X >= bitmap.PixelWidth || position.Y >= bitmap.PixelHeight)
            return;

        _vm.AddPoint(position.X, position.Y);
    }

    /// <summary>视觉树中定位 ImageViewer 内部 x:Name="image" 的图像元素。</summary>
    private static System.Windows.Controls.Image? FindImageElement(DependencyObject node)
    {
        var count = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            if (child is System.Windows.Controls.Image { Name: "image" } image)
                return image;
            var found = FindImageElement(child);
            if (found is not null)
                return found;
        }
        return null;
    }

    /// <summary>
    /// 标定点 → PointAnnotationRoi 全量同步（点数 ≤9，全量重建成本可忽略）。
    /// 只增删本页创建的点标注，不动用户手动绘制的其他 ROI；锁定防误拖。
    /// </summary>
    private void SyncPointAnnotations()
    {
        var state = Viewer.ViewerState;
        foreach (var roi in state.AllRois.OfType<PointAnnotationRoi>().ToList())
            state.RemoveRoi(roi);

        if (_vm is null)
            return;

        foreach (var point in _vm.Points)
        {
            state.AddRoi(new PointAnnotationRoi
            {
                Position = new Point(point.PixelX, point.PixelY),
                Label = point.Index.ToString(),
                StrokeColor = Colors.Orange,
                IsLocked = true,
            });
        }
    }
}
