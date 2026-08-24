using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Collections.ObjectModel;
using OpenCvSharp;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Inference;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Infrastructure.Lighting;
using RobotVision.Core.Models;
using System.Windows.Threading;
using RobotVision.Infrastructure.Communication;

namespace RobotVision.WpfHost;

/// <summary>配方列表项（含有效性/启用状态/描述，供列表展示与过滤）。</summary>
public sealed record RecipeListItem(
    string Name, string Summary, bool IsValid, bool IsEnabled = true, string? Description = null);

/// <summary>枚举选项（界面显示中文标签）。</summary>
public sealed record EnumItem<T>(T Value, string Label) where T : struct, Enum;

public sealed record RecipeAngleModeItem(string Value, string Label);

/// <summary>
/// 配方管理：列表（可搜索）+ 编辑表单。编辑始终作用于克隆副本，保存时校验、写回文件并刷新缓存；
/// 未保存的修改在切换配方/删除时弹确认，防止误丢；新建/改名与已有配方重名时弹覆盖确认。
/// </summary>
public partial class RecipeViewModel : ObservableObject, IDisposable
{
    private readonly RecipeLoader _loader;
    private readonly CameraManager _cameras;
    private readonly ModelManager _models;
    private readonly CalibrationManager _calibration;
    private readonly VisionService _vision;
    private readonly LightingManager _lighting;
    private readonly AngleStrategyTypeRegistry _angleRegistry;
    private readonly TcpServerManager _tcp;
    private readonly DispatcherTimer _dirtyTimer;

    /// <summary>进入当前编辑状态时的基线副本（脏标记比较基准）。</summary>
    private RecipeConfig? _baseline;

    /// <summary>当前编辑配方的磁盘原名（覆盖确认与测试触发用；新建/复制时为空）。</summary>
    private string _originalName = "";

    /// <summary>最近一次确认切换的列表项（未保存确认失败时恢复选中）。</summary>
    private RecipeListItem? _lastConfirmed;

    /// <summary>防止 OnSelectedChanged 重入（程序内设置 Selected 时置位）。</summary>
    private bool _switching;

    /// <summary>
    /// 测试触发的一次性快照捕获标志：测试前置位（记录配方名），快照到达后原子清除。
    /// 不能用 IsBusy 判断——快照回调经 Task.Run 在线程池异步执行，RunAsync 返回后
    /// UI 线程的 finally{IsBusy=false} 可能先于回调执行，竞态导致"测试通过但无图"。
    /// </summary>
    private string? _awaitSnapshotFor;

    public ObservableCollection<RecipeListItem> Recipes { get; } = [];

    [ObservableProperty]
    private RecipeListItem? _selected;

    [ObservableProperty]
    private RecipeConfig _editor = new();

    /// <summary>新建/复制模式下名称可编辑（已有配方改名 = 复制 + 删旧）。</summary>
    [ObservableProperty]
    private bool _isNew;

    [ObservableProperty]
    private string _message = "";

    /// <summary>测试触发进行中（防抖：连点不并发推理）。</summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>测试触发的结果图（位姿叠加后的快照；调试阈值时直接看框/关键点画在哪）。</summary>
    [ObservableProperty]
    private System.Windows.Media.ImageSource? _testImage;

    /// <summary>ROI 预览图（当前相机取一帧 + 检测区域矩形叠加）。</summary>
    [ObservableProperty]
    private System.Windows.Media.ImageSource? _roiPreviewImage;

    /// <summary>参数浮动面板可见性（图像主导布局：收起后图像全幅，参照相机管理页）。</summary>
    [ObservableProperty]
    private bool _isParamPanelVisible = true;

    [RelayCommand]
    private void ToggleParamPanel() => IsParamPanelVisible = !IsParamPanelVisible;

    /// <summary>图像区当前视图：true=测试结果图 / false=ROI 预览图（共用主图像区，顶部标签切换）。
    /// 测试触发完成自动切到结果图；预览区域完成自动切到 ROI 图。</summary>
    [ObservableProperty]
    private bool _showTestImage = true;

    /// <summary>图像区是否有任一图片（控制视图切换标签的显隐）。</summary>
    public bool HasAnyImage => TestImage is not null || RoiPreviewImage is not null;

    [RelayCommand]
    private void ShowTestImageView() => ShowTestImage = true;

    [RelayCommand]
    private void ShowRoiPreviewView() => ShowTestImage = false;

    partial void OnTestImageChanged(System.Windows.Media.ImageSource? value)
    {
        if (value is not null)
            ShowTestImage = true; // 新结果到达：自动切到结果视图
        OnPropertyChanged(nameof(HasAnyImage));
    }

    partial void OnRoiPreviewImageChanged(System.Windows.Media.ImageSource? value)
    {
        if (value is not null)
            ShowTestImage = false; // 新 ROI 预览到达：自动切到 ROI 视图
        OnPropertyChanged(nameof(HasAnyImage));
    }

