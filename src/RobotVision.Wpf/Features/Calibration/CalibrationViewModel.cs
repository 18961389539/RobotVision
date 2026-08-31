using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobotVision.Core;
using RobotVision.Core.Models;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Cameras;

namespace RobotVision.WpfHost.Features.Calibration;

public sealed record IntrinsicRow(
    string CameraId, string Resolution, string Distortion, string Rms, string Quality, string CalibratedAt);

public sealed record ExtrinsicRow(
    string StationId, string CameraId, string AffineRow0, string AffineRow1,
    string Rms, string MaxResidual, string Quality, string CalibratedAt);

public sealed record RotationRow(
    string StationId, string CameraId, string Center, string Radius, string Rms,
    string AxisRatio, string PointCount, string Quality, string CalibratedAt);

public sealed record PolynomialRow(
    string StationId, string CameraId, string Resolution, string Order, string Space,
    string Rms, string MaxResidual, string Quality, string CalibratedAt);

/// <summary>比例档案卡片：X/Y 可就地编辑，其余字段只读展示。</summary>
public partial class ScaleCardItem : ObservableObject
{
    public required string StationId { get; init; }

    public required string CameraId { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public string CalibratedAt { get; init; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FieldOfView))]
    [NotifyPropertyChangedFor(nameof(Quality))]
    private double _scaleX;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FieldOfView))]
    [NotifyPropertyChangedFor(nameof(Quality))]
    private double _scaleY;

    public string Resolution => Width > 0 ? $"{Width}×{Height}" : "未记录";

    public string FieldOfView =>
        Width > 0 && ScaleX > 0 && ScaleY > 0
            ? $"{Width * ScaleX:0.##} × {Height * ScaleY:0.##} mm"
            : "-";

    public string Quality => CalibrationViewModel.AssessScaleQuality(ScaleX, ScaleY);

    public static ScaleCardItem FromProfile(ScaleProfile p) => new()
    {
        StationId = p.StationId,
        CameraId = p.CameraId,
        Width = p.Width,
        Height = p.Height,
        ScaleX = p.ScaleX,
        ScaleY = p.ScaleY,
        CalibratedAt = p.CalibratedAt.ToString("yyyy-MM-dd HH:mm"),
    };
}

/// <summary>标定档案浏览：内参/外参/旋转中心/多项式档案的当前加载状态（含质量评估与删除），
/// 以及比例标定（像素→毫米）的手动录入。</summary>
public partial class CalibrationViewModel : ObservableObject, ICommitPendingEdits
{
    private readonly CalibrationManager _calibration;
    private readonly AppConfig _cfg;
    private readonly CameraManager _cameras;

    public ObservableCollection<IntrinsicRow> Intrinsics { get; } = [];

    public ObservableCollection<ExtrinsicRow> Extrinsics { get; } = [];

    public ObservableCollection<RotationRow> RotationCenters { get; } = [];

    public ObservableCollection<PolynomialRow> Polynomials { get; } = [];

    public ObservableCollection<ScaleCardItem> Scales { get; } = [];

    public IReadOnlyList<string> CameraIds => _cameras.CameraIds.ToList();

    public IReadOnlyList<CameraOption> CameraOptions =>
        CameraOption.FromRegistered(_cfg.Cameras, _cameras.CameraIds);

    [ObservableProperty]
    private string _message = "";

    [ObservableProperty]
    private string _scaleStationId = "";

    [ObservableProperty]
    private string _scaleCameraId = "";

    /// <summary>所选相机取图不可用时的原因（如回放目录无图）；可用时为空。</summary>
    public string ScaleCameraGrabHint => _cameras.GetGrabErrorHint(ScaleCameraId) ?? "";

    public bool CanGrabScaleCamera => ScaleCameraGrabHint.Length == 0;

    public bool CanUseScaleGrab => CanGrabScaleCamera && !ScaleBusy;

    partial void OnScaleCameraIdChanged(string value)
    {
        OnPropertyChanged(nameof(ScaleCameraGrabHint));
        OnPropertyChanged(nameof(CanGrabScaleCamera));
        OnPropertyChanged(nameof(CanUseScaleGrab));
    }

