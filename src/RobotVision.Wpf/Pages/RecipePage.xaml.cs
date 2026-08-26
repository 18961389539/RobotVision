using Microsoft.Extensions.DependencyInjection;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ImageViewer.Controls;
using ImageViewer.Models;
using RobotVision.Infrastructure.Calibration;

namespace RobotVision.WpfHost.Pages;

public partial class RecipePage : Page
{
    private RecipeViewModel? _vm;
    private CalibrationManager? _calibration; // 比例档案（像素→毫米）——测量工具物理单位显示用
    private RotatedRect? _trackedRect;   // 图像区里代表配方 ROI 的活矩形（图像像素坐标）
    private bool _syncingRect;           // 矩形↔ViewModel 双向同步的重入闸
    private bool _startDrawAfterGrab;    // 无参考帧点『框选区域』：取帧完成后自动进入绘制模式
    private bool _roiWired;

    public RecipePage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService(typeof(RecipeViewModel));
        NumberBoxCommit.Bind(this, DataContext as RecipeViewModel);
        // 再次进入页面时刷新相机下拉（相机管理页增删相机后）
        Loaded += (_, _) =>
        {
            _vm = DataContext as RecipeViewModel;
            _vm?.RefreshCameras();
            _vm?.RefreshStationIds();
            _vm?.StartDirtyWatch();
            WireRoiEditor();
            _calibration ??= App.Services.GetRequiredService<CalibrationManager>();
            ApplyScaleToViewers();
        };
        Unloaded += (_, _) =>
        {
            UnwireRoiEditor();
            (DataContext as RecipeViewModel)?.StopDirtyWatch();
        };
    }

    /// <summary>按当前配方工位的比例档案设置 viewer 物理换算：测量工具（距离/面积标注）显示毫米，
    /// 无档案回落 px。PixelSize 是单标量，取 X 比例（各向异性档案在保存/加载时已有警告）。</summary>
    private void ApplyScaleToViewers()
    {
        var scale = _calibration?.GetScale(_vm?.Editor.StationId);
        foreach (var viewer in new[] { TestViewer, RoiViewer })
        {
            viewer.PixelSize = scale?.ScaleX ?? 1.0;
            viewer.PhysicalUnit = scale is null ? "px" : "mm";
        }
    }

    /// <summary>角度模式/精修方法切换：Editor 属性级变更不触发 OnEditorChanged，
    /// 此处通知 ViewModel 刷新按模式显隐的派生属性（IsDualMode/IsTemplateMethod 等）。</summary>
    private void AngleMode_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        (DataContext as RecipeViewModel)?.NotifyAngleModeChanged();

    // ---- ROI 框选/拖拽与配方数值双向同步 ----
    // ImageViewer 自带 ROI 绘制交互（StartRoiMode）与像素坐标的 RotatedRect 模型；
    // 页面层负责把『活矩形 ↔ Editor.Roi（比例）』接起来：图上拖拽回写配方，数值输入驱动矩形。

    private void OnDrawRoiRegionClick(object sender, RoutedEventArgs e)
    {
        if (_vm is null)
            return;
        _vm.ShowRoiPreviewViewCommand.Execute(null);
        if (_vm.HasRoiRefFrame)
        {
            RoiViewer.StartRoiMode();
            return;
        }
        // 尚无参考帧：先取一帧（尺寸基准），到位后再进绘制模式
        _startDrawAfterGrab = true;
        _vm.PreviewRoiCommand.Execute(null);
    }

    private void WireRoiEditor()
    {
        if (_vm is null || _roiWired)
            return;
        _roiWired = true;
        _vm.PropertyChanged += OnVmPropertyChanged;
        RoiViewer.ViewerState.RectRois.CollectionChanged += OnRoiCollectionChanged;
    }

    private void UnwireRoiEditor()
    {
        if (!_roiWired)
            return;
        _roiWired = false;
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;
        RoiViewer.ViewerState.RectRois.CollectionChanged -= OnRoiCollectionChanged;
        DetachTrackedRect();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_vm is null)
            return;
        switch (e.PropertyName)
        {
            case nameof(RecipeViewModel.Editor):
                // 新建/复制/加载配方：工位变了，比例档案随之重取
                ApplyScaleToViewers();
                break;
            case nameof(RecipeViewModel.RoiPreviewImage):
                if (_vm.RoiPreviewImage is not null)
                {
                    SyncRoiRectFromRecipe();
                    if (_startDrawAfterGrab)
                    {
                        _startDrawAfterGrab = false;
                        RoiViewer.StartRoiMode();
                    }
                }
                else
                {
                    ClearRoiRect();
                }
                break;
            case nameof(RecipeViewModel.RoiRefWidth):
                if (!_vm.HasRoiRefFrame)
                    ClearRoiRect();   // 换配方：参考帧失效，矩形随之清除
                break;
            case nameof(RecipeViewModel.UseRoi):
                // 回写路径（ApplyTrackedRectToRecipe）也会通知 UseRoi，必须用重入闸挡住，
                // 否则 通知→Sync→AddRoi→回写→通知 无限循环
                if (!_syncingRect && _vm.HasRoiRefFrame)
                {
                    if (_vm.UseRoi)
                        SyncRoiRectFromRecipe();
                    else
                        ClearRoiRect();
                }
                break;
            case nameof(RecipeViewModel.RoiX):
            case nameof(RecipeViewModel.RoiY):
            case nameof(RecipeViewModel.RoiWidth):
            case nameof(RecipeViewModel.RoiHeight):
            case nameof(RecipeViewModel.RoiPxX):
            case nameof(RecipeViewModel.RoiPxY):
            case nameof(RecipeViewModel.RoiPxWidth):
            case nameof(RecipeViewModel.RoiPxHeight):
                UpdateTrackedRectFromRecipe();
                break;
        }
    }

    /// <summary>框选提交（AddRoiCommand 落入集合）：收养新矩形替换旧矩形并回写配方。</summary>
    private void OnRoiCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems?[0] is RotatedRect added)
        {
            if (ReferenceEquals(added, _trackedRect))
                return;
            ClearRoiRect();
            _trackedRect = added;
            added.PropertyChanged += OnTrackedRectPropertyChanged;
            ApplyTrackedRectToRecipe();
        }
        else if (e.Action == NotifyCollectionChangedAction.Remove &&
                 ReferenceEquals(e.OldItems?[0], _trackedRect))
        {
            // 用户删了矩形（Delete/撤销）：解绑即可，配方保留最后数值
            DetachTrackedRect();
        }
        else if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            // 用户触发『清除所有 ROI』：直接解绑，配方保留最后数值
            DetachTrackedRect();
        }
    }

    private void OnTrackedRectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_trackedRect is not { } rect)
            return;
        if (e.PropertyName == nameof(RotatedRect.Angle))
        {
            // 配方 ROI 是轴对齐模型：旋转手柄拽了就弹回 0
            if (rect.Angle != 0)
            {
                _syncingRect = true;
                rect.Angle = 0;
                _syncingRect = false;
            }
            return;
        }
        if (!_syncingRect &&
            (e.PropertyName == nameof(RotatedRect.Center) ||
             e.PropertyName == nameof(RotatedRect.Width) ||
             e.PropertyName == nameof(RotatedRect.Height)))
        {
            ApplyTrackedRectToRecipe();
        }
    }

    /// <summary>矩形 → 配方：中心/宽高（像素）换算成比例 ROI 写回。</summary>
    private void ApplyTrackedRectToRecipe()
    {
        if (_vm is null || _trackedRect is not { } rect)
            return;
        _syncingRect = true;
        try
        {
            _vm.ApplyRoiFromRect(rect.Center.X, rect.Center.Y, rect.Width, rect.Height);
        }
        finally
        {
            _syncingRect = false;
        }
    }

    /// <summary>配方 → 矩形：数值输入驱动图像区矩形移动/缩放。</summary>
    private void UpdateTrackedRectFromRecipe()
    {
        if (_syncingRect || _vm is null || !_vm.HasRoiRefFrame || _vm.Editor.Roi is not { } r || _trackedRect is not { } rect)
            return;
        _syncingRect = true;
        try
        {
            rect.Center = new Point((r.X + r.Width / 2) * _vm.RoiRefWidth, (r.Y + r.Height / 2) * _vm.RoiRefHeight);
            rect.Width = r.Width * _vm.RoiRefWidth;
            rect.Height = r.Height * _vm.RoiRefHeight;
        }
        finally
        {
            _syncingRect = false;
        }
    }

    /// <summary>参考帧到位 / 启用开关打开：按配方 ROI 在图像区放一个活矩形。</summary>
    private void SyncRoiRectFromRecipe()
    {
        ClearRoiRect();
        if (_vm is null || !_vm.HasRoiRefFrame || !_vm.UseRoi || _vm.Editor.Roi is not { } r)
            return;
        var rect = new RotatedRect
        {
            Center = new Point((r.X + r.Width / 2) * _vm.RoiRefWidth, (r.Y + r.Height / 2) * _vm.RoiRefHeight),
            Width = r.Width * _vm.RoiRefWidth,
            Height = r.Height * _vm.RoiRefHeight,
        };
        RoiViewer.ViewerState.AddRoi(rect);
        _trackedRect = rect;
        rect.PropertyChanged += OnTrackedRectPropertyChanged;
    }

    /// <summary>从图像区移除活矩形（不动配方数值）。</summary>
    private void ClearRoiRect()
    {
        var old = _trackedRect;
        DetachTrackedRect();
        if (old is not null)
            RoiViewer.ViewerState.RemoveRoi(old);
    }

    private void DetachTrackedRect()
    {
        if (_trackedRect is { } rect)
            rect.PropertyChanged -= OnTrackedRectPropertyChanged;
        _trackedRect = null;
    }
}
