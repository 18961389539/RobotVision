using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using ImageViewer.Models;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Recipe;

/// <summary>
/// ImageViewer 检测/特征 ROI 与 <see cref="RecipeRoiEditor"/> 的双向同步（配方页与向导共用）。
/// </summary>
internal sealed class RecipeRoiLiveSync : IDisposable
{
    private enum LiveRoiKind { Detection, Template, Secondary }

    private readonly RecipeRoiEditor _roi;
    private readonly Func<IRoiViewport> _activeViewport;
    private readonly IReadOnlyList<IRoiViewport> _viewports;
    private readonly Func<Roi?> _detectionRoi;
    private readonly Func<Roi?> _templateRoi;
    private readonly Func<Roi?> _secondaryRoi;
    private readonly Func<bool> _usesFeatureTeachRoi;
    private readonly Func<bool> _showTemplateRoi;
    private readonly Func<bool> _showSecondaryRoi;

    private RotatedRect? _detectionRect;
    private RotatedRect? _templateRect;
    private RotatedRect? _secondaryRect;
    private LineMeasureRoi? _refineLineRoi;
    private readonly Func<RefineLine?> _refineLine;
    private readonly Func<bool> _usesRefineLine;
    private LiveRoiKind _drawTarget = LiveRoiKind.Detection;
    private bool _syncingRect;
    private bool _startDrawAfterGrab;
    private bool _startDrawTemplateAfterGrab;
    private bool _startDrawSecondaryAfterGrab;
    private bool _wired;

    public RecipeRoiLiveSync(
        RecipeRoiEditor roi,
        Func<IRoiViewport> activeViewport,
        IReadOnlyList<IRoiViewport> viewports,
        Func<Roi?> detectionRoi,
        Func<Roi?> templateRoi,
        Func<bool> usesFeatureTeachRoi,
        Func<bool> showTemplateRoi,
        Func<RefineLine?> refineLine,
        Func<bool> usesRefineLine,
        Func<Roi?>? secondaryRoi = null,
        Func<bool>? showSecondaryRoi = null)
    {
        _roi = roi;
        _activeViewport = activeViewport;
        _viewports = viewports;
        _detectionRoi = detectionRoi;
        _templateRoi = templateRoi;
        _usesFeatureTeachRoi = usesFeatureTeachRoi;
        _showTemplateRoi = showTemplateRoi;
        _refineLine = refineLine;
        _usesRefineLine = usesRefineLine;
        _secondaryRoi = secondaryRoi ?? (() => null);
        _showSecondaryRoi = showSecondaryRoi ?? (() => false);
    }

    public bool IsTemplateDrawTarget
    {
        get => _drawTarget == LiveRoiKind.Template;
        set => _drawTarget = value ? LiveRoiKind.Template : LiveRoiKind.Detection;
    }

    public void SetDrawTarget(RecipeRoiDrawKind kind) =>
        _drawTarget = kind switch
        {
            RecipeRoiDrawKind.Template => LiveRoiKind.Template,
            RecipeRoiDrawKind.SecondaryBlob => LiveRoiKind.Secondary,
            _ => LiveRoiKind.Detection,
        };

    public bool StartDrawAfterGrab
    {
        get => _startDrawAfterGrab;
        set => _startDrawAfterGrab = value;
    }

    public bool StartDrawTemplateAfterGrab
    {
        get => _startDrawTemplateAfterGrab;
        set => _startDrawTemplateAfterGrab = value;
    }

    public bool StartDrawSecondaryAfterGrab
    {
        get => _startDrawSecondaryAfterGrab;
        set => _startDrawSecondaryAfterGrab = value;
    }

    public void Wire()
    {
        if (_wired)
            return;
        _wired = true;
        _roi.PropertyChanged += OnRoiPropertyChanged;
        foreach (var viewport in _viewports)
        {
            viewport.RectRoisChanged += OnRoiCollectionChanged;
            viewport.LineRoisChanged += OnLineRoiCollectionChanged;
        }
    }

    public void Unwire()
    {
        if (!_wired)
            return;
        _wired = false;
        _roi.PropertyChanged -= OnRoiPropertyChanged;
        foreach (var viewport in _viewports)
        {
            viewport.RectRoisChanged -= OnRoiCollectionChanged;
            viewport.LineRoisChanged -= OnLineRoiCollectionChanged;
        }
        ClearAllLiveRects();
        ClearLiveLine();
    }

    public void Dispose() => Unwire();

    public void SyncFromRecipe() => SyncLiveRectsFromRecipe();

    public void StartRoiMode()
    {
        if (_drawTarget == LiveRoiKind.Template)
            _roi.EnsureFeatureRoiDrawable();
        else if (_drawTarget == LiveRoiKind.Secondary)
            _roi.UseSecondaryRoi = true;
        _activeViewport().StartRoiMode();
    }

