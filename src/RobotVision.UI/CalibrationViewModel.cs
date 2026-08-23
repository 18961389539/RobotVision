using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Calibration;

namespace RobotVision.UI;

public sealed record IntrinsicRow(
    string CameraId, string Resolution, string Distortion, string Rms, string Quality, string CalibratedAt);

public sealed record ExtrinsicRow(
    string StationId, string CameraId, string AffineRow0, string AffineRow1,
    string Rms, string MaxResidual, string Quality, string CalibratedAt);

public sealed record RotationRow(
    string StationId, string CameraId, string Center, string Radius, string Rms,
    string AxisRatio, string PointCount, string Quality, string CalibratedAt);

/// <summary>标定档案浏览：内参/外参/旋转中心三类档案的当前加载状态（含质量评估与删除）。</summary>
public partial class CalibrationViewModel : ObservableObject
{
    private readonly CalibrationManager _calibration;
    private readonly AppConfig _cfg;

    public ObservableCollection<IntrinsicRow> Intrinsics { get; } = [];

    public ObservableCollection<ExtrinsicRow> Extrinsics { get; } = [];

    public ObservableCollection<RotationRow> RotationCenters { get; } = [];

    [ObservableProperty]
    private string _message = "";

    [ObservableProperty]
    private IntrinsicRow? _selectedIntrinsic;

    [ObservableProperty]
    private ExtrinsicRow? _selectedExtrinsic;

    [ObservableProperty]
    private RotationRow? _selectedRotation;

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

        OnPropertyChanged(nameof(DeleteIntrinsicCommand));
        OnPropertyChanged(nameof(DeleteExtrinsicCommand));
        OnPropertyChanged(nameof(DeleteRotationCommand));
        Message = $"内参 {Intrinsics.Count} · 外参 {Extrinsics.Count} · 旋转中心 {RotationCenters.Count}（重新标定请使用标定向导/CalibTool）";
    }

    [RelayCommand]
    private void DeleteIntrinsic()
    {
        if (SelectedIntrinsic is null) return;
        if (ConfirmDelete($"{SelectedIntrinsic.CameraId}.intrinsic.json"))
            _calibration.DeleteIntrinsic(SelectedIntrinsic.CameraId);
        Refresh();
    }

    [RelayCommand]
    private void DeleteExtrinsic()
    {
        if (SelectedExtrinsic is null) return;
        if (ConfirmDelete($"{SelectedExtrinsic.StationId}.extrinsic.json"))
            _calibration.DeleteExtrinsic(SelectedExtrinsic.StationId);
        Refresh();
    }

    [RelayCommand]
    private void DeleteRotation()
    {
        if (SelectedRotation is null) return;
        if (ConfirmDelete($"{SelectedRotation.StationId}.rotation.json"))
            _calibration.DeleteRotationCenter(SelectedRotation.StationId);
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
