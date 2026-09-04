using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Recipe;

/// <summary>检测/示教 ROI：比例存储、像素换算、参考帧预览。脏标记回写工作区。</summary>
public sealed partial class RecipeRoiEditor : ObservableObject
{
    private readonly IRecipeWorkspace _host;
    private readonly ICameraRuntime _cameras;
    private readonly ICalibrationRuntime _calibration;
    private readonly ILightingRuntime _lighting;
    private readonly PageAsyncSession _pageSession;
    private string? _roiFrameCameraId;

    internal RecipeRoiEditor(
        IRecipeWorkspace host,
        ICameraRuntime cameras,
        ICalibrationRuntime calibration,
        ILightingRuntime lighting,
        PageAsyncSession pageSession)
    {
        _host = host;
        _cameras = cameras;
        _calibration = calibration;
        _lighting = lighting;
        _pageSession = pageSession;
    }

    private RecipeConfig Editor => _host.Editor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRoiRefFrame))]
    [NotifyPropertyChangedFor(nameof(RoiPxX), nameof(RoiPxY), nameof(RoiPxWidth), nameof(RoiPxHeight))]
    [NotifyPropertyChangedFor(nameof(SecondaryRoiPxX), nameof(SecondaryRoiPxY), nameof(SecondaryRoiPxWidth), nameof(SecondaryRoiPxHeight))]
    [NotifyPropertyChangedFor(nameof(RoiRefFrameHint), nameof(RoiRatioHint))]
    private int _roiRefWidth;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRoiRefFrame))]
    [NotifyPropertyChangedFor(nameof(RoiPxX), nameof(RoiPxY), nameof(RoiPxWidth), nameof(RoiPxHeight))]
    [NotifyPropertyChangedFor(nameof(SecondaryRoiPxX), nameof(SecondaryRoiPxY), nameof(SecondaryRoiPxWidth), nameof(SecondaryRoiPxHeight))]
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

    public DualTemplateTeachSlot TeachCropSlot { get; set; } = DualTemplateTeachSlot.A;

    private Roi? SecondarySearchRoi
    {
        get => Editor.SecondarySearchRoi;
        set => Editor.SecondarySearchRoi = value;
    }

    public bool UseSecondaryRoi
    {
        get => SecondarySearchRoi is not null;
        set
        {
            if (value)
            {
                var addedPrimary = EnsurePrimaryRoiForDualSearch();
                SecondarySearchRoi ??= new Roi(0.55, 0.15, 0.4, 0.7);
                if (addedPrimary)
                    NotifyRoiChanged();
            }
            else
                SecondarySearchRoi = null;
            NotifySecondaryRoiChanged();
        }
    }

    public double SecondaryRoiPxX
    {
        get => SecondarySearchRoi is { } r && RoiRefWidth > 0 ? Math.Round(r.X * RoiRefWidth) : 0;
        set
        {
            if (RoiRefWidth <= 0 || SecondarySearchRoi is not { } r)
                return;
            var w = Math.Max(1, (int)Math.Round(r.Width * RoiRefWidth));
            var px = Math.Clamp((int)Math.Round(value), 0, Math.Max(0, RoiRefWidth - w));
            SetSecondaryRoi(nameof(SecondaryRoiPxX), x => x with { X = px / (double)RoiRefWidth });
        }
    }

    public double SecondaryRoiPxY
    {
        get => SecondarySearchRoi is { } r && RoiRefHeight > 0 ? Math.Round(r.Y * RoiRefHeight) : 0;
        set
        {
            if (RoiRefHeight <= 0 || SecondarySearchRoi is not { } r)
                return;
            var h = Math.Max(1, (int)Math.Round(r.Height * RoiRefHeight));
            var px = Math.Clamp((int)Math.Round(value), 0, Math.Max(0, RoiRefHeight - h));
            SetSecondaryRoi(nameof(SecondaryRoiPxY), v => v with { Y = px / (double)RoiRefHeight });
        }
    }

    public double SecondaryRoiPxWidth
    {
        get => SecondarySearchRoi is { } r && RoiRefWidth > 0 ? Math.Round(r.Width * RoiRefWidth) : 0;
        set
        {
            if (RoiRefWidth <= 0 || SecondarySearchRoi is not { } r)
                return;
            var x = (int)Math.Round(r.X * RoiRefWidth);
            var px = Math.Clamp((int)Math.Round(value), 1, Math.Max(1, RoiRefWidth - x));
            SetSecondaryRoi(nameof(SecondaryRoiPxWidth), v => v with { Width = px / (double)RoiRefWidth });
        }
    }

    public double SecondaryRoiPxHeight
    {
        get => SecondarySearchRoi is { } r && RoiRefHeight > 0 ? Math.Round(r.Height * RoiRefHeight) : 0;
        set
        {
            if (RoiRefHeight <= 0 || SecondarySearchRoi is not { } r)
                return;
            var y = (int)Math.Round(r.Y * RoiRefHeight);
            var px = Math.Clamp((int)Math.Round(value), 1, Math.Max(1, RoiRefHeight - y));
            SetSecondaryRoi(nameof(SecondaryRoiPxHeight), v => v with { Height = px / (double)RoiRefHeight });
        }
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

    public bool HasRefineLine => Editor.Template?.RefineLine is not null;

    public string RefineLineStatusText => Editor.Template?.RefineLine is not { } line
        ? "未手动画基准线：默认沿长轴自动采头尾明暗（较亮一端为头）"
        : line.HasReliableSignature
            ? $"基准线已示教：头尾明暗差 {line.HeadMinusTailGray:0.#}（可消 180°，输出有向角）"
            : $"基准线已画，但头尾明暗差仅 {line.HeadMinusTailGray:0.#}（<4，对称件判不了头尾，建议端头留明暗差或改用卡尺）";

    /// <summary>
    /// 用户在 ImageViewer 用「线」工具画的基准线回填：两端点（设备像素，比例于 <see cref="RoiRefWidth"/>/<see cref="RoiRefHeight"/>）
    /// → 归一化存入配方；并采 PreviewImage 上 P2(头)/P1(尾) 邻域均值差作为 180° 头尾签名。
    /// </summary>
    public void ApplyRefineLineFromPx(double x1Px, double y1Px, double x2Px, double y2Px)
    {
        if (RoiRefWidth <= 0 || RoiRefHeight <= 0)
            return;
        Editor.Template ??= new();
        var nx1 = Math.Clamp(x1Px / RoiRefWidth, 0, 1);
        var ny1 = Math.Clamp(y1Px / RoiRefHeight, 0, 1);
        var nx2 = Math.Clamp(x2Px / RoiRefWidth, 0, 1);
        var ny2 = Math.Clamp(y2Px / RoiRefHeight, 0, 1);
        var headMinusTail = TrySampleHeadTailGray(x1Px, y1Px, x2Px, y2Px, out var gray) ? gray : 0.0;
        Editor.Template.RefineLine = new RefineLine(nx1, ny1, nx2, ny2, headMinusTail);
        NotifyRefineLineChanged();
    }

    public void ClearRefineLine()
    {
        if (Editor.Template is not { } t || t.RefineLine is null)
            return;
        t.RefineLine = null;
        NotifyRefineLineChanged();
    }

    private bool TrySampleHeadTailGray(double x1Px, double y1Px, double x2Px, double y2Px, out double headMinusTail)
    {
        headMinusTail = 0;
        if (PreviewImage is not BitmapSource src || src.PixelWidth < 1 || src.PixelHeight < 1)
            return false;

        var bgra = src.Format == PixelFormats.Bgra32
            ? src
            : new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
        var w = bgra.PixelWidth;
        var h = bgra.PixelHeight;
        var stride = w * 4;
        var buf = new byte[stride * h];
        bgra.CopyPixels(buf, stride, 0);

        var sx = RoiRefWidth > 0 ? (double)w / RoiRefWidth : 1.0;
        var sy = RoiRefHeight > 0 ? (double)h / RoiRefHeight : 1.0;
        var tail = MeanGrayAt(buf, w, h, stride, x1Px * sx, y1Px * sy);
        var head = MeanGrayAt(buf, w, h, stride, x2Px * sx, y2Px * sy);
        headMinusTail = head - tail;
        return true;
    }

    private static double MeanGrayAt(byte[] bgra, int w, int h, int stride, double cx, double cy)
    {
        const int half = RefineLine.TeachProbePx / 2;
        var x0 = Math.Clamp((int)Math.Round(cx) - half, 0, w - 1);
        var x1 = Math.Clamp((int)Math.Round(cx) + half, 0, w - 1);
        var y0 = Math.Clamp((int)Math.Round(cy) - half, 0, h - 1);
        var y1 = Math.Clamp((int)Math.Round(cy) + half, 0, h - 1);
        if (x1 <= x0) x1 = x0;
        if (y1 <= y0) y1 = y0;
        double sum = 0;
        var count = 0;
        for (var y = y0; y <= y1; y++)
        {
            var row = y * stride;
            for (var x = x0; x <= x1; x++)
            {
                var o = row + x * 4;
                // BGRA → luma
                sum += 0.114 * bgra[o] + 0.587 * bgra[o + 1] + 0.299 * bgra[o + 2];
                count++;
            }
        }
        return count == 0 ? 0 : sum / count;
    }

    private void NotifyRefineLineChanged()
    {
        OnPropertyChanged(nameof(HasRefineLine));
        OnPropertyChanged(nameof(RefineLineStatusText));
        _host.NotifyDirty();
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
        var roi = RoiFromCenterPx(centerXPx, centerYPx, widthPx, heightPx, RoiRefWidth, RoiRefHeight);
        if (Editor.AngleMode == AngleMode.DualTemplateCenterLine)
        {
            if (TeachCropSlot == DualTemplateTeachSlot.B)
                Editor.DualTemplate.TeachRoiB = roi;
            else
                Editor.DualTemplate.TeachRoiA = roi;
            NotifyTemplateRoiChanged();
            return;
        }

        Editor.Template ??= new();
        Editor.Template.Roi = roi;
        NotifyTemplateRoiChanged();
    }

    public void ApplySecondaryRoiFromRect(double centerXPx, double centerYPx, double widthPx, double heightPx)
    {
        if (RoiRefWidth <= 0 || RoiRefHeight <= 0)
            return;
        var addedPrimary = EnsurePrimaryRoiForDualSearch();
        SecondarySearchRoi = RoiFromCenterPx(centerXPx, centerYPx, widthPx, heightPx, RoiRefWidth, RoiRefHeight);
        if (addedPrimary)
            NotifyRoiChanged();
        NotifySecondaryRoiChanged();
    }

    /// <summary>双 ROI 时主区必须有 ROI1；未设则给左侧默认框，避免退回全图误检 ROI2。</summary>
    private bool EnsurePrimaryRoiForDualSearch()
    {
        if (Editor.Roi is not null)
            return false;
        Editor.Roi = new Roi(0.05, 0.15, 0.4, 0.7);
        return true;
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

    public void EnsureDualTemplateTeachDrawable()
    {
        var placeholder = new Roi(0.35, 0.35, 0.3, 0.3);
        if (TeachCropSlot == DualTemplateTeachSlot.B)
            Editor.DualTemplate.TeachRoiB ??= placeholder;
        else
            Editor.DualTemplate.TeachRoiA ??= placeholder;
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
        OnPropertyChanged(nameof(UseSecondaryRoi));
        OnPropertyChanged(nameof(SecondaryRoiPxX));
        OnPropertyChanged(nameof(SecondaryRoiPxY));
        OnPropertyChanged(nameof(SecondaryRoiPxWidth));
        OnPropertyChanged(nameof(SecondaryRoiPxHeight));
        OnPropertyChanged(nameof(HasRefineLine));
        OnPropertyChanged(nameof(RefineLineStatusText));
    }

    public void NotifyCanExecuteChanged() => PreviewRoiCommand.NotifyCanExecuteChanged();

    private bool CanPreviewRoi => !_host.IsBusy && !_host.IsPipelineOccupied;

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

        var generation = _pageSession.CaptureGeneration();
        var ct = _pageSession.Token;
        var work = PreviewRoiCoreAsync(cameraId, generation, ct);
        _pageSession.Track(work);
        await work;
    }

    private async Task PreviewRoiCoreAsync(string cameraId, int generation, CancellationToken ct)
    {
        _host.IsBusy = true;
        try
        {
            var roi = Editor.Roi;
            _host.Message = $"ROI 预览取图中 · {cameraId} …（推理图像空间：光源+去畸变/工位映射）";
            var (frame, width, height) = await RecipeEditorFrame.GrabPreviewAsync(
                _cameras, _calibration, _lighting, Editor, ct);
            if (!_pageSession.IsCurrent(generation) || ct.IsCancellationRequested)
                return;

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
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (_pageSession.IsCurrent(generation))
                _host.Message = $"ROI 预览失败: {ex.Message}";
        }
        finally
        {
            if (_pageSession.IsCurrent(generation))
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

    private void SetSecondaryRoi(string propertyName, Func<Roi, Roi> update)
    {
        if (SecondarySearchRoi is { } roi)
        {
            SecondarySearchRoi = update(roi);
            NotifySecondaryRoiChanged(propertyName);
        }
    }

    private void NotifySecondaryRoiChanged(string? callerProperty = null)
    {
        OnPropertyChanged(nameof(UseSecondaryRoi));
        OnPropertyChanged(nameof(SecondaryRoiPxX));
        OnPropertyChanged(nameof(SecondaryRoiPxY));
        OnPropertyChanged(nameof(SecondaryRoiPxWidth));
        OnPropertyChanged(nameof(SecondaryRoiPxHeight));
        _host.NotifyDirty();
        if (callerProperty != null)
            OnPropertyChanged(callerProperty);
    }
}
