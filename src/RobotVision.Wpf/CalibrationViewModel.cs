using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobotVision.Core.Models;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Calibration;

namespace RobotVision.WpfHost;

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

/// <summary>标定档案浏览：内参/外参/旋转中心/多项式档案的当前加载状态（含质量评估与删除）。</summary>
public partial class CalibrationViewModel : ObservableObject
{
    private readonly CalibrationManager _calibration;
    private readonly AppConfig _cfg;

    public ObservableCollection<IntrinsicRow> Intrinsics { get; } = [];

    public ObservableCollection<ExtrinsicRow> Extrinsics { get; } = [];

    public ObservableCollection<RotationRow> RotationCenters { get; } = [];

    public ObservableCollection<PolynomialRow> Polynomials { get; } = [];

    [ObservableProperty]
    private string _message = "";

    public CalibrationViewModel(CalibrationManager calibration, AppConfig cfg)
    {
        _calibration = calibration;
        _cfg = cfg;
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

        OnPropertyChanged(nameof(DeleteIntrinsicCommand));
        OnPropertyChanged(nameof(DeleteExtrinsicCommand));
        OnPropertyChanged(nameof(DeleteRotationCommand));
        OnPropertyChanged(nameof(DeletePolynomialCommand));
        Message = $"内参 {Intrinsics.Count} · 外参 {Extrinsics.Count} · 旋转中心 {RotationCenters.Count} · 多项式 {Polynomials.Count}（重新标定请使用标定向导/CalibTool）";
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
        var path = Path.Combine(AppConfigExtensions.ResolveFolder(_cfg.CalibrationFolder), fileName);
        var detail = File.Exists(path) ? "\n文件将从磁盘删除（不可恢复）。" : "";
        return MessageBox.Show($"删除档案 {fileName}？{detail}", "删除标定档案",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    [RelayCommand]
    private void OpenFolder() => RecipeViewModel.ShellOpen(
        AppConfigExtensions.ResolveFolder(_cfg.CalibrationFolder));

    private static string QualityText(CalibrationQuality quality) => quality switch
    {
        CalibrationQuality.Good => "优秀",
        CalibrationQuality.Fair => "可用",
        _ => "超标",
    };

    private static string FormatNumbers(double[] values) =>
        string.Join(", ", values.Select(v => v.ToString("0.######")));
}
