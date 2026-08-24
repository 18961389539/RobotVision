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

namespace RobotVision.WpfHost;

public enum WizardMode
{
    Intrinsic,
    Extrinsic,
    Rotation,

    /// <summary>多项式标定（单图模式）：一张棋盘格图 + 2 个同行参考角点 → 像素→机器人多项式。
    /// 替代"内参 + 外参"两步，适合小畸变/单平面/统一高度场景。</summary>
    Polynomial,
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

    /// <summary>用户已抄录机器人坐标（含原点 0,0）。默认可避免把未填写的 (0,0) 当有效点。</summary>
    [ObservableProperty]
    private bool _robotEntered;

    partial void OnRobotXChanged(double value)
    {
        if (value != 0 || RobotY != 0)
            RobotEntered = true;
    }

    partial void OnRobotYChanged(double value)
    {
        if (value != 0 || RobotX != 0)
            RobotEntered = true;
    }

    /// <summary>旋转中心模式：取该点时第 4 轴角度（可选；填 ≥3 个即自动做方向自检）。</summary>
    [ObservableProperty]
    private double? _robotRzDeg;
}

/// <summary>相机安装模式下拉项。</summary>
public sealed record MountOption(string Value, string Label);

/// <summary>OnArm 位姿处理模式下拉项。</summary>
public sealed record ComposeOption(string Value, string Label);