    partial void OnScaleBusyChanged(bool value) => OnPropertyChanged(nameof(CanUseScaleGrab));

    [ObservableProperty]
    private int _scaleWidth;

    [ObservableProperty]
    private int _scaleHeight;

    /// <summary>X 方向比例（mm/px）。可手填，也可用下方换算助手算出。</summary>
    [ObservableProperty]
    private double _scaleX;

    /// <summary>Y 方向比例（mm/px）。</summary>
    [ObservableProperty]
    private double _scaleY;

    /// <summary>换算助手：已知物长的实际毫米值。</summary>
    [ObservableProperty]
    private double _refLengthMm;

    /// <summary>换算助手：该物长在图上量得的像素值。</summary>
    [ObservableProperty]
    private double _refLengthPx;

    /// <summary>比例表单的即时反馈（预览/校验错误，绿色成功/红色错误）。</summary>
    [ObservableProperty]
    private string _scaleFormMessage = "";

    [ObservableProperty]
    private bool _scaleFormMessageIsError;

    [ObservableProperty]
    private bool _scaleBusy;

    /// <summary>比例标定取图预览（换算助手量像素时对照用）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScalePreviewVisible))]
    private ImageSource? _scalePreviewImage;

    [ObservableProperty]
    private string _scalePreviewCaption = "";

    public bool ScalePreviewVisible => ScalePreviewImage is not null;

    [ObservableProperty]
    private bool _isScaleListPanelVisible = true;

    [ObservableProperty]
    private bool _isScaleParamPanelVisible = true;

    [ObservableProperty]
    private ScaleCardItem? _selectedScale;

    private bool _suppressScaleSelectionLoad;

    /// <summary>页面把 NumberBox 未提交的值刷进 ViewModel。见 <see cref="NumberBoxCommit"/>。</summary>
    public Action? FlushPendingEdits { get; set; }

    public CalibrationViewModel(CalibrationManager calibration, AppConfig cfg, CameraManager cameras)
    {
        _calibration = calibration;
        _cfg = cfg;
        _cameras = cameras;
        Refresh();
    }

