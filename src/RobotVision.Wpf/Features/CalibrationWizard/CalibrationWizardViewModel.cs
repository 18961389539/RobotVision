using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RobotVision.Core;
using RobotVision.Core.IO;
using RobotVision.Core.Models;
using RobotVision.Hosting;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.CalibrationWizard;

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
public partial class CalibrationWizardViewModel : ObservableObject, ICommitPendingEdits, IPageUnloadAware, IDisposable
{
    private readonly ICameraRuntime _cameras;
    private readonly ICalibrationRuntime _calibration;
    private readonly ICalibrationWizardService _wizard;
    private readonly AppConfig _cfg;
    private readonly IDialogService _dialogs;
    private readonly ILogger<CalibrationWizardViewModel> _log;
    private readonly string _calibrationFolder;
    private readonly PageAsyncSession _pageSession = new();
    public Action? FlushPendingEdits { get; set; }
    private BgraImageBuffer? _lastRawFrame;
    private int _frameWidth;
    private int _frameHeight;
    private bool _lastChessboardFound;
    private IntrinsicProfile? _pendingIntrinsic;
    private ExtrinsicProfile? _pendingExtrinsic;
    private RotationCenterProfile? _pendingRotation;
    private PolynomialProfile? _pendingPolynomial;
    private WizardMode? _pendingResultMode;
    private string _intrinsicTempFolder = "";
    /// <summary>Polynomial 模式：最近一次取图检测到的棋盘角点（点选吸附基准）。</summary>
    private CalibrationCornerPoint[] _chessboardCorners = [];
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
    public IReadOnlyList<CameraOption> CameraOptions =>
        CameraOption.FromRegistered(_cfg.Cameras, _cameras.CameraIds);
    /// <summary>已有标定档案的工位 Id（下拉可选，亦可手输新建）。</summary>
    public IReadOnlyList<string> StationIds => _calibration.ExtrinsicProfiles
        .Select(p => p.StationId)
        .Concat(_calibration.PolynomialProfiles.Select(p => p.StationId))
        .Where(s => !string.IsNullOrWhiteSpace(s))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
        .ToList();
    public ObservableCollection<CalibPointItem> Points { get; } = [];
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowStationIdField))]
    private WizardMode _mode = WizardMode.Polynomial; // 默认快换标定（推荐）：拍 1 张免示教
    /// <summary>内参标定按相机 Id 存档，不需要工位 Id。</summary>
    public bool ShowStationIdField => Mode != WizardMode.Intrinsic;
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
    /// <summary>已采集但未保存为标定档案的数据（切页提示用）。</summary>
    public bool HasUnsavedChanges => CollectedFrames > 0;
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
        ICameraRuntime cameras,
        ICalibrationRuntime calibration,
        ICalibrationWizardService wizard,
        AppConfig cfg,
        IDialogService dialogs,
        ILogger<CalibrationWizardViewModel> log)
    {
        _cameras = cameras;
        _calibration = calibration;
        _wizard = wizard;
        _cfg = cfg;
        _dialogs = dialogs;
        _log = log;
        _calibrationFolder = cfg.ResolveCalibrationFolder();
        SelectedCamera = CameraIds.Count > 0 ? CameraIds[0] : "";
        SelectedModeOption = ModeOptions[0]; // 快换标定（推荐）
    }

    /// <summary>页面再次进入时刷新相机下拉。</summary>
    public void RefreshCameras()
    {
        OnPropertyChanged(nameof(CameraIds));
        OnPropertyChanged(nameof(CameraOptions));
        OnPropertyChanged(nameof(StationIds));
    }

    /// <summary>参数浮动面板可见性（参照相机管理页：图像主导、参数可折叠）。</summary>
    [ObservableProperty]
    private bool _isParamPanelVisible = true;
    [RelayCommand]
    private void ToggleParamPanel() => IsParamPanelVisible = !IsParamPanelVisible;
    partial void OnSelectedCameraChanged(string value)
    {
        // 虚拟相机的棋盘规格固定可推导（默认 1280×960/40px = 15×11 内角点）：
        // 切换到虚拟相机时自动同步，避免按物理棋盘默认 9×6 查询导致检测失败或子网格错位
        if (value.Length == 0 ||
            _cameras.GetVirtualChessboardInnerCorners(value) is not { } chessboard)
            return;
        if (chessboard.InnerWidth is < 3 or > 30 || chessboard.InnerHeight is < 3 or > 30 ||
            (Cols == chessboard.InnerWidth && Rows == chessboard.InnerHeight))
            return;
        Cols = chessboard.InnerWidth;
        Rows = chessboard.InnerHeight;
        Message = $"虚拟相机棋盘内角点已同步为 {Cols}×{Rows}";
    }

    partial void OnModeChanged(WizardMode value)
    {
        Clickable = !IsBusy && value is WizardMode.Extrinsic or WizardMode.Rotation or WizardMode.Polynomial;
        ClearAllPendingResults();
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
    partial void OnIsBusyChanged(bool value)
    {
        NotifyOperationCommandsCanExecuteChanged();
        // 取图进行中禁止图上点选：此时 _chessboardCorners 正被后台线程改写，
        // 点选吸附会读到半成品角点数组（错吸附）
        Clickable = !value && Mode is WizardMode.Extrinsic or WizardMode.Rotation or WizardMode.Polynomial;
    }

    private void NotifyOperationCommandsCanExecuteChanged()
    {
        GrabCommand.NotifyCanExecuteChanged();
        ComputeCommand.NotifyCanExecuteChanged();
        CaptureFrameCommand.NotifyCanExecuteChanged();
        ClearIntrinsicCommand.NotifyCanExecuteChanged();
        RemoveLastPointCommand.NotifyCanExecuteChanged();
        ClearPointsCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        FillMeasuredOffsetCommand.NotifyCanExecuteChanged();
    }

    /// <summary>离开向导页时取消在途任务、排水并释放帧缓冲。</summary>
    public void OnPageUnloading() => ResetSession();
    public void ResetSession()
    {
        _pageSession.Dispose();
        IsBusy = false;
        _lastRawFrame = null;
        _frameWidth = 0;
        _frameHeight = 0;
        FrameImage = null;
        _chessboardCorners = [];
    }

    public void Dispose() => ResetSession();
}