    [ObservableProperty]
    private bool _includeTriggerPose;

    [ObservableProperty]
    private double _triggerPoseX;

    [ObservableProperty]
    private double _triggerPoseY;

    [ObservableProperty]
    private double _triggerPoseRz;

    [ObservableProperty]
    private string _searchText = "";

    public RecipeViewModel(
        RecipeLoader loader,
        CameraManager cameras,
        ModelManager models,
        CalibrationManager calibration,
        VisionService vision,
        LightingManager lighting,
        AngleStrategyTypeRegistry angleRegistry,
        TcpServerManager tcp)
    {
        _loader = loader;
        _cameras = cameras;
        _models = models;
        _calibration = calibration;
        _vision = vision;
        _lighting = lighting;
        _angleRegistry = angleRegistry;
        _tcp = tcp;
        _dirtyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _dirtyTimer.Tick += (_, _) => NotifyEditorMutated();
        // 测试触发的结果画面：与监控页同一快照通道（成功完成推理后发布），
        // 仅在测试期间捕获（TCP/监控页触发的快照不覆盖本页显示）
        _vision.FrameProcessed += OnTestFrameProcessed;
        Refresh();
    }

    /// <summary>管线快照回调（后台线程）：绘制位姿叠加并转位图。
    /// 一次性消费 _awaitSnapshotFor（与 IsBusy 复位时机解耦，消除竞态），
    /// TCP/监控页触发的快照不覆盖本页显示。</summary>
    private void OnTestFrameProcessed(VisionFrameSnapshot snapshot)
    {
        var image = snapshot.UndistortedImage;
        try
        {
            var expected = Volatile.Read(ref _awaitSnapshotFor);
            if (expected is null || snapshot.RecipeName != expected)
                return;
            if (Interlocked.Exchange(ref _awaitSnapshotFor, null) != expected)
                return;

            OverlayDrawer.DrawPoses(image, snapshot.Poses);
            var source = ImageConverter.ToBitmapSource(image);
            UiDispatch.Begin(() => TestImage = source);
        }
        catch (Exception)
        {
            // 绘制失败不影响管线
        }
        finally
        {
            image.Dispose();
        }
    }

