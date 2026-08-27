using Microsoft.Extensions.DependencyInjection;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ImageViewer.Controls;
using ImageViewer.Models;
using RobotVision.Infrastructure.Calibration;

namespace RobotVision.WpfHost.Pages;

public partial class RecipePage : Page
{
    private enum LiveRoiKind { Detection, Template }

    private RecipeViewModel? _vm;
    private CalibrationManager? _calibration; // 比例档案（像素→毫米）——测量工具物理单位显示用
    private RotatedRect? _detectionRect; // 检测 ROI（TRIGGER 搜索窗）
    private RotatedRect? _templateRect;  // 示教特征 ROI（不参与 TRIGGER）
    private LiveRoiKind _drawTarget = LiveRoiKind.Detection;
    private bool _syncingRect;           // 矩形↔ViewModel 双向同步的重入闸
    private bool _startDrawAfterGrab;    // 无参考帧点『框选区域』：取帧完成后自动进入绘制模式
    private bool _startDrawTemplateAfterGrab;
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
    // 页面层负责把『活矩形 ↔ Editor.Roi / Template.Roi（比例）』接起来。

    private void OnDrawRoiRegionClick(object sender, RoutedEventArgs e) =>
        BeginRoiDraw(template: false);

    private void OnDrawTemplateRoiClick(object sender, RoutedEventArgs e) =>
        BeginRoiDraw(template: true);

