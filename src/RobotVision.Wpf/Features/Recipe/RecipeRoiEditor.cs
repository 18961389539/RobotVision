using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;

namespace RobotVision.WpfHost.Features.Recipe;

/// <summary>检测/示教 ROI：比例存储、像素换算、参考帧预览。脏标记回写工作区。</summary>
public sealed partial class RecipeRoiEditor : ObservableObject
{
    private readonly IRecipeWorkspace _host;
    private readonly ICameraRuntime _cameras;
    private readonly ICalibrationRuntime _calibration;
    private readonly ILightingRuntime _lighting;
    private string? _roiFrameCameraId;

    internal RecipeRoiEditor(
        IRecipeWorkspace host,
        ICameraRuntime cameras,
        ICalibrationRuntime calibration,
        ILightingRuntime lighting)
    {
        _host = host;
        _cameras = cameras;
        _calibration = calibration;
        _lighting = lighting;
    }

    private RecipeConfig Editor => _host.Editor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRoiRefFrame))]
    [NotifyPropertyChangedFor(nameof(RoiPxX), nameof(RoiPxY), nameof(RoiPxWidth), nameof(RoiPxHeight))]
    [NotifyPropertyChangedFor(nameof(RoiRefFrameHint), nameof(RoiRatioHint))]
    private int _roiRefWidth;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRoiRefFrame))]
    [NotifyPropertyChangedFor(nameof(RoiPxX), nameof(RoiPxY), nameof(RoiPxWidth), nameof(RoiPxHeight))]
    [NotifyPropertyChangedFor(nameof(RoiRefFrameHint), nameof(RoiRatioHint))]
    private int _roiRefHeight;

    [ObservableProperty]
    private ImageSource? _previewImage;

    public bool HasRoiRefFrame => RoiRefWidth > 0 && RoiRefHeight > 0;

    public string RoiRefFrameHint => HasRoiRefFrame
        ? $"参考帧 {RoiRefWidth}×{RoiRefHeight}px（{_roiFrameCameraId}）"
        : "";

    public string RoiRatioHint
    {
        get
        {
            if (Editor.Roi is not { } r)
                return "";
            var edge = HasRoiRefFrame
                ? $" · 右缘 {(r.X + r.Width) * RoiRefWidth:0}/{RoiRefWidth}px · 下缘 {(r.Y + r.Height) * RoiRefHeight:0}/{RoiRefHeight}px"
                : "";
            return $"存储比例 ({r.X:0.000}, {r.Y:0.000}, {r.Width:0.000}, {r.Height:0.000}){edge}";
        }
    }

    public bool UseRoi
    {
        get => Editor.Roi is not null;
        set
        {
            if (value)
                Editor.Roi ??= new Roi(0, 0, 1, 1);
            else
                Editor.Roi = null;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RoiRatioHint));
            _host.NotifyDirty();
        }
    }

    public double RoiPxX
    {
        get => Editor.Roi is { } r && RoiRefWidth > 0 ? Math.Round(r.X * RoiRefWidth) : 0;
        set
        {
            if (RoiRefWidth <= 0 || Editor.Roi is not { } r)
                return;
            var w = Math.Max(1, (int)Math.Round(r.Width * RoiRefWidth));
            var px = Math.Clamp((int)Math.Round(value), 0, Math.Max(0, RoiRefWidth - w));
            SetRoi(nameof(RoiPxX), x => x with { X = px / (double)RoiRefWidth });
        }
    }

    public double RoiPxY
    {
        get => Editor.Roi is { } r && RoiRefHeight > 0 ? Math.Round(r.Y * RoiRefHeight) : 0;
        set
        {
            if (RoiRefHeight <= 0 || Editor.Roi is not { } r)
                return;
            var h = Math.Max(1, (int)Math.Round(r.Height * RoiRefHeight));
            var px = Math.Clamp((int)Math.Round(value), 0, Math.Max(0, RoiRefHeight - h));
            SetRoi(nameof(RoiPxY), v => v with { Y = px / (double)RoiRefHeight });
        }
    }

    public double RoiPxWidth
    {
        get => Editor.Roi is { } r && RoiRefWidth > 0 ? Math.Round(r.Width * RoiRefWidth) : 0;
        set
        {
            if (RoiRefWidth <= 0 || Editor.Roi is not { } r)
                return;
            var x = (int)Math.Round(r.X * RoiRefWidth);
            var px = Math.Clamp((int)Math.Round(value), 1, Math.Max(1, RoiRefWidth - x));
            SetRoi(nameof(RoiPxWidth), v => v with { Width = px / (double)RoiRefWidth });
        }
    }

    public double RoiPxHeight
    {
        get => Editor.Roi is { } r && RoiRefHeight > 0 ? Math.Round(r.Height * RoiRefHeight) : 0;
        set
        {
            if (RoiRefHeight <= 0 || Editor.Roi is not { } r)
                return;
            var y = (int)Math.Round(r.Y * RoiRefHeight);
            var px = Math.Clamp((int)Math.Round(value), 1, Math.Max(1, RoiRefHeight - y));
            SetRoi(nameof(RoiPxHeight), v => v with { Height = px / (double)RoiRefHeight });
        }
    }

    public double RoiX
    {
        get => Editor.Roi?.X ?? 0;
        set => SetRoi(nameof(RoiX), r => r with { X = Math.Clamp(value, 0, Math.Max(0, 1 - r.Width)) });
    }

    public double RoiY
    {
        get => Editor.Roi?.Y ?? 0;
        set => SetRoi(nameof(RoiY), r => r with { Y = Math.Clamp(value, 0, Math.Max(0, 1 - r.Height)) });
    }

    public double RoiWidth
    {
        get => Editor.Roi?.Width ?? 1;
        set => SetRoi(nameof(RoiWidth), r =>
        {
            var max = Math.Max(0, 1 - r.X);
            return r with { Width = Math.Clamp(value, Math.Min(0.01, max), max) };
        });
    }

    public double RoiHeight
    {
        get => Editor.Roi?.Height ?? 1;
        set => SetRoi(nameof(RoiHeight), r =>
        {
            var max = Math.Max(0, 1 - r.Y);
            return r with { Height = Math.Clamp(value, Math.Min(0.01, max), max) };
        });
    }

    public bool UseTemplateRoi
    {
        get => Editor.Template?.Roi is not null;
        set
        {
            if (value)
            {
                Editor.Template ??= new();
                Editor.Template.Roi ??= new Roi(0.35, 0.35, 0.3, 0.3);
            }
            else if (Editor.Template is not null)
                Editor.Template.Roi = null;
            NotifyTemplateRoiChanged();
        }
    }

    public double TemplateRoiPxX
    {
        get => Editor.Template?.Roi is { } r && RoiRefWidth > 0 ? Math.Round(r.X * RoiRefWidth) : 0;
        set
        {
            if (RoiRefWidth <= 0 || Editor.Template?.Roi is not { } r)
                return;
            var w = Math.Max(1, (int)Math.Round(r.Width * RoiRefWidth));
            var px = Math.Clamp((int)Math.Round(value), 0, Math.Max(0, RoiRefWidth - w));
            SetTemplateRoi(nameof(TemplateRoiPxX), x => x with { X = px / (double)RoiRefWidth });
        }
    }

    public double TemplateRoiPxY
    {
        get => Editor.Template?.Roi is { } r && RoiRefHeight > 0 ? Math.Round(r.Y * RoiRefHeight) : 0;
        set
        {
            if (RoiRefHeight <= 0 || Editor.Template?.Roi is not { } r)
                return;
            var h = Math.Max(1, (int)Math.Round(r.Height * RoiRefHeight));
            var px = Math.Clamp((int)Math.Round(value), 0, Math.Max(0, RoiRefHeight - h));
            SetTemplateRoi(nameof(TemplateRoiPxY), v => v with { Y = px / (double)RoiRefHeight });
        }
    }

    public double TemplateRoiPxWidth
    {
        get => Editor.Template?.Roi is { } r && RoiRefWidth > 0 ? Math.Round(r.Width * RoiRefWidth) : 0;
        set
        {
            if (RoiRefWidth <= 0 || Editor.Template?.Roi is not { } r)
                return;
            var x = (int)Math.Round(r.X * RoiRefWidth);
            var px = Math.Clamp((int)Math.Round(value), 1, Math.Max(1, RoiRefWidth - x));
            SetTemplateRoi(nameof(TemplateRoiPxWidth), v => v with { Width = px / (double)RoiRefWidth });
        }
    }

    public double TemplateRoiPxHeight
    {
        get => Editor.Template?.Roi is { } r && RoiRefHeight > 0 ? Math.Round(r.Height * RoiRefHeight) : 0;
        set
        {
            if (RoiRefHeight <= 0 || Editor.Template?.Roi is not { } r)
                return;
            var y = (int)Math.Round(r.Y * RoiRefHeight);
            var px = Math.Clamp((int)Math.Round(value), 1, Math.Max(1, RoiRefHeight - y));
            SetTemplateRoi(nameof(TemplateRoiPxHeight), v => v with { Height = px / (double)RoiRefHeight });
        }
    }

    public void ApplyRoiFromRect(double centerXPx, double centerYPx, double widthPx, double heightPx)
    {
        if (RoiRefWidth <= 0 || RoiRefHeight <= 0)
            return;
        Editor.Roi = RoiFromCenterPx(centerXPx, centerYPx, widthPx, heightPx, RoiRefWidth, RoiRefHeight);
        NotifyRoiChanged();
    }

    public void ApplyTemplateRoiFromRect(double centerXPx, double centerYPx, double widthPx, double heightPx)
    {
        if (RoiRefWidth <= 0 || RoiRefHeight <= 0)
            return;
        Editor.Template ??= new();
        Editor.Template.Roi = RoiFromCenterPx(centerXPx, centerYPx, widthPx, heightPx, RoiRefWidth, RoiRefHeight);
        NotifyTemplateRoiChanged();
    }

    /// <summary>
    /// 用当前结果图当框选底板，不再 Grab。文件夹相机会进下一张，看起来像换图。
    /// <paramref name="keepCurrentPreview"/> 为 true 时不改 PreviewImage，避免切到 ROI 预览页。
    /// </summary>
    public bool TryAdoptDisplayedImage(
        ImageSource? source, string? cameraId, string reason, bool keepCurrentPreview = false)
    {
        if (source is not BitmapSource bmp || bmp.PixelWidth < 8 || bmp.PixelHeight < 8)
            return false;

        _roiFrameCameraId = string.IsNullOrWhiteSpace(cameraId) ? Editor.CameraId : cameraId;
        RoiRefWidth = bmp.PixelWidth;
        RoiRefHeight = bmp.PixelHeight;
        if (!keepCurrentPreview && !ReferenceEquals(PreviewImage, bmp))
            PreviewImage = bmp;
        EnsureFeatureRoiDrawable();
        _host.Message = $"{reason} {RoiRefWidth}×{RoiRefHeight}px，可直接框选（未重新取图）";
        return true;
    }

    /// <summary>1×1 这类退化框无法拖，换成画面中部占位，等用户拖出真正的特征。</summary>
    public void EnsureFeatureRoiDrawable()
    {
        if (Editor.Template?.Roi is not { } r || RoiRefWidth < 8 || RoiRefHeight < 8)
            return;
        var w = r.Width * RoiRefWidth;
        var h = r.Height * RoiRefHeight;
        if (w >= 8 && h >= 8)
            return;
        Editor.Template.Roi = new Roi(0.35, 0.35, 0.3, 0.3);
        NotifyTemplateRoiChanged();
    }

    public void ClearReferenceFrame()
    {
        _roiFrameCameraId = null;
        RoiRefWidth = 0;
        RoiRefHeight = 0;
    }

    /// <summary>换配方时仅在与参考帧相机不一致时清空，避免同相机重复取预览。</summary>
    public void MaybeClearReferenceFrameForCamera(string? cameraId)
    {
        if (_roiFrameCameraId is null)
            return;
        if (string.IsNullOrWhiteSpace(cameraId) ||
            !string.Equals(_roiFrameCameraId, cameraId, StringComparison.OrdinalIgnoreCase))
            ClearReferenceFrame();
    }

    public void NotifyFromEditor()
    {
        OnPropertyChanged(nameof(UseRoi));
        OnPropertyChanged(nameof(RoiX));
        OnPropertyChanged(nameof(RoiY));
        OnPropertyChanged(nameof(RoiWidth));
        OnPropertyChanged(nameof(RoiHeight));
        OnPropertyChanged(nameof(HasRoiRefFrame));
        OnPropertyChanged(nameof(RoiPxX));
        OnPropertyChanged(nameof(RoiPxY));
        OnPropertyChanged(nameof(RoiPxWidth));
        OnPropertyChanged(nameof(RoiPxHeight));
        OnPropertyChanged(nameof(RoiRefFrameHint));
        OnPropertyChanged(nameof(RoiRatioHint));
        OnPropertyChanged(nameof(UseTemplateRoi));
        OnPropertyChanged(nameof(TemplateRoiPxX));
        OnPropertyChanged(nameof(TemplateRoiPxY));
        OnPropertyChanged(nameof(TemplateRoiPxWidth));
        OnPropertyChanged(nameof(TemplateRoiPxHeight));
    }

    public void NotifyCanExecuteChanged() => PreviewRoiCommand.NotifyCanExecuteChanged();

    private bool CanPreviewRoi => !_host.IsBusy;

    [RelayCommand(CanExecute = nameof(CanPreviewRoi))]
    private async Task PreviewRoiAsync()
    {
        if (_host.IsBusy)
            return;
        _host.CommitEdits();
        var cameraId = Editor.CameraId;
        if (string.IsNullOrWhiteSpace(cameraId))
        {
            _host.Message = "ROI 预览：请先选择相机";
            return;
        }

        _host.IsBusy = true;
        try
        {
            var roi = Editor.Roi;
            _host.Message = $"ROI 预览取图中 · {cameraId} …（推理图像空间：光源+去畸变/工位映射）";
            var (frame, width, height) = await RecipeEditorFrame.GrabPreviewAsync(
                _cameras, _calibration, _lighting, Editor);
            _roiFrameCameraId = cameraId;
            RoiRefWidth = width;
            RoiRefHeight = height;
            PreviewImage = frame;
            _host.Message = roi is null
                ? $"ROI 预览（{cameraId}）{width}×{height}px：当前为全图推理，可直接框选区域"
                : $"ROI 预览（{cameraId}）{width}×{height}px：({roi.X * width:0},{roi.Y * height:0}) ~ " +
                  $"({(roi.X + roi.Width) * width:0},{(roi.Y + roi.Height) * height:0}) px · " +
                  $"{roi.Width * width:0}×{roi.Height * height:0}px（存储比例 {roi.X:0.000},{roi.Y:0.000},{roi.Width:0.000},{roi.Height:0.000}）";
        }
        catch (Exception ex)
        {
            _host.Message = $"ROI 预览失败: {ex.Message}";
        }
        finally
        {
            _host.IsBusy = false;
        }
    }

    private static Roi RoiFromCenterPx(
        double centerXPx, double centerYPx, double widthPx, double heightPx, int refW, int refH)
    {
        var w = Math.Clamp(widthPx, 1, refW) / refW;
        var h = Math.Clamp(heightPx, 1, refH) / refH;
        var x = Math.Clamp((centerXPx - widthPx / 2) / refW, 0, 1 - w);
        var y = Math.Clamp((centerYPx - heightPx / 2) / refH, 0, 1 - h);
        return new Roi(x, y, w, h);
    }

    private void SetRoi(string propertyName, Func<Roi, Roi> update)
    {
        if (Editor.Roi is { } roi)
        {
            Editor.Roi = update(roi);
            NotifyRoiChanged(propertyName);
        }
    }

    private void NotifyRoiChanged(string? callerProperty = null)
    {
        OnPropertyChanged(nameof(UseRoi));
        OnPropertyChanged(nameof(RoiX));
        OnPropertyChanged(nameof(RoiY));
        OnPropertyChanged(nameof(RoiWidth));
        OnPropertyChanged(nameof(RoiHeight));
        OnPropertyChanged(nameof(RoiPxX));
        OnPropertyChanged(nameof(RoiPxY));
        OnPropertyChanged(nameof(RoiPxWidth));
        OnPropertyChanged(nameof(RoiPxHeight));
        OnPropertyChanged(nameof(RoiRatioHint));
        _host.NotifyDirty();
        if (callerProperty != null)
            OnPropertyChanged(callerProperty);
    }

    private void SetTemplateRoi(string propertyName, Func<Roi, Roi> update)
    {
        if (Editor.Template?.Roi is { } roi)
        {
            Editor.Template.Roi = update(roi);
            NotifyTemplateRoiChanged(propertyName);
        }
    }

    private void NotifyTemplateRoiChanged(string? callerProperty = null)
    {
        OnPropertyChanged(nameof(UseTemplateRoi));
        OnPropertyChanged(nameof(TemplateRoiPxX));
        OnPropertyChanged(nameof(TemplateRoiPxY));
        OnPropertyChanged(nameof(TemplateRoiPxWidth));
        OnPropertyChanged(nameof(TemplateRoiPxHeight));
        _host.NotifyDirty();
        if (callerProperty != null)
            OnPropertyChanged(callerProperty);
    }
}