    /// <summary>ROI 预览：当前相机取一帧，按配方 ROI（比例）叠加检测区域矩形。
    /// 四个裸数字框（X/Y/W/H 比例值）无图像对照时现场无从判断对应画面哪块。</summary>
    [RelayCommand]
    private async Task PreviewRoiAsync()
    {
        var cameraId = Editor.CameraId;
        if (string.IsNullOrWhiteSpace(cameraId))
        {
            Message = "ROI 预览：请先选择相机";
            return;
        }

        IsBusy = true;
        try
        {
            var roi = Editor.Roi;
            Message = $"ROI 预览取图中 · {cameraId} …";
            var source = await Task.Run(() =>
            {
                using var frame = _cameras.Grab(cameraId).Image;
                using var preview = frame.Clone();
                // 全图推理（Roi=null）也画整框：让操作员确认"当前是全图"这一事实
                var r = roi ?? new Roi(0, 0, 1, 1);
                var rect = new OpenCvSharp.Rect(
                    (int)(r.X * preview.Width), (int)(r.Y * preview.Height),
                    (int)(r.Width * preview.Width), (int)(r.Height * preview.Height));
                Cv2.Rectangle(preview, rect, new OpenCvSharp.Scalar(0, 200, 255), 2);
                Cv2.PutText(preview, "ROI", new OpenCvSharp.Point(rect.X + 6, Math.Max(rect.Y + 22, 22)),
                    OpenCvSharp.HersheyFonts.HersheySimplex, 0.6, new OpenCvSharp.Scalar(0, 200, 255), 2);
                return ImageConverter.ToBitmapSource(preview);
            });
            RoiPreviewImage = source;
            Message = roi is null
                ? $"ROI 预览（{cameraId}）：当前为全图推理"
                : $"ROI 预览（{cameraId}）：检测区域 = ({roi.X:0.00},{roi.Y:0.00}) ~ ({roi.X + roi.Width:0.00},{roi.Y + roi.Height:0.00})（比例）";
        }
        catch (Exception ex)
        {
            Message = $"ROI 预览失败: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>配方页可见时轮询脏标记：Editor.* 绑定不经过 ViewModel setter。</summary>
    public void StartDirtyWatch() => _dirtyTimer.Start();

    public void StopDirtyWatch() => _dirtyTimer.Stop();

    /// <summary>刷新未保存横幅与按模式显隐（角度/工位等 Editor 属性变更后）。</summary>
    public void NotifyEditorMutated()
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
        OnPropertyChanged(nameof(RotationCenterHint));
        OnPropertyChanged(nameof(MappingHint));
        OnPropertyChanged(nameof(AngleModeHint));
        OnPropertyChanged(nameof(IsDualMode));
        OnPropertyChanged(nameof(IsKeyPointMode));
        OnPropertyChanged(nameof(IsSegmentationMode));
        OnPropertyChanged(nameof(IsMaskTemplateMode));
        OnPropertyChanged(nameof(IsTemplateMethod));
        OnPropertyChanged(nameof(HasTemplate));
        OnPropertyChanged(nameof(PrimaryModel));
        OnPropertyChanged(nameof(SecondaryModel));
    }

    /// <summary>
    /// 示教模板（MaskTemplate 模式）：点亮配方光源 → 取图 → 分割 → 置信度最高的目标转正裁剪 →
    /// base64 内嵌配方。变换与运行时策略共用 MaskTemplateMatcher，坐标系一致。
    /// </summary>
    [RelayCommand]
    private async Task TeachTemplateAsync()
    {
        var cameraId = Editor.CameraId;
        if (string.IsNullOrWhiteSpace(cameraId))
        {
            Message = "示教模板：请先选择相机";
            return;
        }
        if (Editor.Models.Count == 0 || string.IsNullOrWhiteSpace(Editor.Models[0]))
        {
            Message = "示教模板：请先选择分割模型";
            return;
        }

        IsBusy = true;
        try
        {
            Message = $"示教模板取图中 · {cameraId} …";
            using var lightingScope = _lighting.Apply(Editor.LightControllerId, Editor.Lighting);
            if (lightingScope.StabilizeDelayMs > 0)
                await Task.Delay(lightingScope.StabilizeDelayMs);

            var (b64, w, h) = await Task.Run(() =>
            {
                using var frame = _cameras.Grab(cameraId).Image;
                // 与管线一致的去畸变：多项式工位用原图（多项式吸收畸变），否则内参去畸变；
                // 未做内参标定（台架调试）退回原图，模板坐标系仍与触发时一致（同相机同路径）
                Mat image;
                Mat? owned;
                if (!string.IsNullOrEmpty(Editor.StationId) && _calibration.HasPolynomial(Editor.StationId))
                {
                    image = frame;
                    owned = null;
                }
                else
                {
                    try
                    {
                        owned = _calibration.Undistort(cameraId, frame);
                        image = owned;
                    }
                    catch (Core.VisionException)
                    {
                        owned = null;
                        image = frame;
                    }
                }

                using var ownedScope = owned;
                // 推理在 ROI 内，裁剪也用 ROI 视图：轮廓坐标与像素坐标同一坐标系
                Mat roiView = image;
                Mat? roiOwned = Editor.Roi is null ? null : RoiHelper.Crop(image, Editor.Roi, out _, out _);
                try
                {
                    if (roiOwned is not null)
                        roiView = roiOwned;
                    using var bitmap = RoiHelper.ToBitmap(roiView, null, out _, out _);
                    var session = _models.Open(Editor.Models[0], InferenceTask.Segmentation);
                    var results = session.Run(y => y.RunSegmentation(
                        bitmap, Editor.Confidence, Editor.Segmentation.PixelConfidence, Editor.Iou));

                    // 最优目标：置信度最高且轮廓有效（与运行时同口径的面积/点数下限）
                    foreach (var seg in results.OrderByDescending(s => s.Confidence))
                    {
                        var box = seg.BoundingBox;
                        if ((double)box.Width * box.Height < 400)
                            continue;
                        var contour = seg.GetContourPoints();
                        if (contour.Length < 4)
                            continue;

                        var points = new Point2f[contour.Length];
                        for (var i = 0; i < contour.Length; i++)
                            points[i] = new Point2f(contour[i].X + box.Left, contour[i].Y + box.Top);

                        // 紧裁剪（无边距）：运行时旋转搜索的滑窗目标就是这块
                        var crop = MaskTemplateMatcher.UprightCrop(roiView, points, 0);
                        using (crop.Upright)
                            return (MaskTemplateMatcher.EncodeTemplatePng(crop.Upright), crop.Upright.Width, crop.Upright.Height);
                    }
                    throw new InvalidOperationException("分割未检出有效目标，无法示教（请确认模型/阈值/画面内有目标）");
                }
                finally
                {
                    roiOwned?.Dispose();
                }
            });
            lightingScope.Dispose();

            Editor.Template.TemplateImageBase64 = b64;
            OnPropertyChanged(nameof(HasTemplate));
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(UnsavedHint));
            Message = $"模板已示教（{w}×{h}px）· 保存配方后生效";
        }
        catch (Exception ex)
        {
            Message = $"示教模板失败: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>相机增删后通知下拉重新求值（页面 Loaded 时调用）。</summary>
    public void RefreshCameras() => OnPropertyChanged(nameof(CameraIds));

    public IReadOnlyList<string> CameraIds => _cameras.CameraIds.ToList();

    /// <summary>models 目录 .onnx 文件列表（下拉选择，避免手填拼错）。</summary>
    public IReadOnlyList<string> ModelFiles => _models.ModelFileNames;

    /// <summary>已标定工位列表（外参档案 + 多项式档案去重，下拉选择避免手填触发 1004）。
    /// 多项式工位（快换标定/机器人坐标标定）同样可被配方引用。</summary>
    public IReadOnlyList<string> StationIds => _calibration.ExtrinsicProfiles
        .Select(p => p.StationId)
        .Concat(_calibration.PolynomialProfiles.Select(p => p.StationId))
        .Where(s => !string.IsNullOrWhiteSpace(s))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
        .ToList();

    /// <summary>偏心补偿预警：开启补偿但工位缺旋转中心档案（保存/触发都会失败，表单阶段提示）。</summary>
    public string RotationCenterHint =>
        Editor.RotationCompensation == Core.Recipe.RotationCompensationMode.EccentricTool &&
        (string.IsNullOrWhiteSpace(Editor.StationId) ||
         !_calibration.RotationCenterProfiles.Any(p =>
             string.Equals(p.StationId, Editor.StationId, StringComparison.OrdinalIgnoreCase)))
            ? $"工位 {Editor.StationId ?? "（空）"} 未做旋转轴心标定：偏心补偿保存/触发将被拒绝，请先在标定向导完成轴心标定"
            : "";

    /// <summary>工位映射提示：多项式优先、棋盘毫米系、双档案并存。</summary>
    public string MappingHint
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Editor.StationId))
                return Editor.DebugPassthrough
                    ? "台架直通已开启：TRIGGER 返回像素坐标，禁止对接 PLC"
                    : "";
            var station = Editor.StationId!;
            var poly = _calibration.PolynomialProfiles.FirstOrDefault(p =>
                string.Equals(p.StationId, station, StringComparison.OrdinalIgnoreCase));
            var hasExt = _calibration.HasExtrinsic(station);
            if (poly is not null && hasExt)
                return $"工位 {station} 同时有多项式与外参：生产只用多项式（原图），外参被忽略";
            if (poly is not null &&
                string.Equals(poly.CoordinateSpace, PolynomialCoordinateSpace.Image, StringComparison.OrdinalIgnoreCase))
                return $"工位 {station} 为棋盘毫米系（非机器人基座标），PLC 不能直接当 TCP 坐标使用";
            if (poly is not null &&
                string.Equals(poly.MountType, CameraMountType.OnArm, StringComparison.OrdinalIgnoreCase) &&
                poly.HasTeachPose)
                return $"工位 {station} 为末端相机：TRIGGER 必须带 X,Y,RZ，否则 1014";
            var ext = _calibration.ExtrinsicProfiles.FirstOrDefault(p =>
                string.Equals(p.StationId, station, StringComparison.OrdinalIgnoreCase));
            if (ext is not null &&
                string.Equals(ext.MountType, CameraMountType.OnArm, StringComparison.OrdinalIgnoreCase) &&
                ext.HasTeachPose)
                return $"工位 {station} 为末端相机：TRIGGER 必须带 X,Y,RZ，否则 1014";
            return "";
        }
    }

    /// <summary>角度模式现场提示：最小外接矩形 180° 歧义、双模型就近配对。</summary>
    public string AngleModeHint => Editor.AngleMode switch
    {
        AngleMode.MaskMinAreaRect => "最小外接矩形角度为 [0,180)，无头尾方向；偏心工具补偿可能差 180°",
        AngleMode.DualCenterLine => "双模型连线为一对一就近配对，多目标间距接近时可能配错",
        AngleMode.MaskTemplate => "模板匹配失败会回退粗角度 [0,180)（无方向）；示教须与生产同一套照明",
        _ => "",
    };

    /// <summary>已注册光源控制器列表（下拉选择；为空 = 未配置任何光源）。</summary>
    public IReadOnlyList<string> LightControllerIds => _lighting.ControllerIds.ToList();

    // ---- 光源（扁平编辑字段，直接作用于 Editor.Lighting/LightControllerId，参与脏标记）----

    /// <summary>是否启用光源（取图前点亮）。</summary>
    public bool UseLighting
    {
        get => Editor.Lighting is not null;
        set
        {
            if (value)
            {
                Editor.Lighting ??= NewLightingConfig();
                Editor.LightControllerId ??= LightControllerIds.FirstOrDefault();
            }
            else
            {
                Editor.Lighting = null;
                Editor.LightControllerId = null;
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
        }
    }

    public string? SelectedLightControllerId
    {
        get => Editor.LightControllerId;
        set
        {
            Editor.LightControllerId = string.IsNullOrWhiteSpace(value) ? null : value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
        }
    }

    public int LightChannel
    {
        get => Editor.Lighting?.Channels.FirstOrDefault()?.Channel ?? 1;
        set
        {
            if (Editor.Lighting is { } l)
            {
                Channel0(l).Channel = Math.Max(1, value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
            }
        }
    }

    public int LightBrightness
    {
        get => Editor.Lighting?.Channels.FirstOrDefault()?.Brightness ?? 128;
        set
        {
            if (Editor.Lighting is { } l)
            {
                Channel0(l).Brightness = Math.Clamp(value, 0, 255);
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
            }
        }
    }

    public int LightStabilizeDelayMs
    {
        get => Editor.Lighting?.StabilizeDelayMs ?? 0;
        set
        {
            if (Editor.Lighting is { } l)
            {
                l.StabilizeDelayMs = Math.Max(0, value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
            }
        }
    }

    public bool LightTurnOffAfterGrab
    {
        get => Editor.Lighting?.TurnOffAfterGrab ?? true;
        set
        {
            if (Editor.Lighting is { } l)
            {
                l.TurnOffAfterGrab = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
            }
        }
    }

    private static LightingConfig NewLightingConfig() => new()
    {
        Channels = [new LightingChannelConfig { Channel = 1, Brightness = 128 }],
        StabilizeDelayMs = 0,
        TurnOffAfterGrab = true,
    };

    private static LightingChannelConfig Channel0(LightingConfig lighting)
    {
        if (lighting.Channels.Count == 0)
            lighting.Channels.Add(new LightingChannelConfig());
        return lighting.Channels[0];
    }

    // ---- 检测区域 ROI（相对比例 0~1；null = 全图推理）----

    /// <summary>是否启用检测区域（裁剪后推理，大图局部检测显著降低 CPU 耗时）。</summary>
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
            OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
        }
    }

    public double RoiX
    {
        get => Editor.Roi?.X ?? 0;
        set => SetRoi(r => r with { X = Math.Clamp(value, 0, 1) });
    }

    public double RoiY
    {
        get => Editor.Roi?.Y ?? 0;
        set => SetRoi(r => r with { Y = Math.Clamp(value, 0, 1) });
    }

    public double RoiWidth
    {
        get => Editor.Roi?.Width ?? 1;
        set => SetRoi(r => r with { Width = Math.Clamp(value, 0, 1) });
    }

    public double RoiHeight
    {
        get => Editor.Roi?.Height ?? 1;
        set => SetRoi(r => r with { Height = Math.Clamp(value, 0, 1) });
    }

    private void SetRoi(Func<Roi, Roi> update)
    {
        if (Editor.Roi is { } roi)
        {
            Editor.Roi = update(roi);
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
        }
    }

    /// <summary>按名称/描述过滤后的列表（搜索框驱动）。</summary>
    public IEnumerable<RecipeListItem> VisibleRecipes =>
        string.IsNullOrWhiteSpace(SearchText)
            ? Recipes
            : Recipes.Where(r =>
                r.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                (r.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false));

    /// <summary>编辑内容相对保存态是否有差异（脏标记）。</summary>
    public bool HasUnsavedChanges =>
        _baseline is not null && !SameRecipe(Editor, _baseline);

    /// <summary>有未保存修改时的常驻提示（UI 提示条）。</summary>
    public string UnsavedHint => HasUnsavedChanges
        ? "有未保存的修改：测试触发仍用磁盘旧配方，切换/刷新将丢弃"
        : "";

    /// <summary>未保存修改确认框；用户拒绝时返回 false 并中止当前操作。</summary>
    private bool ConfirmDiscard(string action)
    {
        return MessageBox.Show($"配方 {_originalName} 有未保存的修改，{action}将丢弃这些修改。继续？",
            "未保存修改", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    /// <summary>仅当选中配方有效时可测试触发。</summary>
    public bool CanTestTrigger => Selected is { IsValid: true };

    /// <summary>角度模式下拉数据源：来自策略工厂注册表（新注册的工厂自动出现，与相机类型下拉同构）。</summary>
    public IReadOnlyList<EnumItem<AngleMode>> AngleModeOptions =>
        _angleRegistry.Factories
            .Select(f => new EnumItem<AngleMode>(f.Mode, f.Label))
            .ToList();

    public IReadOnlyList<EnumItem<Core.Recipe.RotationCompensationMode>> RotationOptions { get; } =
    [
        new(Core.Recipe.RotationCompensationMode.None, "不补偿"),
        new(Core.Recipe.RotationCompensationMode.EccentricTool, "偏心工具补偿（需旋转中心标定）"),
    ];

    public string PrimaryModel
    {
        get => Editor.Models.Count > 0 ? Editor.Models[0] : "";
        set
        {
            while (Editor.Models.Count < 1)
                Editor.Models.Add("");
            Editor.Models[0] = value;
            NotifyEditorMutated();
        }
    }

    public string SecondaryModel
    {
        get => Editor.Models.Count > 1 ? Editor.Models[1] : "";
        set
        {
            while (Editor.Models.Count < 2)
                Editor.Models.Add("");
            Editor.Models[1] = value;
            NotifyEditorMutated();
        }
    }

    partial void OnSearchTextChanged(string value) => OnPropertyChanged(nameof(VisibleRecipes));

    partial void OnEditorChanged(RecipeConfig value) => NotifyEditorMutated();

    /// <summary>角度模式联动显隐：次模型/配对距离仅双模型；关键点参数仅关键点；掩码置信度仅分割；
    /// 模板参数仅分割+模板匹配。Editor 属性级绑定不触发 OnEditorChanged，AngleMode 的编辑器联动见 NotifyEditorBindings。</summary>
    public bool IsDualMode => Editor.AngleMode == AngleMode.DualCenterLine;
    public bool IsKeyPointMode => Editor.AngleMode == AngleMode.KeyPointLine;
    public bool IsSegmentationMode => Editor.AngleMode == AngleMode.MaskMinAreaRect;
    public bool IsMaskTemplateMode => Editor.AngleMode == AngleMode.MaskTemplate;

    /// <summary>是否已示教模板（模板参数卡状态提示；示教/换配方后通知）。</summary>
    public bool HasTemplate => !string.IsNullOrEmpty(Editor.Template.TemplateImageBase64);

    /// <summary>精修方法下拉（分割+精修模式）：模板匹配（吃纹理可判头尾）/ 直线拟合（吃轮廓，弱纹理适用）/
    /// 质心-内孔连线（吃掩码孔洞，有方向）。</summary>
    public IReadOnlyList<EnumItem<SegmentRefineMethod>> RefineMethodOptions { get; } =
    [
        new(SegmentRefineMethod.Template, "模板匹配（需示教，可判头尾）"),
        new(SegmentRefineMethod.LineFit, "直线拟合（弱纹理矩形，免示教）"),
        new(SegmentRefineMethod.CentroidHoleLine, "质心-内标连线（掩码有孔/槽，有方向）"),
    ];

    /// <summary>当前精修方法为模板匹配（控制模板专属参数/示教按钮显隐）。</summary>
    public bool IsTemplateMethod => Editor.Template.RefineMethod == SegmentRefineMethod.Template;

    /// <summary>角度模式切换联动（页面 SelectionChanged 调用）：刷新按模式显隐的派生属性。</summary>
    public void NotifyAngleModeChanged() => NotifyEditorMutated();

    [RelayCommand]
    public void Refresh()
    {
        // 未保存修改：确认后才丢弃（与相机页一致）
        if (HasUnsavedChanges && !ConfirmDiscard("刷新列表"))
            return;

        Recipes.Clear();
        foreach (var name in _loader.ListNames())
            Recipes.Add(DescribeItem(name));

        // 下拉数据源：models 目录 / 外参档案 / 光源控制器可能已变化
        OnPropertyChanged(nameof(ModelFiles));
        OnPropertyChanged(nameof(StationIds));
        OnPropertyChanged(nameof(LightControllerIds));

        _switching = true;
        Selected = Recipes.FirstOrDefault(r => r.Name == Selected?.Name) ?? Recipes.FirstOrDefault();
        _switching = false;
        _lastConfirmed = Selected;
        Message = $"共 {Recipes.Count} 个配方";
        OnPropertyChanged(nameof(VisibleRecipes));
        OnPropertyChanged(nameof(CanTestTrigger));
        TestTriggerCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void New()
    {
        // 未保存修改：确认后才丢弃
        if (HasUnsavedChanges && !ConfirmDiscard("新建配方"))
            return;
        IsNew = true;
        _originalName = "";
        Editor = new RecipeConfig
        {
            Name = "",
            CameraId = CameraIds.FirstOrDefault() ?? "",
            Models = [""],
        };
        _baseline = Editor.Clone();
        NotifyEditorBindings();
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
        OnPropertyChanged(nameof(CanTestTrigger));
        TestTriggerCommand.NotifyCanExecuteChanged();
        Message = "新建配方：填写名称与参数后保存";
    }

    [RelayCommand]
    private void Copy()
    {
        // 未保存修改：确认后才丢弃
        if (HasUnsavedChanges && !ConfirmDiscard("复制配方"))
            return;
        var source = Editor;
        var copy = source.Clone();
        copy.Name = source.Name.Length > 0 ? source.Name + "_copy" : "";
        IsNew = true;
        _originalName = "";
        Editor = copy;
        _baseline = copy.Clone();
        NotifyEditorBindings();
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
        OnPropertyChanged(nameof(CanTestTrigger));
        TestTriggerCommand.NotifyCanExecuteChanged();
        Message = $"已复制 {source.Name}：改名后保存即新配方";
    }

    [RelayCommand]
    private void Save()
    {
        try
        {
            // 空名/非法名先给友好提示（RecipeLoader 的校验文案面向配置格式，不面向表单）
            if (string.IsNullOrWhiteSpace(Editor.Name))
            {
                Message = "保存失败：请先填写配方名称";
                return;
            }
            if (!RecipeLoader.IsValidRecipeName(Editor.Name))
            {
                Message = "保存失败：名称只允许字母、数字、下划线、中划线（长度 ≤ 64）";
                return;
            }

            // 重名覆盖确认：新建/改名后目标名已存在且非当前原名时提示
            var isRename = IsNew || !string.Equals(Editor.Name, _originalName, StringComparison.OrdinalIgnoreCase);
            if (isRename && _loader.FileExists(Editor.Name) &&
                MessageBox.Show($"配方 {Editor.Name} 已存在，保存将覆盖现有内容。继续？",
                    "覆盖确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            // 双模型模式保留两个条目，其余压缩为单个（空串过滤）
            var models = new[] { PrimaryModel, SecondaryModel }
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            Editor.Models = models;

            // 光源成对校验：启用光源必须有控制器（值域校验由 RecipeLoader 兜底）
            if (Editor.Lighting is not null && string.IsNullOrWhiteSpace(Editor.LightControllerId))
            {
                Message = "保存失败：已启用光源但未选择光源控制器（appsettings LightControllers 未配置时先添加 None 类型）";
                return;
            }

            _loader.Save(Editor);

            // 改名 = 移动语义：删除旧名文件，避免双配方同名异体（PLC 触发旧名走旧参数）
            if (isRename && _originalName.Length > 0 &&
                !string.Equals(Editor.Name, _originalName, StringComparison.OrdinalIgnoreCase))
            {
                _loader.Delete(_originalName);
                Message = $"已保存 {Editor.Name}（原 {_originalName} 已重命名，旧文件已删除）";
            }
            else
            {
                Message = $"已保存 {Editor.Name}";
            }

            IsNew = false;
            _originalName = Editor.Name;
            _baseline = Editor.Clone();
            OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
            Refresh();
        }
        catch (Exception ex)
        {
            Message = $"保存失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private void Delete()
    {
        if (Selected is null)
            return;

        // 提示对象以实际删除的列表项为准（编辑器可能是改名后的未保存态，原名已无意义）
        var prompt = HasUnsavedChanges && string.Equals(Selected.Name, _originalName, StringComparison.OrdinalIgnoreCase)
            ? $"配方 {Selected.Name} 有未保存的修改，删除将一并丢弃。确定删除？（不可恢复）"
            : $"确定删除配方 {Selected.Name}？（不可恢复）";

        if (MessageBox.Show(prompt, "删除配方",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        try
        {
            _loader.Delete(Selected.Name);
            Message = $"已删除 {Selected.Name}";
            _lastConfirmed = null;
            Refresh();
            // 重新载入列表首项，让编辑器离开被删配方
            if (Selected is { } item)
            {
                LoadIntoEditor(item.Name);
                _lastConfirmed = Selected;
            }
        }
        catch (Exception ex)
        {
            Message = $"删除失败：{ex.Message}";
        }
    }

    /// <summary>测试触发：用磁盘上的配方跑一次完整链路（取图→去畸变→推理→外参）。</summary>
    private bool CanOperate => !IsBusy && CanTestTrigger;

    partial void OnIsBusyChanged(bool value) => TestTriggerCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task TestTriggerAsync()
    {
        if (string.IsNullOrEmpty(_originalName))
            return;

        if (HasUnsavedChanges &&
            MessageBox.Show("有未保存的修改：测试触发仍用磁盘上的旧配方。继续？",
                "未保存修改", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        IsBusy = true;
        TestImage = null; // 清上一轮结果图（失败时不显示旧画面误导）
        _awaitSnapshotFor = _originalName; // 置位一次性捕获标志（见字段注释）
        try
        {
            Message = $"测试触发中：{_originalName} …";
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(Math.Max(500, _tcp.TimeoutMs)));
            TcpClientPose? pose = IncludeTriggerPose
                ? new TcpClientPose(TriggerPoseX, TriggerPoseY, TriggerPoseRz)
                : null;
            var result = pose is null
                ? await _vision.RunAsync(_originalName, cts.Token)
                : await _vision.RunAsync(_originalName, pose, cts.Token);
            if (result.Ok)
            {
                // 成功：保持标志置位——快照回调可能在 RunAsync 返回后才执行，
                // 由回调一次性消费；finally 清除会重现"测试通过但无图"竞态
                Message = $"测试通过：{result.RecipeName} · {result.Poses.Count} 个目标 · {result.ElapsedMs:0}ms";
            }
            else
            {
                // 失败：清标志，防之后 TCP 触发的同配方快照被误当作测试结果显示
                Interlocked.Exchange(ref _awaitSnapshotFor, null);
                Message = $"测试失败：ERR {result.ErrorCode} · {result.Message}";
            }
        }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref _awaitSnapshotFor, null);
            Message = $"测试异常：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenFolder() => ShellOpen(_loader.Folder);

    partial void OnSelectedChanged(RecipeListItem? value)
    {
        if (_switching || value is null)
            return;

        // 未保存修改：确认后才丢弃
        if (_lastConfirmed is not null && HasUnsavedChanges &&
            MessageBox.Show($"配方 {_originalName} 有未保存的修改，切换将丢弃这些修改。继续？",
                "未保存修改", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            _switching = true;
            Selected = _lastConfirmed;
            _switching = false;
            return;
        }

        LoadIntoEditor(value.Name);
        _lastConfirmed = value;
    }

    /// <summary>把磁盘配方载入编辑器并设置脏标记基线。</summary>
    private void LoadIntoEditor(string name)
    {
        IsNew = false;
        _originalName = name;
        try
        {
            var loaded = _loader.Get(name);
            Editor = loaded.Clone();
            _baseline = Editor.Clone();
            NotifyEditorBindings();
            Message = loaded.Enabled ? "" : $"配方 {name} 已停用（Enabled=false），触发将返回 1001";
        }
        catch (Exception ex)
        {
            Editor = new RecipeConfig { Name = name };
            _baseline = Editor.Clone();
            NotifyEditorBindings();
            Message = $"读取失败：{ex.Message}";
        }
        finally
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedHint));
            OnPropertyChanged(nameof(CanTestTrigger));
            TestTriggerCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>Editor 替换后，通知派生绑定（模型文件名代理属性、光源扁平字段）重新求值。</summary>
    private void NotifyEditorBindings()
    {
        OnPropertyChanged(nameof(PrimaryModel));
        OnPropertyChanged(nameof(SecondaryModel));
        OnPropertyChanged(nameof(UseLighting));
        OnPropertyChanged(nameof(SelectedLightControllerId));
        OnPropertyChanged(nameof(LightChannel));
        OnPropertyChanged(nameof(LightBrightness));
        OnPropertyChanged(nameof(LightStabilizeDelayMs));
        OnPropertyChanged(nameof(LightTurnOffAfterGrab));
        OnPropertyChanged(nameof(UseRoi));
        OnPropertyChanged(nameof(RoiX));
        OnPropertyChanged(nameof(RoiY));
        OnPropertyChanged(nameof(RoiWidth));
        OnPropertyChanged(nameof(RoiHeight));
        OnPropertyChanged(nameof(RotationCenterHint));
        OnPropertyChanged(nameof(IsDualMode));
        OnPropertyChanged(nameof(IsKeyPointMode));
        OnPropertyChanged(nameof(IsSegmentationMode));
        OnPropertyChanged(nameof(IsTemplateMethod));
        OnPropertyChanged(nameof(HasTemplate));
    }

    private RecipeListItem DescribeItem(string name)
    {
        try
        {
            var r = _loader.Get(name);
            var mode = r.AngleMode switch
            {
                AngleMode.MaskMinAreaRect => "分割",
                AngleMode.DualCenterLine => "双模型",
                AngleMode.KeyPointLine => "关键点",
                AngleMode.MaskTemplate => "分割+精修",
                _ => r.AngleMode.ToString(),
            };
            // 信息密度：模式/相机/主模型之外，工位、ROI、光源直接可见——
            // 现场找"哪个配方开了 ROI / 用了光源"不再逐个点开
            var tags = new List<string> { mode, r.CameraId, r.Models.FirstOrDefault("") };
            if (!string.IsNullOrWhiteSpace(r.StationId))
                tags.Add($"工位:{r.StationId}");
            if (r.Roi is not null)
                tags.Add("ROI");
            if (r.Lighting is not null)
                tags.Add($"光:{r.LightControllerId}");
            if (r.DebugPassthrough)
                tags.Add("直通");
            return new RecipeListItem(name, string.Join(" · ", tags), true, r.Enabled, r.Description);
        }
        catch (Exception ex)
        {
            return new RecipeListItem(name, ex.Message, false);
        }
    }

    /// <summary>配方比较（脏标记用）：序列化为 JSON 后比对。
    /// 此前为手写字段清单——RecipeConfig 新增字段时清单容易漏更（脏标记失灵 → 静默丢改动），
    /// 序列化比对对字段演进免疫（克隆链路保证两对象属性值即全部差异来源）。</summary>
    private static bool SameRecipe(RecipeConfig a, RecipeConfig b) =>
        JsonSerializer.Serialize(a, CompareOptions) == JsonSerializer.Serialize(b, CompareOptions);

    private static readonly JsonSerializerOptions CompareOptions = new()
    {
        // 与保存一致：枚举转字符串，保证语义相同写法不同的值不影响比较
        Converters = { new JsonStringEnumConverter() },
    };

    internal static void ShellOpen(string path)
    {
        // 目录缺失不静默重建：静默重建会掩盖"配方目录曾被误删"的异常（ListNames 会显示空列表）
        if (!Directory.Exists(path))
        {
            MessageBox.Show($"目录不存在: {path}\n（配方目录缺失属异常，请检查是否被移动/误删）",
                "打开目录", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{path}\"")
        {
            UseShellExecute = true,
        });
    }

    public void Dispose()
    {
        _dirtyTimer.Stop();
        _vision.FrameProcessed -= OnTestFrameProcessed;
    }
}