    [RelayCommand]
    public void Refresh()
    {
        Intrinsics.Clear();
        foreach (var p in _calibration.IntrinsicProfiles)
            Intrinsics.Add(new IntrinsicRow(
                p.CameraId,
                $"{p.Width}×{p.Height}",
                FormatNumbers(p.DistCoeffs),
                $"{p.Rms:0.000} px",
                QualityText(CalibrationManager.AssessIntrinsic(p)),
                p.CalibratedAt.ToString("yyyy-MM-dd HH:mm")));

        Extrinsics.Clear();
        foreach (var p in _calibration.ExtrinsicProfiles)
            Extrinsics.Add(new ExtrinsicRow(
                p.StationId,
                p.CameraId,
                FormatNumbers(p.Affine.Length >= 3 ? p.Affine[..3] : p.Affine),
                FormatNumbers(p.Affine.Length >= 6 ? p.Affine[3..6] : []),
                $"{p.Rms:0.000}",
                $"{p.MaxResidual:0.000}",
                QualityText(CalibrationManager.AssessExtrinsic(p)),
                p.CalibratedAt.ToString("yyyy-MM-dd HH:mm")));

        RotationCenters.Clear();
        foreach (var p in _calibration.RotationCenterProfiles)
            RotationCenters.Add(new RotationRow(
                p.StationId,
                p.CameraId,
                $"({p.Cx:0.0}, {p.Cy:0.0}) px",
                $"{p.RadiusPx:0.0} px",
                $"{p.Rms:0.000} px",
                p.AxisRatio > 0 ? $"{p.AxisRatio:0.000}" : "-",
                $"{p.PointCount}",
                QualityText(CalibrationManager.AssessRotation(p)),
                p.CalibratedAt.ToString("yyyy-MM-dd HH:mm")));

        Polynomials.Clear();
        foreach (var p in _calibration.PolynomialProfiles)
            Polynomials.Add(new PolynomialRow(
                p.StationId,
                p.CameraId,
                $"{p.Width}×{p.Height}",
                $"{p.Order} 阶",
                string.Equals(p.CoordinateSpace, PolynomialCoordinateSpace.Image, StringComparison.OrdinalIgnoreCase)
                    ? "棋盘毫米系"
                    : "机器人系",
                $"{p.Rms:0.000}",
                $"{p.MaxResidual:0.000}",
                QualityText(CalibrationManager.AssessPolynomial(p)),
                p.CalibratedAt.ToString("yyyy-MM-dd HH:mm")));

        Scales.Clear();
        foreach (var p in _calibration.ScaleProfiles)
            Scales.Add(ScaleCardItem.FromProfile(p));

        var keepStationId = SelectedScale?.StationId;
        if (string.IsNullOrWhiteSpace(keepStationId) && !string.IsNullOrWhiteSpace(ScaleStationId))
            keepStationId = ScaleStationId.Trim();
        _suppressScaleSelectionLoad = true;
        SelectedScale = string.IsNullOrWhiteSpace(keepStationId)
            ? null
            : Scales.FirstOrDefault(s => string.Equals(s.StationId, keepStationId, StringComparison.OrdinalIgnoreCase));
        _suppressScaleSelectionLoad = false;

        OnPropertyChanged(nameof(DeleteIntrinsicCommand));
        OnPropertyChanged(nameof(DeleteExtrinsicCommand));
        OnPropertyChanged(nameof(DeleteRotationCommand));
        OnPropertyChanged(nameof(DeletePolynomialCommand));
        OnPropertyChanged(nameof(DeleteScaleCommand));
        OnPropertyChanged(nameof(LoadScaleToFormCommand));
        OnPropertyChanged(nameof(QuickSaveScaleCommand));
        OnPropertyChanged(nameof(ToggleScaleListPanelCommand));
        OnPropertyChanged(nameof(ToggleScaleParamPanelCommand));
        OnPropertyChanged(nameof(DeleteCurrentScaleCommand));
        OnPropertyChanged(nameof(CameraOptions));
        OnPropertyChanged(nameof(ScaleCameraGrabHint));
        OnPropertyChanged(nameof(CanGrabScaleCamera));
        OnPropertyChanged(nameof(CanUseScaleGrab));
        Message = $"内参 {Intrinsics.Count} · 外参 {Extrinsics.Count} · 旋转中心 {RotationCenters.Count} · 多项式 {Polynomials.Count} · 比例 {Scales.Count}（重新标定请使用标定向导/CalibTool）";
    }

    /// <summary>比例档案质量评估：X/Y 一致为"可用"，各向异性超阈值为"近似"。</summary>
    public static string AssessScaleQuality(double scaleX, double scaleY)
    {
        if (scaleX <= 0 || scaleY <= 0)
            return "可用";
        var ratio = Math.Max(scaleX, scaleY) / Math.Min(scaleX, scaleY) - 1;
        return ratio > CalibrationManager.ScaleAnisotropyWarnLimit ? "近似" : "可用";
    }

    partial void OnScaleXChanged(double value) => UpdateScalePreview();
    partial void OnScaleYChanged(double value) => UpdateScalePreview();
    partial void OnScaleWidthChanged(int value) => UpdateScalePreview();
    partial void OnScaleHeightChanged(int value) => UpdateScalePreview();

    /// <summary>比例表单即时预览：1 px 换算值 + 全视场物理尺寸（量级对错一眼可见）。</summary>
    private void UpdateScalePreview()
    {
        if (ScaleX <= 0 || ScaleY <= 0)
        {
            ScaleFormMessage = "";
            return;
        }
        var fov = ScaleWidth > 0 && ScaleHeight > 0
            ? $" · 视场 {ScaleWidth * ScaleX:0.##} × {ScaleHeight * ScaleY:0.##} mm"
            : "";
        var anisotropy = Math.Max(ScaleX, ScaleY) / Math.Min(ScaleX, ScaleY) - 1;
        var hint = anisotropy > CalibrationManager.ScaleAnisotropyWarnLimit
            ? $" · X/Y 差 {anisotropy * 100:0.0}%（疑似旋转/透视/畸变）"
            : "";
        ScaleFormMessageIsError = false;
        ScaleFormMessage = $"1 px = X {ScaleX:0.######} / Y {ScaleY:0.######} mm{fov}{hint}";
    }