    public void OnPreviewImageReady()
    {
        if (_roi.PreviewImage is null)
            return;
        SyncLiveRectsFromRecipe();
        if (_startDrawAfterGrab)
        {
            _startDrawAfterGrab = false;
            _drawTarget = LiveRoiKind.Detection;
            StartRoiMode();
        }
        else if (_startDrawTemplateAfterGrab)
        {
            _startDrawTemplateAfterGrab = false;
            if (_usesFeatureTeachRoi())
            {
                _drawTarget = LiveRoiKind.Template;
                StartRoiMode();
            }
        }
        else if (_startDrawSecondaryAfterGrab)
        {
            _startDrawSecondaryAfterGrab = false;
            if (_showSecondaryRoi())
            {
                _drawTarget = LiveRoiKind.Secondary;
                StartRoiMode();
            }
        }
    }

    public void OnViewContextChanged()
    {
        if (_roi.HasRoiRefFrame)
            SyncLiveRectsFromRecipe();
    }

    private void OnRoiPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(RecipeRoiEditor.PreviewImage):
                if (_roi.PreviewImage is not null)
                    OnPreviewImageReady();
                else
                    ClearAllLiveRects();
                break;
            case nameof(RecipeRoiEditor.RoiRefWidth):
                if (!_roi.HasRoiRefFrame)
                    ClearAllLiveRects();
                break;
            case nameof(RecipeRoiEditor.UseRoi):
                if (!_syncingRect && _roi.HasRoiRefFrame)
                {
                    if (_roi.UseRoi)
                        SyncLiveRectsFromRecipe();
                    else
                        ClearLiveRect(ref _detectionRect);
                }
                break;
            case nameof(RecipeRoiEditor.UseTemplateRoi):
                if (!_syncingRect && _roi.HasRoiRefFrame)
                {
                    if (_usesFeatureTeachRoi() && _roi.UseTemplateRoi)
                        SyncLiveRectsFromRecipe();
                    else
                        ClearLiveRect(ref _templateRect);
                }
                break;
            case nameof(RecipeRoiEditor.UseSecondaryRoi):
                if (!_syncingRect && _roi.HasRoiRefFrame)
                {
                    if (_showSecondaryRoi() && _roi.UseSecondaryRoi)
                        SyncLiveRectsFromRecipe();
                    else
                        ClearLiveRect(ref _secondaryRect);
                }
                break;
            case nameof(RecipeRoiEditor.HasRefineLine):
                if (!_syncingRect && _roi.HasRoiRefFrame)
                    SyncLiveLineFromRecipe();
                break;
            case nameof(RecipeRoiEditor.RoiX):
            case nameof(RecipeRoiEditor.RoiY):
            case nameof(RecipeRoiEditor.RoiWidth):
            case nameof(RecipeRoiEditor.RoiHeight):
            case nameof(RecipeRoiEditor.RoiPxX):
            case nameof(RecipeRoiEditor.RoiPxY):
            case nameof(RecipeRoiEditor.RoiPxWidth):
            case nameof(RecipeRoiEditor.RoiPxHeight):
                UpdateLiveRectFromRecipe(_detectionRect, _detectionRoi());
                break;
            case nameof(RecipeRoiEditor.TemplateRoiPxX):
            case nameof(RecipeRoiEditor.TemplateRoiPxY):
            case nameof(RecipeRoiEditor.TemplateRoiPxWidth):
            case nameof(RecipeRoiEditor.TemplateRoiPxHeight):
                if (_usesFeatureTeachRoi())
                    UpdateLiveRectFromRecipe(_templateRect, _templateRoi());
                break;
            case nameof(RecipeRoiEditor.SecondaryRoiPxX):
            case nameof(RecipeRoiEditor.SecondaryRoiPxY):
            case nameof(RecipeRoiEditor.SecondaryRoiPxWidth):
            case nameof(RecipeRoiEditor.SecondaryRoiPxHeight):
                if (_showSecondaryRoi())
                    UpdateLiveRectFromRecipe(_secondaryRect, _secondaryRoi());
                break;
        }
    }

    private void OnRoiCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_syncingRect)
            return;
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems?[0] is RotatedRect added)
        {
            if (ReferenceEquals(added, _detectionRect) ||
                ReferenceEquals(added, _templateRect) ||
                ReferenceEquals(added, _secondaryRect))
                return;
            if (_drawTarget == LiveRoiKind.Template)
                AdoptDrawnRect(added, ref _templateRect, LiveRoiKind.Template);
            else if (_drawTarget == LiveRoiKind.Secondary)
                AdoptDrawnRect(added, ref _secondaryRect, LiveRoiKind.Secondary);
            else
                AdoptDrawnRect(added, ref _detectionRect, LiveRoiKind.Detection);
        }
        else if (e.Action == NotifyCollectionChangedAction.Remove)
        {
            if (ReferenceEquals(e.OldItems?[0], _detectionRect))
                DetachRect(ref _detectionRect);
            else if (ReferenceEquals(e.OldItems?[0], _templateRect))
                DetachRect(ref _templateRect);
            else if (ReferenceEquals(e.OldItems?[0], _secondaryRect))
                DetachRect(ref _secondaryRect);
        }
        else if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            DetachRect(ref _detectionRect);
            DetachRect(ref _templateRect);
            DetachRect(ref _secondaryRect);
        }
    }

    private void AdoptDrawnRect(RotatedRect added, ref RotatedRect? slot, LiveRoiKind kind)
    {
        var previous = slot;
        DetachRect(ref slot);
        slot = added;
        StyleLiveRect(added, kind);
        added.PropertyChanged += OnLiveRectPropertyChanged;
        ApplyLiveRectToRecipe(added, kind);
        if (previous is not null)
            ScheduleRemoveStale(previous);
    }

    private void ScheduleRemoveStale(RotatedRect previous)
    {
        if (!_wired)
            return;
        if (ReferenceEquals(previous, _detectionRect) ||
            ReferenceEquals(previous, _templateRect) ||
            ReferenceEquals(previous, _secondaryRect))
            return;
        var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(() => RemoveRoiFromAll(previous)));
    }

    private void OnLiveRectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not RotatedRect rect)
            return;
        if (e.PropertyName == nameof(RotatedRect.Angle))
        {
            if (rect.Angle != 0)
            {
                _syncingRect = true;
                rect.Angle = 0;
                _syncingRect = false;
            }
            return;
        }

        if (!_syncingRect &&
            e.PropertyName is nameof(RotatedRect.Center) or nameof(RotatedRect.Width) or nameof(RotatedRect.Height))
            ApplyLiveRectToRecipe(rect, KindOf(rect));
    }

    private void ApplyLiveRectToRecipe(RotatedRect rect, LiveRoiKind kind)
    {
        _syncingRect = true;
        try
        {
            if (kind == LiveRoiKind.Template)
            {
                if (!_usesFeatureTeachRoi())
                    return;
                _roi.ApplyTemplateRoiFromRect(rect.Center.X, rect.Center.Y, rect.Width, rect.Height);
            }
            else if (kind == LiveRoiKind.Secondary)
            {
                if (!_showSecondaryRoi())
                    return;
                _roi.ApplySecondaryRoiFromRect(rect.Center.X, rect.Center.Y, rect.Width, rect.Height);
            }
            else
                _roi.ApplyRoiFromRect(rect.Center.X, rect.Center.Y, rect.Width, rect.Height);
        }
        finally
        {
            _syncingRect = false;
        }
    }

    private LiveRoiKind KindOf(RotatedRect rect)
    {
        if (ReferenceEquals(rect, _templateRect))
            return LiveRoiKind.Template;
        if (ReferenceEquals(rect, _secondaryRect))
            return LiveRoiKind.Secondary;
        return LiveRoiKind.Detection;
    }

    private void UpdateLiveRectFromRecipe(RotatedRect? rect, Roi? r)
    {
        if (_syncingRect || !_roi.HasRoiRefFrame || r is null || rect is null)
            return;
        _syncingRect = true;
        try
        {
            rect.Center = new Point(
                (r.X + r.Width / 2) * _roi.RoiRefWidth,
                (r.Y + r.Height / 2) * _roi.RoiRefHeight);
            rect.Width = r.Width * _roi.RoiRefWidth;
            rect.Height = r.Height * _roi.RoiRefHeight;
        }
        finally
        {
            _syncingRect = false;
        }
    }

    private void SyncLiveRectsFromRecipe()
    {
        if (!_roi.HasRoiRefFrame)
        {
            ClearAllLiveRects();
            return;
        }

        _syncingRect = true;
        try
        {
            ClearAllLiveRects();
            if (_detectionRoi() is { } det)
                _detectionRect = AddLiveRect(det, LiveRoiKind.Detection);
            if (_showTemplateRoi() && _templateRoi() is { } feat)
                _templateRect = AddLiveRect(feat, LiveRoiKind.Template);
            if (_showSecondaryRoi() && _secondaryRoi() is { } sec)
                _secondaryRect = AddLiveRect(sec, LiveRoiKind.Secondary);
        }
        finally
        {
            _syncingRect = false;
        }

        SyncLiveLineFromRecipe();
    }

    private void SyncLiveLineFromRecipe()
    {
        ClearLiveLine();
        if (!_roi.HasRoiRefFrame || !_usesRefineLine() || _refineLine() is not { } line)
            return;

        _syncingRect = true;
        try
        {
            var roi = new LineMeasureRoi
            {
                P1 = new Point(line.X1 * _roi.RoiRefWidth, line.Y1 * _roi.RoiRefHeight),
                P2 = new Point(line.X2 * _roi.RoiRefWidth, line.Y2 * _roi.RoiRefHeight),
            };
            StyleLiveLine(roi);
            roi.PropertyChanged += OnLiveLinePropertyChanged;
            _refineLineRoi = roi;
            _activeViewport().AddLineRoi(roi);
        }
        finally
        {
            _syncingRect = false;
        }
    }

    private void OnLineRoiCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_syncingRect || !_usesRefineLine())
            return;
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems?[0] is LineMeasureRoi added)
        {
            if (ReferenceEquals(added, _refineLineRoi))
                return;
            AdoptDrawnLine(added);
        }
        else if (e.Action == NotifyCollectionChangedAction.Remove)
        {
            if (ReferenceEquals(e.OldItems?[0], _refineLineRoi))
                DetachLine();
        }
        else if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            DetachLine();
        }
    }

    private void AdoptDrawnLine(LineMeasureRoi added)
    {
        var previous = _refineLineRoi;
        DetachLine();
        _refineLineRoi = added;
        StyleLiveLine(added);
        added.PropertyChanged += OnLiveLinePropertyChanged;
        ApplyLineToRecipe(added);
        if (previous is not null)
            ScheduleRemoveStaleLine(previous);
    }

    private void OnLiveLinePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not LineMeasureRoi line)
            return;
        if (!_syncingRect && ReferenceEquals(line, _refineLineRoi) &&
            e.PropertyName is nameof(LineMeasureRoi.P1) or nameof(LineMeasureRoi.P2))
            ApplyLineToRecipe(line);
    }

    private void ApplyLineToRecipe(LineMeasureRoi line)
    {
        _syncingRect = true;
        try
        {
            _roi.ApplyRefineLineFromPx(line.P1.X, line.P1.Y, line.P2.X, line.P2.Y);
        }
        finally
        {
            _syncingRect = false;
        }
    }

    private void ScheduleRemoveStaleLine(LineMeasureRoi previous)
    {
        if (!_wired || ReferenceEquals(previous, _refineLineRoi))
            return;
        var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(() => RemoveLineFromAll(previous)));
    }

    private static void StyleLiveLine(LineMeasureRoi roi)
    {
        roi.Label = "基准线";
        roi.StrokeColor = Colors.Red;
    }

    private void ClearLiveLine()
    {
        var old = _refineLineRoi;
        DetachLine();
        if (old is not null)
            RemoveLineFromAll(old);
    }

    private void DetachLine()
    {
        if (_refineLineRoi is { } line)
            line.PropertyChanged -= OnLiveLinePropertyChanged;
        _refineLineRoi = null;
    }

    private void RemoveLineFromAll(LineMeasureRoi line)
    {
        foreach (var viewport in _viewports)
            viewport.RemoveLineRoi(line);
    }

    private RotatedRect AddLiveRect(Roi r, LiveRoiKind kind)
    {
        var rect = new RotatedRect
        {
            Center = new Point(
                (r.X + r.Width / 2) * _roi.RoiRefWidth,
                (r.Y + r.Height / 2) * _roi.RoiRefHeight),
            Width = r.Width * _roi.RoiRefWidth,
            Height = r.Height * _roi.RoiRefHeight,
        };
        StyleLiveRect(rect, kind);
        rect.PropertyChanged += OnLiveRectPropertyChanged;
        _activeViewport().AddRoi(rect);
        return rect;
    }

    private void StyleLiveRect(RotatedRect rect, LiveRoiKind kind)
    {
        (rect.Label, rect.StrokeColor) = kind switch
        {
            LiveRoiKind.Template => ("特征", Colors.Orange),
            LiveRoiKind.Secondary => ("ROI2", Colors.DeepSkyBlue),
            LiveRoiKind.Detection when _roi.UseSecondaryRoi => ("ROI1", Colors.Lime),
            _ => ("检测", Colors.Lime),
        };
    }

    private void ClearAllLiveRects()
    {
        ClearLiveRect(ref _detectionRect);
        ClearLiveRect(ref _templateRect);
        ClearLiveRect(ref _secondaryRect);
    }

    private void ClearLiveRect(ref RotatedRect? slot)
    {
        var old = slot;
        DetachRect(ref slot);
        if (old is not null)
            RemoveRoiFromAll(old);
    }

    private void RemoveRoiFromAll(RotatedRect rect)
    {
        foreach (var viewport in _viewports)
            viewport.RemoveRoi(rect);
    }

    private void DetachRect(ref RotatedRect? slot)
    {
        if (slot is { } rect)
            rect.PropertyChanged -= OnLiveRectPropertyChanged;
        slot = null;
    }
}
