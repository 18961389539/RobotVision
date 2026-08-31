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
using RobotVision.WpfHost;

namespace RobotVision.WpfHost.Features.Recipe;

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

    /// <summary>有结果图且正在看结果页时，框选画在结果图上，避免切到 ROI 预览换图。</summary>
    private ImageViewer.Controls.ImageViewer LiveHost =>
        _vm is { ShowTestImage: true, Test.ResultImage: not null } ? TestViewer : RoiViewer;

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
        NotifyModeUiChanged();

    /// <summary>POCO 勾选（窗口配对 / Otsu）同样不走 Editor INPC，需手动刷新派生显隐。</summary>
    private void NotifyModeUiChanged(object sender, RoutedEventArgs e) => NotifyModeUiChanged();

    private void NotifyModeUiChanged() =>
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
        if (template && !_vm.UsesFeatureTeachRoi)
            return;
        _drawTarget = template ? LiveRoiKind.Template : LiveRoiKind.Detection;
        _startDrawAfterGrab = false;
        _startDrawTemplateAfterGrab = false;

        var adopted = _vm.Roi.TryAdoptDisplayedImage(
            _vm.Test.ResultImage,
            _vm.Editor.CameraId,
            template ? "框选特征：沿用当前结果图" : "框选检测区：沿用当前结果图");
        if (adopted)
        {
            _vm.ShowTestImageViewCommand.Execute(null);
            if (template)
                _vm.Roi.EnsureFeatureRoiDrawable();
            SyncLiveRectsFromRecipe();
            LiveHost.StartRoiMode();
            return;
        }

        _vm.ShowRoiPreviewViewCommand.Execute(null);
        if (_vm.Roi.HasRoiRefFrame)
        {
            if (template)
                _vm.Roi.EnsureFeatureRoiDrawable();
            LiveHost.StartRoiMode();
            return;
        }

        if (template)
            _startDrawTemplateAfterGrab = true;
        else
            _startDrawAfterGrab = true;
        _vm.Roi.PreviewRoiCommand.Execute(null);
    }

    private void WireRoiEditor()
    {
        if (_vm is null || _roiWired)
            return;
        _roiWired = true;
        _vm.PropertyChanged += OnVmPropertyChanged;
        _vm.Roi.PropertyChanged += OnRoiPropertyChanged;
        _vm.Test.PropertyChanged += OnTestPropertyChanged;
        RoiViewer.ViewerState.RectRois.CollectionChanged += OnRoiCollectionChanged;
        TestViewer.ViewerState.RectRois.CollectionChanged += OnRoiCollectionChanged;
    }

    private void UnwireRoiEditor()
    {
        if (!_roiWired)
            return;
        _roiWired = false;
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm.Roi.PropertyChanged -= OnRoiPropertyChanged;
            _vm.Test.PropertyChanged -= OnTestPropertyChanged;
        }
        RoiViewer.ViewerState.RectRois.CollectionChanged -= OnRoiCollectionChanged;
        TestViewer.ViewerState.RectRois.CollectionChanged -= OnRoiCollectionChanged;
        DetachRect(ref _detectionRect);
        DetachRect(ref _templateRect);
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_vm is null)
            return;
        if (e.PropertyName == nameof(RecipeViewModel.Editor))
            ApplyScaleToViewers();
        if (e.PropertyName == nameof(RecipeViewModel.ShowTestImage) && _vm.Roi.HasRoiRefFrame)
            SyncLiveRectsFromRecipe();
        if (e.PropertyName is nameof(RecipeViewModel.IsTemplateMethod)
            or nameof(RecipeViewModel.UsesFeatureTeachRoi)
            or nameof(RecipeViewModel.Editor))
        {
            if (_vm.Roi.HasRoiRefFrame)
                SyncLiveRectsFromRecipe();
        }
    }

    private void OnTestPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_vm is null || e.PropertyName != nameof(RecipeTestSession.ResultImage))
            return;
        if (_vm.ShowTestImage)
            _drawTarget = LiveRoiKind.Detection;
        if (_vm.Roi.HasRoiRefFrame)
            SyncLiveRectsFromRecipe();
    }

    private void OnRoiPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_vm is null)
            return;
        switch (e.PropertyName)
        {
            case nameof(RecipeRoiEditor.PreviewImage):
                if (_vm.Roi.PreviewImage is not null)
                {
                    SyncLiveRectsFromRecipe();
                    if (_startDrawAfterGrab)
                    {
                        _startDrawAfterGrab = false;
                        _drawTarget = LiveRoiKind.Detection;
                        LiveHost.StartRoiMode();
                    }
                    else if (_startDrawTemplateAfterGrab)
                    {
                        _startDrawTemplateAfterGrab = false;
                        if (_vm.UsesFeatureTeachRoi)
                        {
                            _drawTarget = LiveRoiKind.Template;
                            LiveHost.StartRoiMode();
                        }
                    }
                }
                else
                {
                    ClearAllLiveRects();
                }
                break;
            case nameof(RecipeRoiEditor.RoiRefWidth):
                if (!_vm.Roi.HasRoiRefFrame)
                    ClearAllLiveRects();
                break;
            case nameof(RecipeRoiEditor.UseRoi):
                if (!_syncingRect && _vm.Roi.HasRoiRefFrame)
                {
                    if (_vm.Roi.UseRoi)
                        SyncLiveRectsFromRecipe();
                    else
                        ClearLiveRect(ref _detectionRect);
                }
                break;
            case nameof(RecipeRoiEditor.UseTemplateRoi):
                if (!_syncingRect && _vm.Roi.HasRoiRefFrame)
                {
                    if (_vm.UsesFeatureTeachRoi && _vm.Roi.UseTemplateRoi)
                        SyncLiveRectsFromRecipe();
                    else
                        ClearLiveRect(ref _templateRect);
                }
                break;
            case nameof(RecipeRoiEditor.RoiX):
            case nameof(RecipeRoiEditor.RoiY):
            case nameof(RecipeRoiEditor.RoiWidth):
            case nameof(RecipeRoiEditor.RoiHeight):
            case nameof(RecipeRoiEditor.RoiPxX):
            case nameof(RecipeRoiEditor.RoiPxY):
            case nameof(RecipeRoiEditor.RoiPxWidth):
            case nameof(RecipeRoiEditor.RoiPxHeight):
                UpdateLiveRectFromRecipe(_detectionRect, _vm.Editor.Roi);
                break;
            case nameof(RecipeRoiEditor.TemplateRoiPxX):
            case nameof(RecipeRoiEditor.TemplateRoiPxY):
            case nameof(RecipeRoiEditor.TemplateRoiPxWidth):
            case nameof(RecipeRoiEditor.TemplateRoiPxHeight):
                if (_vm.UsesFeatureTeachRoi)
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
        TestViewer.ViewerState.RemoveRoi(previous);
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
            {
                if (!_vm.UsesFeatureTeachRoi)
                    return;
                _vm.Roi.ApplyTemplateRoiFromRect(rect.Center.X, rect.Center.Y, rect.Width, rect.Height);
            }
            else
                _vm.Roi.ApplyRoiFromRect(rect.Center.X, rect.Center.Y, rect.Width, rect.Height);
        }
        finally
        {
            _syncingRect = false;
        }
    }

    private void UpdateLiveRectFromRecipe(RotatedRect? rect, RobotVision.Core.Models.Roi? r)
    {
        if (_syncingRect || _vm is null || !_vm.Roi.HasRoiRefFrame || r is null || rect is null)
            return;
        _syncingRect = true;
        try
        {
            rect.Center = new Point((r.X + r.Width / 2) * _vm.Roi.RoiRefWidth, (r.Y + r.Height / 2) * _vm.Roi.RoiRefHeight);
            rect.Width = r.Width * _vm.Roi.RoiRefWidth;
            rect.Height = r.Height * _vm.Roi.RoiRefHeight;
        }
        finally
        {
            _syncingRect = false;
        }
    }

    /// <summary>参考帧到位 / 启用开关打开：按配方在图像区放检测框与特征框。</summary>
    private void SyncLiveRectsFromRecipe()
    {
        if (_vm is null || !_vm.Roi.HasRoiRefFrame)
        {
            ClearAllLiveRects();
            return;
        }

        _syncingRect = true;
        try
        {
            ClearAllLiveRects();
            if (_vm.Roi.UseRoi && _vm.Editor.Roi is { } det)
                _detectionRect = AddLiveRect(det, isTemplate: false);
            if (ShowTeachFeatureRect() && _vm.Editor.Template?.Roi is { } feat)
                _templateRect = AddLiveRect(feat, isTemplate: true);
        }
        finally
        {
            _syncingRect = false;
        }
    }

    /// <summary>橙框只在示教预览或正在「框选特征」时显示；测试/监控结果只看金框匹配窗。</summary>
    private bool ShowTeachFeatureRect()
    {
        if (_vm is null || !_vm.UsesFeatureTeachRoi || !_vm.Roi.UseTemplateRoi)
            return false;
        if (_vm.ShowTestImage && _vm.Test.ResultImage is not null && _drawTarget != LiveRoiKind.Template)
            return false;
        return true;
    }

    private RotatedRect AddLiveRect(RobotVision.Core.Models.Roi r, bool isTemplate)
    {
        var rect = new RotatedRect
        {
            Center = new Point((r.X + r.Width / 2) * _vm!.Roi.RoiRefWidth, (r.Y + r.Height / 2) * _vm.Roi.RoiRefHeight),
            Width = r.Width * _vm.Roi.RoiRefWidth,
            Height = r.Height * _vm.Roi.RoiRefHeight,
        };
        StyleLiveRect(rect, isTemplate);
        rect.PropertyChanged += OnLiveRectPropertyChanged;
        LiveHost.ViewerState.AddRoi(rect);
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
        {
            RoiViewer.ViewerState.RemoveRoi(old);
            TestViewer.ViewerState.RemoveRoi(old);
        }
    }

    private void DetachRect(ref RotatedRect? slot)
    {
        if (slot is { } rect)
            rect.PropertyChanged -= OnLiveRectPropertyChanged;
        slot = null;
    }
}