    /// <summary>换算助手：物长 mm ÷ 图上 px → 写入 X 比例。</summary>
    [RelayCommand]
    private void ApplyRefToX()
    {
        this.Commit();
        if (RefLengthMm <= 0 || RefLengthPx <= 0)
        {
            ScaleFormMessageIsError = true;
            ScaleFormMessage = "换算助手需先填写物长 (mm) 与图上像素 (px)，均为正数";
            return;
        }
        ScaleX = RefLengthMm / RefLengthPx;
    }

    /// <summary>换算助手：物长 mm ÷ 图上 px → 写入 Y 比例。</summary>
    [RelayCommand]
    private void ApplyRefToY()
    {
        this.Commit();
        if (RefLengthMm <= 0 || RefLengthPx <= 0)
        {
            ScaleFormMessageIsError = true;
            ScaleFormMessage = "换算助手需先填写物长 (mm) 与图上像素 (px)，均为正数";
            return;
        }
        ScaleY = RefLengthMm / RefLengthPx;
    }

    /// <summary>从所选相机取一帧并显示预览，同时同步分辨率（与「读分辨率」同一帧语义）。</summary>
    [RelayCommand]
    private async Task GrabScalePreviewAsync()
    {
        if (ScaleBusy)
            return;
        if (!TryBeginScaleGrab(out var cameraId))
            return;
        ScaleBusy = true;
        try
        {
            var result = await Task.Run(() =>
            {
                using var grabbed = _cameras.Grab(cameraId);
                return (
                    Source: ImageConverter.ToBitmapSource(grabbed.Image),
                    grabbed.Image.Width,
                    grabbed.Image.Height);
            });
            ScalePreviewImage = result.Source;
            ScaleWidth = result.Width;
            ScaleHeight = result.Height;
            ScalePreviewCaption = $"{cameraId} · {result.Width}×{result.Height}";
            ScaleFormMessageIsError = false;
            ScaleFormMessage = $"已取图 {cameraId} · {result.Width}×{result.Height}（分辨率已同步，可在图上量像素填换算助手）";
        }
        catch (Exception ex)
        {
            SetScaleGrabError(ex);
        }
        finally
        {
            ScaleBusy = false;
        }
    }

    /// <summary>从所选相机取一帧，自动填入分辨率（比例的像素基准）。</summary>
    [RelayCommand]
    private async Task ReadCameraResolutionAsync()
    {
        if (ScaleBusy)
            return;
        if (!TryBeginScaleGrab(out var cameraId))
            return;
        ScaleBusy = true;
        try
        {
            var (w, h) = await Task.Run(() =>
            {
                using var grabbed = _cameras.Grab(cameraId);
                return (grabbed.Image.Width, grabbed.Image.Height);
            });
            ScaleWidth = w;
            ScaleHeight = h;
            ScaleFormMessageIsError = false;
            ScaleFormMessage = $"已读取相机 {cameraId} 分辨率 {w}×{h}";
        }
        catch (Exception ex)
        {
            SetScaleGrabError(ex);
        }
        finally
        {
            ScaleBusy = false;
        }
    }

    private bool TryBeginScaleGrab(out string cameraId)
    {
        cameraId = ScaleCameraId;
        var hint = _cameras.GetGrabErrorHint(cameraId);
        if (hint is null)
            return true;

        ScaleFormMessageIsError = true;
        ScaleFormMessage = hint.Contains("回放", StringComparison.Ordinal) ||
                           hint.Contains("图片", StringComparison.Ordinal)
            ? $"{hint}。请到「相机」页检查回放目录，或手动填写分辨率"
            : $"{hint}。请到「相机」页检查配置，或手动填写分辨率";
        return false;
    }

