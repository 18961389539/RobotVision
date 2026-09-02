using System.Collections.Specialized;
using ImageViewer.Models;
using ViewerControl = ImageViewer.Controls.ImageViewer;

namespace RobotVision.WpfHost.Shared;

/// <summary>
/// 配方/标定页 ROI 视口抽象：隔离 <see cref="RecipeRoiLiveSync"/> 对 ImageViewer 控件的直接依赖。
/// </summary>
public interface IRoiViewport
{
    event NotifyCollectionChangedEventHandler? RectRoisChanged;

    void StartRoiMode();

    void AddRoi(RotatedRect rect);

    void RemoveRoi(RotatedRect rect);
}

/// <summary><see cref="ImageViewer"/> 的 <see cref="IRoiViewport"/> 适配器。</summary>
public sealed class ImageViewerRoiViewport(ViewerControl viewer) : IRoiViewport
{
    private readonly ViewerControl _viewer = viewer;

    public event NotifyCollectionChangedEventHandler? RectRoisChanged
    {
        add => _viewer.ViewerState.RectRois.CollectionChanged += value;
        remove => _viewer.ViewerState.RectRois.CollectionChanged -= value;
    }

    public void StartRoiMode() => _viewer.StartRoiMode();

    public void AddRoi(RotatedRect rect) => _viewer.ViewerState.AddRoi(rect);

    public void RemoveRoi(RotatedRect rect) => _viewer.ViewerState.RemoveRoi(rect);
}