/// <summary>标定类型下拉项（操作员视角）：中文名称 + 一句话副标（做什么/要付出什么）。
/// 术语与工程概念（枚举名）的映射只在此处维护。</summary>
public sealed record ModeOption(WizardMode Value, string Label, string Description, bool IsRecommended);

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
    private PolynomialProfile? _pendingPolynomial;
    private string _intrinsicTempFolder = "";

    /// <summary>Polynomial 模式：最近一次取图检测到的棋盘角点（点选吸附基准）。</summary>
    private Point2f[] _chessboardCorners = [];

    /// <summary>标定类型下拉（操作员视角排序：推荐项置顶；中文 + 白话副标，见 ModeOption）。</summary>
    public IReadOnlyList<ModeOption> ModeOptions { get; } =
    [
        new(WizardMode.Polynomial, "快换标定（推荐）",
            "拍 1 张棋盘 · 免示教 · 输出毫米坐标 —— 量尺寸/上位机换算首选", IsRecommended: true),
        new(WizardMode.Polynomial, "机器人坐标标定",
            "拍 1 张棋盘 + 示教 2 个点 · 输出机器人坐标", IsRecommended: false),
        new(WizardMode.Rotation, "旋转轴心标定",
            "吸嘴/工具装歪（偏心）时用 · 多角度拍标记点", IsRecommended: false),
        new(WizardMode.Intrinsic, "完整标定 · 第 1 步（内参）",
            "拍 15~25 张棋盘 · 最高精度链路用", IsRecommended: false),
        new(WizardMode.Extrinsic, "完整标定 · 第 2 步（外参）",
            "内参完成后 · 图上点 9 点 + 抄机器人坐标", IsRecommended: false),
    ];

    /// <summary>相机安装模式（外参）：Fixed 档案全工位有效；OnArm 档案仅标定拍照位姿下有效。</summary>
    public IReadOnlyList<MountOption> MountOptions { get; } =
        [new(CameraMountType.Fixed, "固定机架"), new(CameraMountType.OnArm, "装在末端")];

    /// <summary>OnArm 位姿处理模式：Check=校验一致性（默认）；Translate=平移合成（相机纯平移，换点不重标）。</summary>
    public IReadOnlyList<ComposeOption> ComposeOptions { get; } =
        [new(PoseComposeMode.Check, "校验（须与标定一致）"), new(PoseComposeMode.Translate, "平移合成（允许平移）")];

    public IReadOnlyList<string> CameraIds => _cameras.CameraIds.ToList();

    public ObservableCollection<CalibPointItem> Points { get; } = [];

    [ObservableProperty]
    private WizardMode _mode = WizardMode.Polynomial; // 默认快换标定（推荐）：拍 1 张免示教

    /// <summary>下拉选中的标定类型项（中文显示模型）。两个 Polynomial 项靠坐标空间区分：
    /// "快换标定"自动切到棋盘毫米系（免示教），"机器人坐标标定"切到机器人系。</summary>
    [ObservableProperty]
    private ModeOption _selectedModeOption;

    partial void OnSelectedModeOptionChanged(ModeOption value)
    {
        if (value.Value == WizardMode.Polynomial)
        {
            // 快换（推荐）= Image 免示教；机器人坐标标定 = Robot 示教 2 点。
            // 通过选中项的 IsRecommended 标记区分（同一枚举的两个入口项）。
            SelectedSpace = value.IsRecommended
                ? PolynomialCoordinateSpace.Image
                : PolynomialCoordinateSpace.Robot;
        }
        Mode = value.Value;
    }

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

    /// <summary>外参：相机安装模式（Fixed/OnArm）。</summary>
    [ObservableProperty]
    private string _selectedMount = CameraMountType.Fixed;

    /// <summary>OnArm 位姿处理模式（Check/Translate，多项式与外参 OnArm 工位共用）。</summary>
    [ObservableProperty]
    private string _selectedCompose = PoseComposeMode.Check;

    /// <summary>外参 OnArm：标定时拍照点 TCP X（生产拍照位姿必须与此一致）。</summary>
    [ObservableProperty]
    private double _teachTcpX;

    /// <summary>外参 OnArm：标定时拍照点 TCP Y。</summary>
    [ObservableProperty]
    private double _teachTcpY;

    /// <summary>外参 OnArm：标定时拍照点第 4 轴角度（deg）。</summary>
    [ObservableProperty]
    private double _teachRzDeg;

    /// <summary>外参：标定平面 Z 高度（多厚度零件分层标定用；0 = 不记录）。</summary>
    [ObservableProperty]
    private double _calibrationPlaneZ;

    /// <summary>旋转中心：工具零位偏角 δ（deg）。第 4 轴转角 = 零件角 − δ；从带角度的标定点实测或工具坐标系换算。</summary>
    [ObservableProperty]
    private double _toolOffsetDeg;

    /// <summary>Polynomial 模式：多项式阶数（2=小畸变默认，3=畸变较大需更多网格点）。</summary>
    [ObservableProperty]
    private int _polynomialOrder = 2;

    /// <summary>Polynomial 模式：输出坐标空间。true=棋盘毫米系（免示教，只做像素→毫米）；
    /// false=机器人系（需示教 2 个参考角点锚定）。切换后点表/参考点输入区随之显隐。</summary>
    [ObservableProperty]
    private bool _polynomialImageSpace;

    /// <summary>坐标空间下拉项。</summary>
    public IReadOnlyList<ComposeOption> SpaceOptions { get; } =
        [new(PolynomialCoordinateSpace.Image, "棋盘毫米系（免示教）"),
         new(PolynomialCoordinateSpace.Robot, "机器人系（示教 2 点）")];

    [ObservableProperty]
    private string _selectedSpace = PolynomialCoordinateSpace.Image;

    partial void OnSelectedSpaceChanged(string value)
    {
        PolynomialImageSpace = string.Equals(value, PolynomialCoordinateSpace.Image, StringComparison.OrdinalIgnoreCase);
        // 切换坐标空间即换了一种标定语义：清空点表与结果，避免参考点残留误用
        Points.Clear();
        OnPointsChanged();
        Result = "";
        // 同步消息栏指引（Polynomial 在两种空间下操作步骤不同）
        if (Mode == WizardMode.Polynomial)
            OnModeChanged(WizardMode.Polynomial);
    }

    /// <summary>实测工具零位偏角（计算后可用，"填入实测偏角"按钮写入 ToolOffsetDeg）。</summary>
    private double? _measuredToolOffset;

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
        SelectedModeOption = ModeOptions[0]; // 快换标定（推荐）
    }

    /// <summary>页面再次进入时刷新相机下拉。</summary>
    public void RefreshCameras() => OnPropertyChanged(nameof(CameraIds));

    /// <summary>参数浮动面板可见性（参照相机管理页：图像主导、参数可折叠）。</summary>
    [ObservableProperty]
    private bool _isParamPanelVisible = true;

    [RelayCommand]
    private void ToggleParamPanel() => IsParamPanelVisible = !IsParamPanelVisible;

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
        Clickable = value is WizardMode.Extrinsic or WizardMode.Rotation or WizardMode.Polynomial;
        Result = "";
        _chessboardCorners = [];
        // 操作员视角的步骤指引（与下拉副标同一套文案口径）
        Message = value switch
        {
            WizardMode.Intrinsic => "完整标定·第 1 步：多拍 15~25 张棋盘（覆盖四角、姿态多样），每张检测到棋盘后点「加入当前帧」",
            WizardMode.Extrinsic => "完整标定·第 2 步（需先完成第 1 步）：取图后在图上依次点 9 个点，并抄录机器人坐标",
            WizardMode.Rotation => "旋转轴心标定（吸嘴偏心用）：第 4 轴每转一个角度取一次图并点标记点，共 5~9 个角度",
            WizardMode.Polynomial => PolynomialImageSpace
                ? "快换标定：棋盘放平在工件平面 → 取图 → 直接点「计算」（免示教）→ 保存"
                : "机器人坐标标定：取图 → 图上点同一行的 2 个角点 → 抄录机器人坐标 → 计算 → 保存",
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

        if ((Mode is WizardMode.Extrinsic or WizardMode.Polynomial) && Points.Count > 0 &&
            MessageBox.Show("重新取图将清空已录入的参考点。继续？", "重新取图",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        IsBusy = true;
        try
        {
            Message = $"取图中 · {SelectedCamera} …";
            // 取图 + 棋盘检测在后台线程执行（真实相机可达 GrabTimeoutMs，棋盘检测亦耗时），避免冻结 UI
            var result = await Task.Run(() => GrabCore(SelectedCamera));

            if (Mode is WizardMode.Extrinsic or WizardMode.Polynomial)
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
            var needIntrinsic = Mode is WizardMode.Extrinsic
                || (Mode is WizardMode.Rotation && !_calibration.HasPolynomial(StationId.Trim()));
            Message = needIntrinsic
                ? $"{vex.Message}（外参/旋转中心取图前须先完成该相机的内参标定）"
                : vex.Message;
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

        if (Mode is WizardMode.Intrinsic or WizardMode.Polynomial)
        {
            using var gray = new Mat();
            Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
            var pattern = new OpenCvSharp.Size(Cols, Rows);
            var found = Cv2.FindChessboardCornersSB(gray, pattern, out var corners);

            var preview = frame.Clone();
            if (found)
                Cv2.DrawChessboardCorners(preview, pattern, corners, true);

            if (Mode == WizardMode.Intrinsic)
            {
                _lastChessboardFound = found;
                var raw = frame.Clone();
                var info = found
                    ? $"棋盘检测成功（{Cols}×{Rows}），可加入采集"
                    : "未检测到棋盘：调整角度/对焦或核对内角点数";
                return new GrabResult(preview, raw, info);
            }

            // Polynomial：保存角点供点选吸附（原图坐标系，无去畸变——与推理一致）
            _chessboardCorners = found ? corners : [];
            var polyInfo = found
                ? PolynomialImageSpace
                    ? $"棋盘检测成功（{Cols}×{Rows}，{corners.Length} 角点）。棋盘毫米系免示教，直接点「计算」即可"
                    : $"棋盘检测成功（{Cols}×{Rows}，{corners.Length} 角点）。请在图上点选同一行的 2 个参考角点（自动吸附），并抄录其机器人坐标"
                : "未检测到棋盘：调整角度/对焦或核对内角点数（多项式标定不依赖内参，直接用原图）";
            return new GrabResult(preview, null, polyInfo);
        }

        if (Mode is WizardMode.Rotation &&
            !string.IsNullOrWhiteSpace(StationId) &&
            _calibration.HasPolynomial(StationId.Trim()))
        {
            var preview = frame.Clone();
            return new GrabResult(preview, null,
                $"新图已就绪（原图 {preview.Width}×{preview.Height}，多项式工位用原图坐标系，与推理一致）");
        }

        // 外参与（无多项式时的）旋转中心必须在去畸变坐标系下取点（与推理一致）
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

    /// <summary>图上点选回调（页面换算好的图像像素坐标）。
    /// Polynomial 机器人系模式吸附到最近检测角点（亚像素精度取检测值，消除手点误差）；
    /// 棋盘毫米系（免示教）不需要点选。</summary>
    public void AddPoint(double pixelX, double pixelY)
    {
        double px = Math.Round(pixelX, 1), py = Math.Round(pixelY, 1);
        if (Mode == WizardMode.Polynomial)
        {
            if (PolynomialImageSpace)
            {
                Message = "棋盘毫米系无需点选：直接点「计算」即可（免示教）";
                return;
            }
            if (_chessboardCorners.Length > 0)
            {
                if (Points.Count >= 2)
                {
                    Message = "多项式标定只需 2 个参考角点（用「清空点」重选）";
                    return;
                }
                var corner = _chessboardCorners[PolynomialCalibrator.NearestCornerIndex(
                    _chessboardCorners, new Point2f((float)pixelX, (float)pixelY))];
                px = Math.Round(corner.X, 2);
                py = Math.Round(corner.Y, 2);
            }
        }

        Points.Add(new CalibPointItem
        {
            Index = Points.Count + 1,
            PixelX = px,
            PixelY = py,
        });
        OnPointsChanged();
        Message = Mode == WizardMode.Polynomial
            ? $"参考点 #{Points.Count}/2: ({px:0.0}, {py:0.0})，请在右表抄录该角点的机器人坐标"
            : $"点 #{Points.Count}: ({px:0.0}, {py:0.0})";
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
        _measuredToolOffset = null; // 点表已变，旧实测值失效
    }

    /// <summary>把实测工具零位偏角填入输入框（计算后可用；见 ComputeAsync 旋转模式）。</summary>
    [RelayCommand]
    private void FillMeasuredOffset()
    {
        if (_measuredToolOffset is null)
        {
            Message = "请先计算（需 ≥2 个带第4轴角的标记点与工位外参或多项式档案）";
            return;
        }
        ToolOffsetDeg = _measuredToolOffset.Value;
        Message = $"已填入实测偏角 δ = {_measuredToolOffset.Value:0.00}°（若与预期差约 180°，请手动加/减 180）";
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
                    var intrinsicForExt = GetIntrinsic(SelectedCamera);
                    _pendingExtrinsic = await Task.Run(() =>
                        NinePointExtrinsicCalibrator.Calibrate(
                            StationId.Trim(), SelectedCamera,
                            [.. Points.Select(p => new Point2f((float)p.PixelX, (float)p.PixelY))],
                            [.. Points.Select(p => new Point2f((float)p.RobotX, (float)p.RobotY))],
                            intrinsicForExt?.Width ?? 0, intrinsicForExt?.Height ?? 0));
                    Result = Format(_pendingExtrinsic);
                    break;
                case WizardMode.Polynomial:
                    if (_chessboardCorners.Length == 0)
                    {
                        Message = "请先取图并检测到棋盘角点";
                        return;
                    }
                    var frame = _currentFrame;
                    if (frame is null)
                    {
                        Message = "内部错误：当前帧不可用，请重新取图";
                        return;
                    }
                    var polyCorners = _chessboardCorners;
                    var polyOrder = PolynomialOrder;
                    if (PolynomialImageSpace)
                    {
                        // 免示教：目标坐标=棋盘平面毫米系（索引×格距），无需任何参考点
                        _pendingPolynomial = await Task.Run(() => PolynomialCalibrator.CalibrateImageSpace(
                            StationId.Trim(), SelectedCamera,
                            polyCorners, new OpenCvSharp.Size(Cols, Rows), SquareMm,
                            frame.Width, frame.Height, polyOrder));
                        Result = Format(_pendingPolynomial);
                        break;
                    }

                    if (Points.Count < 2 || Points.Any(p => !p.RobotEntered))
                    {
                        Message = "请先点选 2 个参考角点并抄录其机器人坐标（原点 0,0 请勾选「已抄录」；或改用「棋盘毫米系」免示教）";
                        return;
                    }
                    var polyRef1 = new Point2f((float)Points[0].PixelX, (float)Points[0].PixelY);
                    var polyRef2 = new Point2f((float)Points[1].PixelX, (float)Points[1].PixelY);
                    var polyRobot1 = new Point2f((float)Points[0].RobotX, (float)Points[0].RobotY);
                    var polyRobot2 = new Point2f((float)Points[1].RobotX, (float)Points[1].RobotY);
                    _pendingPolynomial = await Task.Run(() => PolynomialCalibrator.Calibrate(
                        StationId.Trim(), SelectedCamera,
                        polyCorners, new OpenCvSharp.Size(Cols, Rows), SquareMm,
                        polyRef1, polyRobot1, polyRef2, polyRobot2,
                        frame.Width, frame.Height, polyOrder));
                    Result = Format(_pendingPolynomial);
                    break;

                case WizardMode.Rotation:
                    var rotationStationId = StationId.Trim();
                    var rotationHasPolynomial = _calibration.HasPolynomial(rotationStationId);
                    int rotWidth, rotHeight;
                    if (rotationHasPolynomial)
                    {
                        var poly = _calibration.PolynomialProfiles.First(p =>
                            string.Equals(p.StationId, rotationStationId, StringComparison.OrdinalIgnoreCase));
                        rotWidth = poly.Width;
                        rotHeight = poly.Height;
                    }
                    else
                    {
                        _calibration.RequireIntrinsic(SelectedCamera);
                        var intrinsicForRot = GetIntrinsic(SelectedCamera);
                        rotWidth = intrinsicForRot?.Width ?? 0;
                        rotHeight = intrinsicForRot?.Height ?? 0;
                    }

                    var rotation = await Task.Run(() =>
                        RotationCenterCalibrator.Calibrate(
                            rotationStationId, SelectedCamera,
                            [.. Points.Select(p => new Point2f((float)p.PixelX, (float)p.PixelY))],
                            rotWidth, rotHeight));
                    _pendingRotation = rotation with { ToolOffsetDeg = ToolOffsetDeg };
                    Result = Format(_pendingRotation);
                    _measuredToolOffset = null;

                    // 方向自检 + 偏角实测：工位有外参或多项式映射即可（GetMapping 两者皆可）
                    var paired = Points.Where(p => p.RobotRzDeg.HasValue).ToArray();
                    var rotationHasMapping = rotationHasPolynomial ||
                        _calibration.ExtrinsicProfiles.Any(e =>
                            string.Equals(e.StationId, rotationStationId, StringComparison.OrdinalIgnoreCase));
                    var pairedPoints = paired.Select(p => new Point2f((float)p.PixelX, (float)p.PixelY)).ToArray();
                    var pairedAngles = paired.Select(p => p.RobotRzDeg!.Value).ToArray();

                    if (!rotationHasMapping)
                    {
                        Result += paired.Length >= 2
                            ? "\n提示: 工位无外参/多项式档案，跳过方向自检与偏角实测（建议先标映射，再回来验证）"
                            : "\n提示: 在点表\"第4轴角\"列填写各点角度（≥2 个 + 工位外参或多项式）可实测工具零位偏角；≥3 个可同时做旋转方向自检";
                        break;
                    }

                    if (paired.Length >= 3)
                    {
                        _calibration.VerifyRotationDirection(rotationStationId, _pendingRotation,
                            pairedPoints, pairedAngles);
                        Result += "\n方向自检通过: 第 4 轴正方向与图像旋转方向一致";
                    }
                    else if (paired.Length > 0)
                    {
                        Result += "\n提示: ≥3 个带角度标记点可同时做旋转方向自检";
                    }

                    if (paired.Length >= 2)
                    {
                        // 从标定点实测 δ：βᵢ（标记绕轴心方位角）− φᵢ（第 4 轴角）的圆均值
                        var (offset, spread) = _calibration.ComputeToolOffsetDeg(
                            rotationStationId, _pendingRotation, pairedPoints, pairedAngles);
                        _measuredToolOffset = Math.Round(offset, 2);
                        Result += $"\n实测工具零位偏角 δ ≈ {offset:0.00}°（离散度 {spread:0.00}°）——点「填入实测偏角」自动填入";
                        if (spread > 5.0)
                            Result += "\n警告: 偏角离散度偏大，标记提取噪声或轴心误差影响实测值，建议核对";
                        Result += "\n提示: 若实测值与预期差约 180°，说明标记取在工具另一端，请手动加/减 180";
                    }
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
                    if (_calibration.HasPolynomial(StationId.Trim()) &&
                        MessageBox.Show("该工位已有多项式档案，生产将优先使用多项式（本外参会被忽略）。仍要保存外参？",
                            "双档案并存", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                        return;
                    // 保存时取 UI 当前值（计算后仍可调整安装模式/位姿/平面 Z）
                    _pendingExtrinsic = _pendingExtrinsic with
                    {
                        MountType = SelectedMount,
                        ComposeMode = SelectedMount == CameraMountType.OnArm ? SelectedCompose : PoseComposeMode.Check,
                        TeachTcpX = TeachTcpX,
                        TeachTcpY = TeachTcpY,
                        TeachRzDeg = TeachRzDeg,
                        // 显式标志而非 (0,0,0) 哨兵：OnArm 即视为已记录（拍照点恰为原点也不误判）
                        HasTeachPose = SelectedMount == CameraMountType.OnArm,
                        CalibrationPlaneZ = CalibrationPlaneZ,
                    };
                    var extrinsicPath = WriteProfile(_pendingExtrinsic, $"{StationId.Trim()}.extrinsic.json");
                    _calibration.LoadExtrinsic(_pendingExtrinsic);
                    Result = Format(_pendingExtrinsic); // 与最终保存值同步（计算后改过安装模式/位姿时）
                    Message = $"已保存并加载: {extrinsicPath}"
                        + (SelectedMount == CameraMountType.OnArm
                            ? $"（OnArm: 拍照位姿 {TeachTcpX:0.000}/{TeachTcpY:0.000} RZ {TeachRzDeg:0.0}° 已记录，生产拍照须与此一致）"
                            : "");
                    break;

                case WizardMode.Polynomial:
                    if (_pendingPolynomial is null)
                    {
                        Message = "请先计算";
                        return;
                    }
                    if (StationId.Trim().Length == 0)
                    {
                        Message = "工位 Id 不能为空";
                        return;
                    }
                    if (!ConfirmOverwrite($"{StationId.Trim()}.polynomial.json"))
                        return;
                    if (_calibration.HasExtrinsic(StationId.Trim()) &&
                        MessageBox.Show("该工位已有外参档案，保存多项式后生产将走多项式（外参被忽略）。继续？",
                            "双档案并存", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                        return;
                    // 保存时取 UI 当前值（坐标空间/安装模式/位姿合成模式/平面 Z 可在计算后调整）。
                    // Image 毫米系无机器人系概念：MountType 强制 Fixed、位姿不记录
                    var polyImageSpace = PolynomialImageSpace ||
                                         string.Equals(SelectedSpace, PolynomialCoordinateSpace.Image, StringComparison.OrdinalIgnoreCase);
                    _pendingPolynomial = _pendingPolynomial with
                    {
                        CoordinateSpace = polyImageSpace ? PolynomialCoordinateSpace.Image : PolynomialCoordinateSpace.Robot,
                        MountType = polyImageSpace ? CameraMountType.Fixed : SelectedMount,
                        ComposeMode = polyImageSpace ? PoseComposeMode.Check
                            : SelectedMount == CameraMountType.OnArm ? SelectedCompose : PoseComposeMode.Check,
                        TeachTcpX = polyImageSpace ? 0 : TeachTcpX,
                        TeachTcpY = polyImageSpace ? 0 : TeachTcpY,
                        TeachRzDeg = polyImageSpace ? 0 : TeachRzDeg,
                        HasTeachPose = !polyImageSpace && SelectedMount == CameraMountType.OnArm,
                        CalibrationPlaneZ = CalibrationPlaneZ,
                    };
                    var polyPath = WriteProfile(_pendingPolynomial, $"{StationId.Trim()}.polynomial.json");
                    _calibration.LoadPolynomial(_pendingPolynomial);
                    Result = Format(_pendingPolynomial);
                    Message = $"已保存并加载: {polyPath}（该工位走单图模式：推理直接用原图，无需内参/外参档案）"
                        + (SelectedMount == CameraMountType.OnArm && SelectedCompose == PoseComposeMode.Translate
                            ? "（Translate: 换拍照点不重标，TRIGGER 上报位姿自动合成）"
                            : "");
                    break;

                case WizardMode.Rotation:
                    if (_pendingRotation is null)
                    {
                        Message = "请先计算";
                        return;
                    }
                    if (!ConfirmOverwrite($"{StationId.Trim()}.rotation.json"))
                        return;
                    _pendingRotation = _pendingRotation with { ToolOffsetDeg = ToolOffsetDeg };
                    var rotationPath = WriteProfile(_pendingRotation, $"{StationId.Trim()}.rotation.json");
                    _calibration.LoadRotationCenter(_pendingRotation);
                    Result = Format(_pendingRotation); // 与最终保存值同步
                    Message = $"已保存并加载: {rotationPath}"
                        + (Math.Abs(ToolOffsetDeg) > 1e-9
                            ? $"（含工具零位偏角 {ToolOffsetDeg:0.0}°）"
                            : "");
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

    /// <summary>查相机已加载的内参档案（外参/旋转中心标定需记录同分辨率，供一致性校验）。</summary>
    private IntrinsicProfile? GetIntrinsic(string cameraId) =>
        _calibration.IntrinsicProfiles.FirstOrDefault(
            p => string.Equals(p.CameraId, cameraId, StringComparison.OrdinalIgnoreCase));

    private string WriteProfile<T>(T profile, string fileName)
    {
        Directory.CreateDirectory(_calibrationFolder);
        var path = Path.Combine(_calibrationFolder, fileName);
        // 原子落盘：标定档案是产线关键资产，写一半崩溃不得留下截断 JSON
        CalibrationManager.AtomicWriteAllText(path,
            JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));
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
        (p.MaxResidual > 0.1 ? "\n警告: 残差偏大，请核对点对（像素点与机器人点须一一对应）" : "") +
        (p.MountType == CameraMountType.OnArm
            ? $"\n安装模式: OnArm · {p.ComposeMode}（档案仅在标定拍照位姿 {p.TeachTcpX:0.000}/{p.TeachTcpY:0.000} RZ {p.TeachRzDeg:0.0}° 下有效）"
            : "") +
        (p.CalibrationPlaneZ != 0 ? $"\n标定平面 Z: {p.CalibrationPlaneZ:0.000}" : "");

    private static string Format(PolynomialProfile p) =>
        $"多项式阶数: {p.Order}（{p.CoefficientCount} 系数/轴） · 网格点 {p.PointCount}\n" +
        $"输出坐标: {(string.Equals(p.CoordinateSpace, PolynomialCoordinateSpace.Image, StringComparison.OrdinalIgnoreCase)
            ? "棋盘平面毫米系（免示教）" : "机器人系")}\n" +
        $"拟合残差 RMS: {p.Rms:0.0000} · 最大 {p.MaxResidual:0.0000}（mm，参考 ≤0.1 优秀 / ≤0.5 可用）" +
        (p.MaxResidual > CalibrationManager.ExtrinsicResidualFair ? "\n警告: 残差偏大，请重拍（棋盘放平、正对镜头）或核对参数" : "") +
        (string.Equals(p.CoordinateSpace, PolynomialCoordinateSpace.Robot, StringComparison.OrdinalIgnoreCase)
            ? p.MountType == CameraMountType.OnArm
                ? $"\n安装模式: OnArm · {p.ComposeMode}" +
                  (p.HasTeachPose
                      ? $"（标定拍照位姿 {p.TeachTcpX:0.000}/{p.TeachTcpY:0.000} RZ {p.TeachRzDeg:0.0}°）"
                      : "（未记录拍照位姿）")
                : "\n安装模式: Fixed（固定机架）"
            : "");

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
        if (Math.Abs(p.ToolOffsetDeg) > 1e-9)
            text += $"\n工具零位偏角: {p.ToolOffsetDeg:0.00}°（输出第 4 轴角 = 零件角 − {p.ToolOffsetDeg:0.00}°）";
        return text;
    }

    /// <summary>离开向导页时释放 OpenCV 帧，避免单例 VM 长期占用原生缓冲。</summary>
    public void ResetSession()
    {
        _lastRawFrame?.Dispose();
        _lastRawFrame = null;
        _currentFrame?.Dispose();
        _currentFrame = null;
        FrameImage = null;
    }
}