    private void SetScaleGrabError(Exception ex)
    {
        ScaleFormMessageIsError = true;
        if (ex is VisionException vex)
        {
            ScaleFormMessage = vex.ErrorCode switch
            {
                VisionErrorCode.CameraInitFailed =>
                    $"{vex.Message}。请到「相机」页检查该相机，或手动填写分辨率",
                VisionErrorCode.CameraGrabFailed when vex.Message.Contains("回放", StringComparison.Ordinal) =>
                    $"{vex.Message}。文件相机请确认回放目录有图片",
                _ => $"取图失败: {vex.Message}（可手动填写分辨率）",
            };
            return;
        }

        ScaleFormMessage = $"取图失败: {ex.Message}（可手动填写分辨率）";
    }

    /// <summary>保存手动录入的比例档案（覆盖同工位旧档）。</summary>
    [RelayCommand]
    private void SaveScale()
    {
        this.Commit();
        TryPersistScale(new ScaleProfile
        {
            StationId = ScaleStationId.Trim(),
            CameraId = ScaleCameraId.Trim(),
            ScaleX = ScaleX,
            ScaleY = ScaleY,
            Width = ScaleWidth,
            Height = ScaleHeight,
            Method = "Manual",
        }, stationId => $"已保存工位 {stationId} 的比例档案（{stationId}.scale.json）");
    }

    /// <summary>新建比例档案：清空表单并取消选中（避免选中卡片回填），填写新工位 Id 后保存即为新建。</summary>
    [RelayCommand]
    private void NewScale()
    {
        this.Commit();
        _suppressScaleSelectionLoad = true;
        SelectedScale = null;
        _suppressScaleSelectionLoad = false;
        ScaleStationId = "";
        ScaleCameraId = "";
        ScaleWidth = 0;
        ScaleHeight = 0;
        ScaleX = 0;
        ScaleY = 0;
        RefLengthMm = 0;
        RefLengthPx = 0;
        ScalePreviewImage = null;
        ScaleFormMessageIsError = false;
        ScaleFormMessage = "已清空表单：填写新工位 Id 后点「保存比例档案」即可新建";
    }

    partial void OnSelectedScaleChanged(ScaleCardItem? value)
    {
        if (value is null || _suppressScaleSelectionLoad)
            return;
        ApplyScaleItemToForm(value);
    }

    [RelayCommand]
    private void ToggleScaleListPanel() => IsScaleListPanelVisible = !IsScaleListPanelVisible;

    [RelayCommand]
    private void ToggleScaleParamPanel() => IsScaleParamPanelVisible = !IsScaleParamPanelVisible;

    /// <summary>将已有档案载入顶部表单，便于修改相机/分辨率等全量字段。</summary>
    [RelayCommand]
    private void LoadScaleToForm(ScaleCardItem? item)
    {
        if (item is null)
            return;
        ApplyScaleItemToForm(item);
    }

    private void ApplyScaleItemToForm(ScaleCardItem item)
    {
        ScaleStationId = item.StationId;
        ScaleCameraId = item.CameraId;
        ScaleWidth = item.Width;
        ScaleHeight = item.Height;
        ScaleX = item.ScaleX;
        ScaleY = item.ScaleY;
        ScaleFormMessageIsError = false;
        ScaleFormMessage = $"已载入工位 {item.StationId}：可在右侧修改后保存";
    }

    /// <summary>卡片就地保存：仅更新 X/Y（相机与分辨率沿用档案原值）。</summary>
    [RelayCommand]
    private void QuickSaveScale(ScaleCardItem? item)
    {
        this.Commit();
        if (item is null)
            return;
        TryPersistScale(new ScaleProfile
        {
            StationId = item.StationId,
            CameraId = item.CameraId,
            ScaleX = item.ScaleX,
            ScaleY = item.ScaleY,
            Width = item.Width,
            Height = item.Height,
            Method = "Manual",
        }, stationId => $"工位 {stationId} 比例已更新");
    }

    private void TryPersistScale(ScaleProfile profile, Func<string, string> successMessage)
    {
        try
        {
            if (_calibration.GetScale(profile.StationId) is { } old &&
                !ConfirmScaleDrift(profile, old))
                return;

            _calibration.SaveScale(profile);
            ScaleFormMessageIsError = false;
            ScaleFormMessage = successMessage(profile.StationId);
            Refresh();
        }
        catch (Exception ex)
        {
            ScaleFormMessageIsError = true;
            ScaleFormMessage = $"保存失败: {ex.Message}";
        }
    }

