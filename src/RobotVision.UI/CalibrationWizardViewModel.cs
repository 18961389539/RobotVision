using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Models;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Cameras;

namespace RobotVision.UI;

public enum WizardMode
{
    Intrinsic,
    Extrinsic,
    Rotation,
}

/// <summary>标定点表行：像素坐标来自图上点选，机器人坐标由用户从示教器抄录。</summary>
public partial class CalibPointItem : ObservableObject
{
    public int Index { get; set; }

    public double PixelX { get; init; }

    public double PixelY { get; init; }

    [ObservableProperty]
    private double _robotX;

    [ObservableProperty]
    private double _robotY;
}

/// <summary>
/// 标定向导：内参（棋盘格多帧采集）/ 外参（九点点对）/ 旋转中心（多角度标记点）。
/// 计算复用 CalibTool 同款标定器；保存写 data/calibration 并热加载进运行中的
/// CalibrationManager，无需重启。外参与旋转中心的取图必须经过内参去畸变
/// （与产线推理同一坐标系，铁律见 CalibrationManager 注释）。
/// </summary>
public partial class CalibrationWizardViewModel : ObservableObject
{
    private readonly CameraManager _cameras;
    private readonly CalibrationManager _calibration;
    private readonly AppConfig _cfg;
    private readonly string _calibrationFolder;

    private Mat? _lastRawFrame;
    private Mat? _currentFrame;
    private bool _lastChessboardFound;
    private IntrinsicProfile? _pendingIntrinsic;
    private ExtrinsicProfile? _pendingExtrinsic;
    private RotationCenterProfile? _pendingRotation;
    private string _intrinsicTempFolder = "";

    public IReadOnlyList<WizardMode> ModeOptions { get; } = Enum.GetValues<WizardMode>();

    public IReadOnlyList<string> CameraIds => _cameras.CameraIds.ToList();

    public ObservableCollection<CalibPointItem> Points { get; } = [];

    [ObservableProperty]
    private WizardMode _mode = WizardMode.Intrinsic;

    [ObservableProperty]
    private string _selectedCamera = "";

    [ObservableProperty]
    private string _stationId = "st1";

    [ObservableProperty]
    private int _cols = 9;

    [ObservableProperty]
    private int _rows = 6;

    [ObservableProperty]
    private double _squareMm = 5.0;

    [ObservableProperty]
    private int _collectedFrames;

    [ObservableProperty]
    private ImageSource? _frameImage;

    [ObservableProperty]
    private string _frameInfo = "尚未取图";

    [ObservableProperty]
    private string _result = "";

    [ObservableProperty]
    private string _message = "";

    /// <summary>取图进行中（防抖：按钮自动禁用）。</summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>当前模式是否允许在图上点选（外参/旋转中心）。</summary>
    [ObservableProperty]
    private bool _clickable;

    public CalibrationWizardViewModel(
        CameraManager cameras,
        CalibrationManager calibration,
        AppConfig cfg)
    {
        _cameras = cameras;
        _calibration = calibration;
        _cfg = cfg;
        _calibrationFolder = cfg.ResolveCalibrationFolder();
        SelectedCamera = CameraIds.FirstOrDefault() ?? "";
    }

    /// <summary>页面再次进入时刷新相机下拉。</summary>
    public void RefreshCameras() => OnPropertyChanged(nameof(CameraIds));

    partial void OnSelectedCameraChanged(string value)
    {
        // 虚拟相机的棋盘规格固定可推导（默认 1280×960/40px = 15×11 内角点）：
        // 切换到虚拟相机时自动同步，避免按物理棋盘默认 9×6 查询导致检测失败或子网格错位
        if (value.Length == 0 || !_cameras.TryGet(value, out var camera) ||
            camera is not VirtualCamera virtualCam)
            return;

        var inner = virtualCam.ChessboardInnerCorners;
        if (inner.Width is < 3 or > 30 || inner.Height is < 3 or > 30 ||
            (Cols == inner.Width && Rows == inner.Height))
            return;

        Cols = inner.Width;
        Rows = inner.Height;
        Message = $"虚拟相机棋盘内角点已同步为 {Cols}×{Rows}";
    }