    private void BeginRoiDraw(bool template)
    {
        if (_vm is null)
            return;
        _drawTarget = template ? LiveRoiKind.Template : LiveRoiKind.Detection;
        _startDrawAfterGrab = false;
        _startDrawTemplateAfterGrab = false;
        _vm.ShowRoiPreviewViewCommand.Execute(null);
        if (_vm.HasRoiRefFrame)
        {
            RoiViewer.StartRoiMode();
            return;
        }
        if (template)
            _startDrawTemplateAfterGrab = true;
        else
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
        DetachRect(ref _detectionRect);
        DetachRect(ref _templateRect);
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
                    SyncLiveRectsFromRecipe();
                    if (_startDrawAfterGrab)
                    {
                        _startDrawAfterGrab = false;
                        _drawTarget = LiveRoiKind.Detection;
                        RoiViewer.StartRoiMode();
                    }
                    else if (_startDrawTemplateAfterGrab)
                    {
                        _startDrawTemplateAfterGrab = false;
                        _drawTarget = LiveRoiKind.Template;
                        RoiViewer.StartRoiMode();
                    }
                }
                else
                {
                    ClearAllLiveRects();
                }
                break;
            case nameof(RecipeViewModel.RoiRefWidth):
                if (!_vm.HasRoiRefFrame)
                    ClearAllLiveRects();   // 换配方：参考帧失效，矩形随之清除
                break;
            case nameof(RecipeViewModel.UseRoi):
                // 回写路径也会通知 UseRoi，必须用重入闸挡住，否则 通知→Sync→AddRoi→回写 循环
                if (!_syncingRect && _vm.HasRoiRefFrame)
                {
                    if (_vm.UseRoi)
                        SyncLiveRectsFromRecipe();
                    else
                        ClearLiveRect(ref _detectionRect);
                }
                break;
            case nameof(RecipeViewModel.UseTemplateRoi):
                if (!_syncingRect && _vm.HasRoiRefFrame)
                {
                    if (_vm.UseTemplateRoi)
                        SyncLiveRectsFromRecipe();
                    else
                        ClearLiveRect(ref _templateRect);
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
                UpdateLiveRectFromRecipe(_detectionRect, _vm.Editor.Roi);
                break;
            case nameof(RecipeViewModel.TemplateRoiPxX):
            case nameof(RecipeViewModel.TemplateRoiPxY):
            case nameof(RecipeViewModel.TemplateRoiPxWidth):
            case nameof(RecipeViewModel.TemplateRoiPxHeight):
                UpdateLiveRectFromRecipe(_templateRect, _vm.Editor.Template?.Roi);
                break;
        }
    }

    /// <summary>框选提交：按当前绘制目标收养新矩形，不拆掉另一种 ROI。</summary>
    private void OnRoiCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_syncingRect)
            return;
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems?[0] is RotatedRect added)
        {
            if (ReferenceEquals(added, _detectionRect) || ReferenceEquals(added, _templateRect))
                return;
            if (_drawTarget == LiveRoiKind.Template)
                AdoptDrawnRect(added, ref _templateRect, isTemplate: true);
            else
                AdoptDrawnRect(added, ref _detectionRect, isTemplate: false);
        }
        else if (e.Action == NotifyCollectionChangedAction.Remove)
        {
            if (ReferenceEquals(e.OldItems?[0], _detectionRect))
                DetachRect(ref _detectionRect);
            else if (ReferenceEquals(e.OldItems?[0], _templateRect))
                DetachRect(ref _templateRect);
        }
        else if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            DetachRect(ref _detectionRect);
            DetachRect(ref _templateRect);
        }
    }

    private void AdoptDrawnRect(RotatedRect added, ref RotatedRect? slot, bool isTemplate)
    {
        var previous = slot;
        DetachRect(ref slot);
        slot = added;
        StyleLiveRect(added, isTemplate);
        added.PropertyChanged += OnLiveRectPropertyChanged;
        ApplyLiveRectToRecipe(added, isTemplate);
        if (previous is not null)
        {
            var stale = previous;
            Dispatcher.BeginInvoke(
                DispatcherPriority.DataBind,
                new Action(() => RemoveStaleRoi(stale)));
        }
    }

    /// <summary>在 Add 事件栈外移除被替换的旧矩形，避免 ObservableCollection 重入。</summary>
    private void RemoveStaleRoi(RotatedRect previous)
    {
        if (!_roiWired)
            return;
        if (ReferenceEquals(previous, _detectionRect) || ReferenceEquals(previous, _templateRect))
            return;
        RoiViewer.ViewerState.RemoveRoi(previous);
    }

    private void OnLiveRectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not RotatedRect rect)
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
            ApplyLiveRectToRecipe(rect, ReferenceEquals(rect, _templateRect));
        }
    }

    private void ApplyLiveRectToRecipe(RotatedRect rect, bool isTemplate)
    {
        if (_vm is null)
            return;
        _syncingRect = true;
        try
        {
            if (isTemplate)
                _vm.ApplyTemplateRoiFromRect(rect.Center.X, rect.Center.Y, rect.Width, rect.Height);
            else
                _vm.ApplyRoiFromRect(rect.Center.X, rect.Center.Y, rect.Width, rect.Height);
        }
        finally
        {
            _syncingRect = false;
        }
    }

    private void UpdateLiveRectFromRecipe(RotatedRect? rect, RobotVision.Core.Models.Roi? r)
    {
        if (_syncingRect || _vm is null || !_vm.HasRoiRefFrame || r is null || rect is null)
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

    /// <summary>参考帧到位 / 启用开关打开：按配方在图像区放检测框与特征框。</summary>
    private void SyncLiveRectsFromRecipe()
    {
        if (_vm is null || !_vm.HasRoiRefFrame)
        {
            ClearAllLiveRects();
            return;
        }

        _syncingRect = true;
        try
        {
            ClearAllLiveRects();
            if (_vm.UseRoi && _vm.Editor.Roi is { } det)
                _detectionRect = AddLiveRect(det, isTemplate: false);
            if (_vm.UseTemplateRoi && _vm.Editor.Template?.Roi is { } feat)
                _templateRect = AddLiveRect(feat, isTemplate: true);
        }
        finally
        {
            _syncingRect = false;
        }
    }

    private RotatedRect AddLiveRect(RobotVision.Core.Models.Roi r, bool isTemplate)
    {
        var rect = new RotatedRect
        {
            Center = new Point((r.X + r.Width / 2) * _vm!.RoiRefWidth, (r.Y + r.Height / 2) * _vm.RoiRefHeight),
            Width = r.Width * _vm.RoiRefWidth,
            Height = r.Height * _vm.RoiRefHeight,
        };
        StyleLiveRect(rect, isTemplate);
        rect.PropertyChanged += OnLiveRectPropertyChanged;
        RoiViewer.ViewerState.AddRoi(rect);
        return rect;
    }

    private static void StyleLiveRect(RotatedRect rect, bool isTemplate)
    {
        rect.Label = isTemplate ? "特征" : "检测";
        rect.StrokeColor = isTemplate ? Colors.Orange : Colors.Lime;
    }

    private void ClearAllLiveRects()
    {
        ClearLiveRect(ref _detectionRect);
        ClearLiveRect(ref _templateRect);
    }

    private void ClearLiveRect(ref RotatedRect? slot)
    {
        var old = slot;
        DetachRect(ref slot);
        if (old is not null)
            RoiViewer.ViewerState.RemoveRoi(old);
    }

    private void DetachRect(ref RotatedRect? slot)
    {
        if (slot is { } rect)
            rect.PropertyChanged -= OnLiveRectPropertyChanged;
        slot = null;
    }
}