    private static bool ConfirmScaleDrift(ScaleProfile profile, ScaleProfile old)
    {
        var drift = Math.Max(
            Math.Abs(profile.ScaleX / old.ScaleX - 1),
            Math.Abs(profile.ScaleY / old.ScaleY - 1));
        if (drift <= 0.1)
            return true;
        return MessageBox.Show(
            $"工位 {profile.StationId} 已有比例档案，新值与旧值相差 {drift * 100:0.#}%：\n" +
            $"旧 X {old.ScaleX:0.######} / Y {old.ScaleY:0.######} → 新 X {profile.ScaleX:0.######} / Y {profile.ScaleY:0.######}\n\n" +
            "确认录入无误？", "比例差异提醒",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    /// <summary>删除比例档案（卡片内按钮，CommandParameter = StationId）。</summary>
    [RelayCommand]
    private void DeleteScale(string? stationId)
    {
        if (string.IsNullOrWhiteSpace(stationId))
            return;
        if (ConfirmDelete($"{stationId}.scale.json"))
            _calibration.DeleteScale(stationId);
        Refresh();
    }

    /// <summary>删除当前表单工位对应的比例档案。</summary>
    [RelayCommand]
    private void DeleteCurrentScale()
    {
        var stationId = ScaleStationId.Trim();
        if (string.IsNullOrEmpty(stationId))
        {
            ScaleFormMessageIsError = true;
            ScaleFormMessage = "请先填写或选择要删除的工位 Id";
            return;
        }
        DeleteScale(stationId);
    }

    /// <summary>删除内参档案（卡片内按钮，CommandParameter = CameraId）。</summary>
    [RelayCommand]
    private void DeleteIntrinsic(string? cameraId)
    {
        if (string.IsNullOrWhiteSpace(cameraId))
            return;
        if (ConfirmDelete($"{cameraId}.intrinsic.json"))
            _calibration.DeleteIntrinsic(cameraId);
        Refresh();
    }

    /// <summary>删除外参档案（卡片内按钮，CommandParameter = StationId）。</summary>
    [RelayCommand]
    private void DeleteExtrinsic(string? stationId)
    {
        if (string.IsNullOrWhiteSpace(stationId))
            return;
        if (ConfirmDelete($"{stationId}.extrinsic.json"))
            _calibration.DeleteExtrinsic(stationId);
        Refresh();
    }

    /// <summary>删除旋转中心档案（卡片内按钮，CommandParameter = StationId）。</summary>
    [RelayCommand]
    private void DeleteRotation(string? stationId)
    {
        if (string.IsNullOrWhiteSpace(stationId))
            return;
        if (ConfirmDelete($"{stationId}.rotation.json"))
            _calibration.DeleteRotationCenter(stationId);
        Refresh();
    }

    /// <summary>删除多项式档案（卡片内按钮，CommandParameter = StationId）。</summary>
    [RelayCommand]
    private void DeletePolynomial(string? stationId)
    {
        if (string.IsNullOrWhiteSpace(stationId))
            return;
        if (ConfirmDelete($"{stationId}.polynomial.json"))
            _calibration.DeletePolynomial(stationId);
        Refresh();
    }

    private bool ConfirmDelete(string fileName)
    {
        var path = Path.Combine(_cfg.ResolveCalibrationFolder(), fileName);
        var detail = File.Exists(path) ? "\n文件将从磁盘删除（不可恢复）。" : "";
        return MessageBox.Show($"删除档案 {fileName}？{detail}", "删除标定档案",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    [RelayCommand]
    private void OpenFolder() => Explorer.OpenFolder(_cfg.ResolveCalibrationFolder());

    private static string QualityText(CalibrationQuality quality) => quality switch
    {
        CalibrationQuality.Good => "优秀",
        CalibrationQuality.Fair => "可用",
        _ => "超标",
    };

    private static string FormatNumbers(double[] values) =>
        string.Join(", ", values.Select(v => v.ToString("0.######")));
}