    partial void OnModeChanged(WizardMode value)
    {
        Clickable = value is WizardMode.Extrinsic or WizardMode.Rotation;
        Result = "";
        Message = value switch
        {
            WizardMode.Intrinsic => "内参：多次取图（覆盖四角、姿态多样），每张检测到棋盘后加入采集",
            WizardMode.Extrinsic => "外参：取图后在图上依次点 9 个标定点，并抄录机器人坐标",
            WizardMode.Rotation => "旋转中心：每个角度取图→点选标记点，共 5~9 个等间隔角度",
            _ => "",
        };
    }

    private string TempFolder()
    {
        if (_intrinsicTempFolder.Length == 0)
            _intrinsicTempFolder = Path.Combine(Path.GetTempPath(), "rv_intrinsic_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_intrinsicTempFolder);
        return _intrinsicTempFolder;
    }

    /// <summary>取图未进行中（防抖）。</summary>
    private bool CanOperate => !IsBusy;

    partial void OnIsBusyChanged(bool value) => GrabCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task GrabAsync()
    {
        if (SelectedCamera.Length == 0)
        {
            Message = "请先选择相机";
            return;
        }

        IsBusy = true;
        try
        {
            Message = $"取图中 · {SelectedCamera} …";
            // 取图 + 棋盘检测在后台线程执行（真实相机可达 GrabTimeoutMs，棋盘检测亦耗时），避免冻结 UI
            var result = await Task.Run(() => GrabCore(SelectedCamera));

            if (Mode == WizardMode.Extrinsic)
            {
                Points.Clear();
                OnPointsChanged();
            }

            UpdateFrame(result.Display);
            _lastRawFrame?.Dispose();
            _lastRawFrame = result.Raw;
            FrameInfo = result.Info;
        }
        catch (VisionException vex)
        {
            Message = $"{vex.Message}（外参/旋转中心取图前须先完成该相机的内参标定）";
        }
        catch (Exception ex)
        {
            Message = $"取图失败: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>一次取图的后台工作结果（Display 所有权移交调用方，Raw 可为 null）。</summary>
    private sealed record GrabResult(Mat Display, Mat? Raw, string Info);

    /// <summary>后台执行取图与棋盘检测；Mat 在后台线程创建、返回后仅 UI 线程使用。</summary>
    private GrabResult GrabCore(string cameraId)
    {
        using var frame = _cameras.Grab(cameraId).Image;

        if (Mode == WizardMode.Intrinsic)
        {
            using var gray = new Mat();
            Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
            var pattern = new OpenCvSharp.Size(Cols, Rows);
            _lastChessboardFound = Cv2.FindChessboardCornersSB(gray, pattern, out var corners);

            var preview = frame.Clone();
            if (_lastChessboardFound)
                Cv2.DrawChessboardCorners(preview, pattern, corners, true);
            var raw = frame.Clone();
            var info = _lastChessboardFound
                ? $"棋盘检测成功（{Cols}×{Rows}），可加入采集"
                : "未检测到棋盘：调整角度/对焦或核对内角点数";
            return new GrabResult(preview, raw, info);
        }

        // 外参与旋转中心必须在去畸变坐标系下取点（与推理一致）
        var undistorted = _calibration.Undistort(cameraId, frame);
        var text = Mode == WizardMode.Extrinsic
            ? $"新图已就绪（{undistorted.Width}×{undistorted.Height}），请依次点 9 个标定点"
            : $"新图已就绪（{undistorted.Width}×{undistorted.Height}），请点选本角度的标记点";
        return new GrabResult(undistorted, null, text);
    }

    private void UpdateFrame(Mat display)
    {
        var source = ImageConverter.ToBitmapSource(display);
        FrameImage = source;
        _currentFrame?.Dispose();
        _currentFrame = display;
    }

    [RelayCommand]
    private void CaptureFrame()
    {
        if (_lastRawFrame is null || _lastRawFrame.Empty())
        {
            Message = "请先取图";
            return;
        }
        if (!_lastChessboardFound)
        {
            Message = "当前帧未检测到棋盘，不能加入采集";
            return;
        }

        var index = ++CollectedFrames;
        var path = Path.Combine(TempFolder(), $"frame_{index:D3}.png");
        Cv2.ImWrite(path, _lastRawFrame);
        Message = $"已加入第 {index} 帧（建议 ≥{ChessboardIntrinsicCalibrator.MinImageCount} 帧）";
    }

    [RelayCommand]
    private void ClearIntrinsic()
    {
        try
        {
            if (Directory.Exists(_intrinsicTempFolder))
                Directory.Delete(_intrinsicTempFolder, true);
        }
        catch (IOException) { /* 下次换新目录 */ }
        _intrinsicTempFolder = "";
        CollectedFrames = 0;
        _pendingIntrinsic = null;
        Result = "";
        Message = "已清空采集";
    }

    /// <summary>图上点选回调（页面换算好的图像像素坐标）。</summary>
    public void AddPoint(double pixelX, double pixelY)
    {
        Points.Add(new CalibPointItem
        {
            Index = Points.Count + 1,
            PixelX = Math.Round(pixelX, 1),
            PixelY = Math.Round(pixelY, 1),
        });
        OnPointsChanged();
        Message = $"点 #{Points.Count}: ({Points[^1].PixelX:0.0}, {Points[^1].PixelY:0.0})";
    }

    [RelayCommand]
    private void RemoveLastPoint()
    {
        if (Points.Count == 0)
            return;
        Points.RemoveAt(Points.Count - 1);
        OnPointsChanged();
    }

    [RelayCommand]
    private void ClearPoints()
    {
        Points.Clear();
        OnPointsChanged();
    }

    private void OnPointsChanged()
    {
        foreach (var item in Points)
            item.Index = Points.IndexOf(item) + 1;
        OnPropertyChanged(nameof(Points));
        Result = "";
    }

    [RelayCommand]
    private async Task ComputeAsync()
    {
        try
        {
            Message = "计算中...";
            Result = "";
            switch (Mode)
            {
                case WizardMode.Intrinsic:
                    _pendingIntrinsic = await Task.Run(() =>
                        ChessboardIntrinsicCalibrator.Calibrate(
                            SelectedCamera, Directory.GetFiles(TempFolder(), "*.png"),
                            new OpenCvSharp.Size(Cols, Rows), SquareMm));
                    Result = Format(_pendingIntrinsic);
                    break;
                case WizardMode.Extrinsic:
                    _calibration.RequireIntrinsic(SelectedCamera);
                    _pendingExtrinsic = await Task.Run(() =>
                        NinePointExtrinsicCalibrator.Calibrate(
                            StationId.Trim(), SelectedCamera,
                            [.. Points.Select(p => new Point2f((float)p.PixelX, (float)p.PixelY))],
                            [.. Points.Select(p => new Point2f((float)p.RobotX, (float)p.RobotY))]));
                    Result = Format(_pendingExtrinsic);
                    break;
                case WizardMode.Rotation:
                    _calibration.RequireIntrinsic(SelectedCamera);
                    _pendingRotation = await Task.Run(() =>
                        RotationCenterCalibrator.Calibrate(
                            StationId.Trim(), SelectedCamera,
                            [.. Points.Select(p => new Point2f((float)p.PixelX, (float)p.PixelY))]));
                    Result = Format(_pendingRotation);
                    break;
            }
            Message = "计算完成，确认指标后保存";
        }
        catch (VisionException vex)
        {
            Result = "";
            Message = $"标定失败: {vex.Message}";
        }
        catch (Exception ex)
        {
            Result = "";
            Message = $"标定失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Save()
    {
        try
        {
            switch (Mode)
            {
                case WizardMode.Intrinsic:
                    if (_pendingIntrinsic is null)
                    {
                        Message = "请先计算";
                        return;
                    }
                    if (!ConfirmOverwrite($"{_pendingIntrinsic.CameraId}.intrinsic.json"))
                        return;
                    var intrinsicPath = WriteProfile(_pendingIntrinsic, $"{_pendingIntrinsic.CameraId}.intrinsic.json");
                    _calibration.LoadIntrinsic(_pendingIntrinsic);
                    Message = $"已保存并加载: {intrinsicPath}";
                    break;

                case WizardMode.Extrinsic:
                    if (_pendingExtrinsic is null)
                    {
                        Message = "请先计算";
                        return;
                    }
                    if (StationId.Trim().Length == 0)
                    {
                        Message = "工位 Id 不能为空";
                        return;
                    }
                    if (!ConfirmOverwrite($"{StationId.Trim()}.extrinsic.json"))
                        return;
                    var extrinsicPath = WriteProfile(_pendingExtrinsic, $"{StationId.Trim()}.extrinsic.json");
                    _calibration.LoadExtrinsic(_pendingExtrinsic);
                    Message = $"已保存并加载: {extrinsicPath}";
                    break;

                case WizardMode.Rotation:
                    if (_pendingRotation is null)
                    {
                        Message = "请先计算";
                        return;
                    }
                    if (!ConfirmOverwrite($"{StationId.Trim()}.rotation.json"))
                        return;
                    var rotationPath = WriteProfile(_pendingRotation, $"{StationId.Trim()}.rotation.json");
                    _calibration.LoadRotationCenter(_pendingRotation);
                    Message = $"已保存并加载: {rotationPath}";
                    break;
            }
        }
        catch (Exception ex)
        {
            Message = $"保存失败: {ex.Message}";
        }
    }

    private bool ConfirmOverwrite(string fileName)
    {
        var path = Path.Combine(_calibrationFolder, fileName);
        if (!File.Exists(path))
            return true;
        return MessageBox.Show($"档案 {fileName} 已存在，覆盖保存？", "覆盖确认",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    private string WriteProfile<T>(T profile, string fileName)
    {
        Directory.CreateDirectory(_calibrationFolder);
        var path = Path.Combine(_calibrationFolder, fileName);
        File.WriteAllText(path, JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }

    private static string Format(IntrinsicProfile p)
    {
        var k = p.CameraMatrix;
        var text = $"重投影 RMS: {p.Rms:0.000} px（≤0.3 优秀，≤0.5 可用）\n" +
                   $"fx={k[0]:0.0} fy={k[4]:0.0} cx={k[2]:0.0} cy={k[5]:0.0} · {p.Width}×{p.Height} · 有效图 {p.ImageCount} 张";
        if (p.ImageCount > 0 && p.ImageCount < ChessboardIntrinsicCalibrator.RecommendedImageCount)
            text += $"\n提示: 有效图仅 {p.ImageCount} 张，建议 ≥{ChessboardIntrinsicCalibrator.RecommendedImageCount} 张（覆盖四角、姿态多样）";
        if (p.PerImageRms is { Count: > 0 })
            text += $"\n单图 RMS: {p.PerImageRms.Min():0.000} ~ {p.PerImageRms.Max():0.000} px（最大者为疑似坏图）";
        if (p.Rms > 0.5)
            text += "\n警告: RMS 偏大，建议重拍（覆盖四角、姿态多样、对焦清晰）";
        return text;
    }

    private static string Format(ExtrinsicProfile p) =>
        $"RMS 残差: {p.Rms:0.0000} · 最大残差: {p.MaxResidual:0.0000}（机器人单位）" +
        (p.LeaveOneOutMax > 0
            ? $"\n留一最大误差: {p.LeaveOneOutMax:0.0000}（偏大说明存在抄错的点对）"
            : "") +
        (p.MaxResidual > 0.1 ? "\n警告: 残差偏大，请核对点对（像素点与机器人点须一一对应）" : "");

    private static string Format(RotationCenterProfile p)
    {
        var text = $"轴心像素坐标: ({p.Cx:0.00}, {p.Cy:0.00}) · 半径 {p.RadiusPx:0.00} px\n" +
                   $"半径残差 RMS: {p.Rms:0.000} px（≤0.3 优秀，≤0.5 可用）";
        if (p.PointCount >= 5)
            text += $"\n椭圆长短轴比: {p.AxisRatio:0.000}（1=正圆）";
        if (p.Rms > 0.5)
            text += "\n警告: 半径残差偏大，建议增加角度数量";
        if (p.PointCount >= 5 && p.AxisRatio > 1.2)
            text += "\n警告: 轨迹不是正圆，检查标记提取或机械间隙";
        return text;
    }
}
