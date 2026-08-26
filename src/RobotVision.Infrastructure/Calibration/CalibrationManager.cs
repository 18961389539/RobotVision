using System.Collections.Concurrent;
using System.Text.Json;
using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Geometry;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure;

namespace RobotVision.Infrastructure.Calibration;

/// <summary>标定质量评估结果（与 README 验收参考对齐）。</summary>
public enum CalibrationQuality
{
    /// <summary>优于验收参考。</summary>
    Good,

    /// <summary>可用（参考区间内）。</summary>
    Fair,

    /// <summary>超标，建议重新标定。</summary>
    Poor,
}

/// <summary>工位坐标映射模式（管线分发用），优先级：多项式 &gt; 外参 &gt; 比例。
/// 见 <see cref="CalibrationManager.GetMappingMode"/>。</summary>
public enum StationMappingMode
{
    /// <summary>无映射档案：外参路径报 1004。</summary>
    None,

    /// <summary>多项式标定（单图模式：原图推理，像素→机器人/棋盘毫米系）。</summary>
    Polynomial,

    /// <summary>外参仿射（去畸变图像推理，像素→机器人系）。</summary>
    Extrinsic,

    /// <summary>比例标定（单图模式：原图推理，像素→图像平面毫米；无标定板工位的回退路径）。</summary>
    Scale,
}

/// <summary>
/// 标定管理类：
/// 1. 内参档案（按相机 Id）——去畸变 Remap，推理前强制要求已标定；
/// 2. 外参档案（按工位 Id）——像素坐标到机器人坐标的仿射变换；
/// 3. 旋转中心档案（按工位 Id）——第 4 轴轴心，偏心工具补偿用。
/// 一致性铁律：去畸变后的图像 = 推理输入 = 外参/旋转中心标定时的图像坐标系。
/// 并发安全：Undistort 与 LoadIntrinsic/DeleteIntrinsic 通过 ReaderWriterLockSlim
/// 互斥，防止热加载替换档案时 Remap 使用已释放的 OpenCV Mat。
/// </summary>
public sealed class CalibrationManager : IDisposable
{
    // 验收阈值（README：内参 RMS ≤0.3 优秀 / ≤0.5 可用；外参最大残差 ≤0.1 优秀 / ≤0.5 可用；
    // 旋转中心 RMS ≤0.3 优秀 / ≤0.5 可用，长短轴比 ≤1.2）
    public const double IntrinsicRmsGood = 0.3;
    public const double IntrinsicRmsFair = 0.5;
    public const double ExtrinsicResidualGood = 0.1;
    public const double ExtrinsicResidualFair = 0.5;
    public const double RotationRmsGood = 0.3;
    public const double RotationRmsFair = 0.5;
    public const double RotationAxisRatioLimit = 1.2;

    private sealed record IntrinsicState(IntrinsicProfile Profile, Mat MapX, Mat MapY);

    private readonly ConcurrentDictionary<string, IntrinsicState> _intrinsics = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ExtrinsicProfile> _extrinsics = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, RotationCenterProfile> _rotationCenters = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, PolynomialProfile> _polynomials = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ScaleProfile> _scales = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<string> _qualityWarnings = new();

    /// <summary>质量警告保留上限：只保留最近 N 条，避免加载大量档案时无限累积。</summary>
    private const int MaxQualityWarnings = 50;

    private readonly ReaderWriterLockSlim _intrinsicLock = new();
    private string? _folder;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public int IntrinsicCount => _intrinsics.Count;

    public int ExtrinsicCount => _extrinsics.Count;

    public int RotationCenterCount => _rotationCenters.Count;

    public int PolynomialCount => _polynomials.Count;

    public int ScaleCount => _scales.Count;

    /// <summary>已加载的比例标定档案（供管理界面显示）。</summary>
    public IReadOnlyList<ScaleProfile> ScaleProfiles =>
        _scales.Values.OrderBy(p => p.StationId, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>工位比例档案（像素→毫米换算，测量显示用）。无档案返回 null。</summary>
    public ScaleProfile? GetScale(string? stationId) =>
        string.IsNullOrEmpty(stationId) ? null : _scales.TryGetValue(stationId, out var p) ? p : null;

    /// <summary>已加载的多项式标定档案（供管理界面显示）。</summary>
    public IReadOnlyList<PolynomialProfile> PolynomialProfiles =>
        _polynomials.Values.OrderBy(p => p.StationId, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>工位是否使用多项式标定（单图模式）：管线据此跳过内参去畸变、走多项式映射。</summary>
    public bool HasPolynomial(string? stationId) =>
        !string.IsNullOrEmpty(stationId) && _polynomials.ContainsKey(stationId);

    /// <summary>工位是否已有外参档案。</summary>
    public bool HasExtrinsic(string? stationId) =>
        !string.IsNullOrEmpty(stationId) && _extrinsics.ContainsKey(stationId);

    /// <summary>工位映射模式：管线据此选择坐标换算路径与图像预处理（去畸变与否）。
    /// 优先级：多项式 &gt; 外参 &gt; 比例——外参/多项式输出机器人系坐标（生产首选），
    /// 比例仅是无标定板工位的回退：输出图像平面毫米（原点=图像左上角，X 右 Y 下，与 UI 预览同向）。
    /// None 时走外参路径（PixelToRobot 统一报 1004）。</summary>
    public StationMappingMode GetMappingMode(string? stationId)
    {
        if (string.IsNullOrEmpty(stationId))
            return StationMappingMode.None;
        if (_polynomials.ContainsKey(stationId))
            return StationMappingMode.Polynomial;
        if (_extrinsics.ContainsKey(stationId))
            return StationMappingMode.Extrinsic;
        if (_scales.ContainsKey(stationId))
            return StationMappingMode.Scale;
        return StationMappingMode.None;
    }

    /// <summary>加载时发现的质量超标警告（供启动日志/UI 展示）。</summary>
    public IReadOnlyList<string> QualityWarnings => _qualityWarnings.ToArray();

    /// <summary>已加载的内参档案（供管理界面显示）。</summary>
    public IReadOnlyList<IntrinsicProfile> IntrinsicProfiles =>
        _intrinsics.Values.Select(s => s.Profile).OrderBy(p => p.CameraId, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>已加载的外参档案（供管理界面显示）。</summary>
    public IReadOnlyList<ExtrinsicProfile> ExtrinsicProfiles =>
        _extrinsics.Values.OrderBy(p => p.StationId, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>已加载的旋转中心档案（供管理界面显示）。</summary>
    public IReadOnlyList<RotationCenterProfile> RotationCenterProfiles =>
        _rotationCenters.Values.OrderBy(p => p.StationId, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>
    /// 扫描目录加载 *.intrinsic.json / *.extrinsic.json / *.rotation.json。
    /// 返回加载失败清单（坏档案隔离，不影响其他档案；同时记录质量超标警告）。
    /// </summary>
    public IReadOnlyList<(string File, string Error)> LoadDirectory(string folder)
    {
        _folder = folder;
        var errors = new List<(string, string)>();
        if (!Directory.Exists(folder))
            return errors;

        // 按文件名排序遍历 + 档案 Id 去重：手工重命名产生同 Id 双档案时，
        // 确定性地"文件名排序在先者生效、后者报错"（此前是枚举序后者覆盖，结果不可预测）
        var seenIntrinsic = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(folder, "*.intrinsic.json")
                     .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var profile = JsonSerializer.Deserialize<IntrinsicProfile>(File.ReadAllText(file))
                    ?? throw new InvalidDataException("档案内容为空");
                if (seenIntrinsic.TryGetValue(profile.CameraId, out var firstFile))
                {
                    errors.Add((Path.GetFileName(file),
                        $"档案 Id 重复: {profile.CameraId} 已由 {Path.GetFileName(firstFile)} 加载（按文件名排序先者生效），请删除多余档案"));
                    continue;
                }
                LoadIntrinsic(profile);
                seenIntrinsic[profile.CameraId] = file;
                WarnIfFileNameMismatch(file, "intrinsic", profile.CameraId);
            }
            catch (Exception ex)
            {
                errors.Add((Path.GetFileName(file), ex.Message));
            }
        }

        var seenExtrinsic = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(folder, "*.extrinsic.json")
                     .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var profile = JsonSerializer.Deserialize<ExtrinsicProfile>(File.ReadAllText(file))
                    ?? throw new InvalidDataException("档案内容为空");
                if (seenExtrinsic.TryGetValue(profile.StationId, out var firstFile))
                {
                    errors.Add((Path.GetFileName(file),
                        $"档案 Id 重复: {profile.StationId} 已由 {Path.GetFileName(firstFile)} 加载（按文件名排序先者生效），请删除多余档案"));
                    continue;
                }
                LoadExtrinsic(profile);
                seenExtrinsic[profile.StationId] = file;
                WarnIfFileNameMismatch(file, "extrinsic", profile.StationId);
            }
            catch (Exception ex)
            {
                errors.Add((Path.GetFileName(file), ex.Message));
            }
        }

        var seenRotation = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(folder, "*.rotation.json")
                     .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var profile = JsonSerializer.Deserialize<RotationCenterProfile>(File.ReadAllText(file))
                    ?? throw new InvalidDataException("档案内容为空");
                if (seenRotation.TryGetValue(profile.StationId, out var firstFile))
                {
                    errors.Add((Path.GetFileName(file),
                        $"档案 Id 重复: {profile.StationId} 已由 {Path.GetFileName(firstFile)} 加载（按文件名排序先者生效），请删除多余档案"));
                    continue;
                }
                LoadRotationCenter(profile);
                seenRotation[profile.StationId] = file;
                WarnIfFileNameMismatch(file, "rotation", profile.StationId);
            }
            catch (Exception ex)
            {
                errors.Add((Path.GetFileName(file), ex.Message));
            }
        }

        var seenPolynomial = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(folder, "*.polynomial.json")
                     .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var profile = JsonSerializer.Deserialize<PolynomialProfile>(File.ReadAllText(file))
                    ?? throw new InvalidDataException("档案内容为空");
                if (seenPolynomial.TryGetValue(profile.StationId, out var firstFile))
                {
                    errors.Add((Path.GetFileName(file),
                        $"档案 Id 重复: {profile.StationId} 已由 {Path.GetFileName(firstFile)} 加载（按文件名排序先者生效），请删除多余档案"));
                    continue;
                }
                LoadPolynomial(profile);
                seenPolynomial[profile.StationId] = file;
                WarnIfFileNameMismatch(file, "polynomial", profile.StationId);
            }
            catch (Exception ex)
            {
                errors.Add((Path.GetFileName(file), ex.Message));
            }
        }

        var seenScale = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(folder, "*.scale.json")
                     .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var profile = JsonSerializer.Deserialize<ScaleProfile>(File.ReadAllText(file))
                    ?? throw new InvalidDataException("档案内容为空");
                if (seenScale.TryGetValue(profile.StationId, out var firstFile))
                {
                    errors.Add((Path.GetFileName(file),
                        $"档案 Id 重复: {profile.StationId} 已由 {Path.GetFileName(firstFile)} 加载（按文件名排序先者生效），请删除多余档案"));
                    continue;
                }
                LoadScale(profile);
                seenScale[profile.StationId] = file;
                WarnIfFileNameMismatch(file, "scale", profile.StationId);
            }
            catch (Exception ex)
            {
                errors.Add((Path.GetFileName(file), ex.Message));
            }
        }

        return errors;
    }

    /// <summary>文件名与档案内部 Id 一致性警告：手工重命名文件后，Save 按 Id 写出新文件，
    /// 目录出现两份档案（按 Id 加载、后者覆盖前者）——警告但不阻断（档案本身有效）。</summary>
    private void WarnIfFileNameMismatch(string file, string kind, string id)
    {
        var name = Path.GetFileName(file);
        var suffix = $".{kind}.json";
        if (!name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return;
        var nameId = name[..^suffix.Length];
        if (!string.Equals(nameId, id, StringComparison.OrdinalIgnoreCase))
            AddQualityWarning($"档案文件名 {name} 与内部 Id \"{id}\" 不一致：保存时将按 Id 写出新文件，请重命名或删除旧文件");
    }

    public void LoadIntrinsic(IntrinsicProfile profile)
    {
        ValidateIntrinsic(profile);

        using var cameraMatrix = ToMat(profile.CameraMatrix, 3, 3);
        using var distCoeffs = ToMat(profile.DistCoeffs, 1, profile.DistCoeffs.Length);
        using var noRotation = new Mat();

        var mapX = new Mat();
        var mapY = new Mat();
        try
        {
            Cv2.InitUndistortRectifyMap(
                cameraMatrix, distCoeffs, noRotation, cameraMatrix,
                new Size(profile.Width, profile.Height), MatType.CV_32FC1, mapX, mapY);
        }
        catch
        {
            // 建图失败（OpenCV 异常）时释放已分配的 Map，避免非托管内存泄漏
            mapX.Dispose();
            mapY.Dispose();
            throw;
        }

        // 写锁：与 Undistort 的读锁互斥，确保旧 Map 释放时没有 Remap 正在使用
        _intrinsicLock.EnterWriteLock();
        try
        {
            if (_intrinsics.TryRemove(profile.CameraId, out var old))
            {
                old.MapX.Dispose();
                old.MapY.Dispose();
            }
            _intrinsics[profile.CameraId] = new IntrinsicState(profile, mapX, mapY);
        }
        finally
        {
            _intrinsicLock.ExitWriteLock();
        }

        if (AssessIntrinsic(profile) == CalibrationQuality.Poor)
            AddQualityWarning($"内参 {profile.CameraId} 质量超标: RMS {profile.Rms:0.000}px（>{IntrinsicRmsFair:0.0} 可用上限），建议重新标定");
    }

    /// <summary>质量警告入队，超过保留上限时丢弃最旧条目（固定容量，避免无限累积）。</summary>
    private void AddQualityWarning(string message)
    {
        _qualityWarnings.Enqueue(message);
        while (_qualityWarnings.Count > MaxQualityWarnings)
            _qualityWarnings.TryDequeue(out _);
    }

    /// <summary>内参字段校验（CameraMatrix 9 元素 / 数值合理 / 分辨率 / 畸变系数长度）。
    /// 非法档案拒绝加载，避免越界崩溃或静默生成垃圾映射（fx=0/Infinity 会让 Remap 输出错图、机器人坐标错）。
    /// DistCoeffs 允许为空（空数组视为零畸变，与标准输出一致）。</summary>
    public static void ValidateIntrinsic(IntrinsicProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.CameraId))
            throw new VisionException(VisionErrorCode.InternalError, "内参 CameraId 为空（空串 Id 会导致档案互相覆盖）");
        if (profile.CameraMatrix is not { Length: 9 })
            throw new VisionException(VisionErrorCode.InternalError,
                $"内参 CameraMatrix 必须为 9 元素，当前 {profile.CameraMatrix?.Length ?? 0}");
        if (profile.CameraMatrix.Any(v => !double.IsFinite(v)) || profile.DistCoeffs.Any(v => !double.IsFinite(v)))
            throw new VisionException(VisionErrorCode.InternalError,
                $"内参 {profile.CameraId} 的 CameraMatrix/DistCoeffs 含非有限值（NaN/Infinity），档案已损坏");
        if (profile.CameraMatrix[0] <= 0 || profile.CameraMatrix[4] <= 0)
            throw new VisionException(VisionErrorCode.InternalError,
                $"内参 {profile.CameraId} 的焦距非法: fx={profile.CameraMatrix[0]}, fy={profile.CameraMatrix[4]}（必须为正）");
        // 主点边界（留 10% 余量）：cx/cy 跑到图像外的档案必为垃圾数据，静默建出的映射表完全错误
        var marginX = profile.Width * 0.1;
        var marginY = profile.Height * 0.1;
        if (profile.CameraMatrix[2] < -marginX || profile.CameraMatrix[2] > profile.Width + marginX ||
            profile.CameraMatrix[5] < -marginY || profile.CameraMatrix[5] > profile.Height + marginY)
            throw new VisionException(VisionErrorCode.InternalError,
                $"内参 {profile.CameraId} 的主点越界: cx={profile.CameraMatrix[2]}, cy={profile.CameraMatrix[5]} " +
                $"（分辨率 {profile.Width}x{profile.Height}，允许范围含 10% 余量）");
        if (profile.Width <= 0 || profile.Height <= 0)
            throw new VisionException(VisionErrorCode.InternalError, $"内参分辨率非法: {profile.Width}x{profile.Height}");
        if (profile.DistCoeffs.Length > 14)
            throw new VisionException(VisionErrorCode.InternalError, $"内参畸变系数长度非法: {profile.DistCoeffs.Length}");
    }

    /// <summary>
    /// 外参档案值域校验：Affine 必须为 6 元素且全部有限、Rms/MaxResidual 非负且有限。
    /// 损坏档案在加载时即拒绝（1099 InternalError），避免仿射映射产生 NaN 坐标被误当真实位姿（安全问题）。
    /// </summary>
    public static void ValidateExtrinsic(ExtrinsicProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.StationId))
            throw new VisionException(VisionErrorCode.InternalError, "外参 StationId 为空（空串 Id 会导致档案互相覆盖）");
        if (string.IsNullOrWhiteSpace(profile.CameraId))
            throw new VisionException(VisionErrorCode.InternalError, $"外参 {profile.StationId} 的 CameraId 为空");
        if (profile.Affine is not { Length: 6 } || profile.Affine.Any(v => !double.IsFinite(v)))
            throw new VisionException(VisionErrorCode.InternalError,
                $"外参 {profile.StationId} 的 Affine 必须为 6 个有限数值，当前 {profile.Affine?.Length ?? 0}");
        if (!double.IsFinite(profile.Rms) || profile.Rms < 0)
            throw new VisionException(VisionErrorCode.InternalError, $"外参 {profile.StationId} 的 Rms 非法: {profile.Rms}");
        if (!double.IsFinite(profile.MaxResidual) || profile.MaxResidual < 0)
            throw new VisionException(VisionErrorCode.InternalError, $"外参 {profile.StationId} 的 MaxResidual 非法: {profile.MaxResidual}");
        if (!CameraMountType.IsValid(profile.MountType))
            throw new VisionException(VisionErrorCode.InternalError,
                $"外参 {profile.StationId} 的 MountType 非法: {profile.MountType}（仅支持 Fixed/OnArm）");
        if (!PoseComposeMode.IsValid(profile.ComposeMode))
            throw new VisionException(VisionErrorCode.InternalError,
                $"外参 {profile.StationId} 的 ComposeMode 非法: {profile.ComposeMode}（仅 Check/Translate）");
        if (!double.IsFinite(profile.TeachTcpX) || !double.IsFinite(profile.TeachTcpY) ||
            !double.IsFinite(profile.TeachRzDeg) || !double.IsFinite(profile.CalibrationPlaneZ))
            throw new VisionException(VisionErrorCode.InternalError, $"外参 {profile.StationId} 的拍照位姿/标定平面字段含非有限值");
    }

    /// <summary>旋转中心档案值域校验：Cx/Cy/RadiusPx/Rms 必须有限，Rms 非负。损坏档案拒绝加载（1099 InternalError）。</summary>
    public static void ValidateRotationCenter(RotationCenterProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.StationId))
            throw new VisionException(VisionErrorCode.InternalError, "旋转中心 StationId 为空（空串 Id 会导致档案互相覆盖）");
        if (string.IsNullOrWhiteSpace(profile.CameraId))
            throw new VisionException(VisionErrorCode.InternalError, $"旋转中心 {profile.StationId} 的 CameraId 为空");
        if (!double.IsFinite(profile.Cx) || !double.IsFinite(profile.Cy) ||
            !double.IsFinite(profile.RadiusPx) || !double.IsFinite(profile.AxisRatio))
            throw new VisionException(VisionErrorCode.InternalError, $"旋转中心 {profile.StationId} 的数值非法（非有限值）");
        if (!double.IsFinite(profile.Rms) || profile.Rms < 0)
            throw new VisionException(VisionErrorCode.InternalError, $"旋转中心 {profile.StationId} 的 Rms 非法: {profile.Rms}");
        if (profile.AxisRatio < 0)
            throw new VisionException(VisionErrorCode.InternalError, $"旋转中心 {profile.StationId} 的 AxisRatio 非法: {profile.AxisRatio}");
        if (!double.IsFinite(profile.ToolOffsetDeg))
            throw new VisionException(VisionErrorCode.InternalError, $"旋转中心 {profile.StationId} 的 ToolOffsetDeg 非法: {profile.ToolOffsetDeg}");
    }

    /// <summary>留一交叉验证最大误差告警阈值（机器人单位）：超过说明可能存在抄错/误点
    /// （单个坏点对整体拟合被均摊，残差好看但留一误差急剧放大）。</summary>
    public const double LeaveOneOutWarnLimit = 1.0;

    public void LoadExtrinsic(ExtrinsicProfile profile)
    {
        ValidateExtrinsic(profile);
        _extrinsics[profile.StationId] = profile;
        if (AssessExtrinsic(profile) == CalibrationQuality.Poor)
            AddQualityWarning($"外参 {profile.StationId} 质量超标: 最大残差 {profile.MaxResidual:0.000}（>{ExtrinsicResidualFair:0.0} 可用上限），建议重新标定");
        if (profile.LeaveOneOutMax > LeaveOneOutWarnLimit)
            AddQualityWarning($"外参 {profile.StationId} 留一最大误差 {profile.LeaveOneOutMax:0.000} 偏大（>{LeaveOneOutWarnLimit:0.0}），疑似存在抄错/误点，请核对点对");
        WarnIfDualMapping(profile.StationId);
    }

    public void LoadRotationCenter(RotationCenterProfile profile)
    {
        ValidateRotationCenter(profile);
        _rotationCenters[profile.StationId] = profile;
        if (AssessRotation(profile) == CalibrationQuality.Poor)
            AddQualityWarning($"旋转中心 {profile.StationId} 质量超标: RMS {profile.Rms:0.000}px"
                + (profile.PointCount >= 5 && profile.AxisRatio > RotationAxisRatioLimit
                    ? $"，长短轴比 {profile.AxisRatio:0.00}"
                    : "")
                + "，建议重新标定");
    }

    /// <summary>多项式档案校验：Id/相机、阶数 2~3、系数个数与阶数匹配且全有限、分辨率、残差非负。
    /// 系数损坏（个数不符/NaN）会让 Evaluate 输出垃圾坐标，加载时拒绝。</summary>
    public static void ValidatePolynomial(PolynomialProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.StationId))
            throw new VisionException(VisionErrorCode.InternalError, "多项式档案 StationId 为空（空串 Id 会导致档案互相覆盖）");
        if (string.IsNullOrWhiteSpace(profile.CameraId))
            throw new VisionException(VisionErrorCode.InternalError, $"多项式档案 {profile.StationId} 的 CameraId 为空");
        if (profile.Order is not (2 or 3))
            throw new VisionException(VisionErrorCode.InternalError, $"多项式档案 {profile.StationId} 的阶数非法: {profile.Order}（仅 2/3）");
        var expected = (profile.Order + 1) * (profile.Order + 2) / 2;
        if (profile.CoefX.Length != expected || profile.CoefY.Length != expected)
            throw new VisionException(VisionErrorCode.InternalError,
                $"多项式档案 {profile.StationId} 的系数个数非法: X {profile.CoefX.Length} / Y {profile.CoefY.Length}（{profile.Order} 阶应为 {expected}）");
        if (profile.CoefX.Concat(profile.CoefY).Any(v => !double.IsFinite(v)))
            throw new VisionException(VisionErrorCode.InternalError, $"多项式档案 {profile.StationId} 的系数含非有限值，档案已损坏");
        if (profile.Width <= 0 || profile.Height <= 0)
            throw new VisionException(VisionErrorCode.InternalError, $"多项式档案 {profile.StationId} 的分辨率非法: {profile.Width}x{profile.Height}");
        if (!double.IsFinite(profile.Rms) || profile.Rms < 0 || !double.IsFinite(profile.MaxResidual) || profile.MaxResidual < 0)
            throw new VisionException(VisionErrorCode.InternalError, $"多项式档案 {profile.StationId} 的残差指标非法");
        if (!CameraMountType.IsValid(profile.MountType))
            throw new VisionException(VisionErrorCode.InternalError,
                $"多项式档案 {profile.StationId} 的 MountType 非法: {profile.MountType}（仅 Fixed/OnArm）");
        if (!PolynomialCoordinateSpace.IsValid(profile.CoordinateSpace))
            throw new VisionException(VisionErrorCode.InternalError,
                $"多项式档案 {profile.StationId} 的 CoordinateSpace 非法: {profile.CoordinateSpace}（仅 Robot/Image）");
        if (!PoseComposeMode.IsValid(profile.ComposeMode))
            throw new VisionException(VisionErrorCode.InternalError,
                $"多项式档案 {profile.StationId} 的 ComposeMode 非法: {profile.ComposeMode}（仅 Check/Translate）");
        if (!double.IsFinite(profile.TeachTcpX) || !double.IsFinite(profile.TeachTcpY) ||
            !double.IsFinite(profile.TeachRzDeg) || !double.IsFinite(profile.CalibrationPlaneZ))
            throw new VisionException(VisionErrorCode.InternalError, $"多项式档案 {profile.StationId} 的位姿/平面字段含非有限值");
    }

    public void LoadPolynomial(PolynomialProfile profile)
    {
        ValidatePolynomial(profile);
        _polynomials[profile.StationId] = profile;
        if (profile.MaxResidual > ExtrinsicResidualFair)
            AddQualityWarning($"多项式标定 {profile.StationId} 质量超标: 最大残差 {profile.MaxResidual:0.000}（>{ExtrinsicResidualFair:0.0} 可用上限），建议重新标定");
        WarnIfDualMapping(profile.StationId);
    }

    /// <summary>同一工位映射档案并存警告：多项式+外参 → 管线只走多项式；
    /// (多项式|外参)+比例 → 比例不参与管线（仅测量显示）。必须警告，否则操作员以为被忽略的那份仍生效。</summary>
    private void WarnIfDualMapping(string stationId)
    {
        if (_polynomials.ContainsKey(stationId) && _extrinsics.ContainsKey(stationId))
            AddQualityWarning($"工位 {stationId} 同时存在多项式与外参档案：生产优先使用多项式（原图+多项式映射），外参/去畸变被忽略。请删除不用的那份以免坐标系混淆");
        if (_scales.ContainsKey(stationId) &&
            (_polynomials.ContainsKey(stationId) || _extrinsics.ContainsKey(stationId)))
            AddQualityWarning($"工位 {stationId} 同时存在比例与外参/多项式档案：管线优先使用外参/多项式（机器人系坐标），比例档案仅用于测量显示");
    }

    public void SavePolynomial(PolynomialProfile profile)
    {
        RequireFolder();
        ValidatePolynomial(profile);
        WriteJson(ProfileFile("polynomial", profile.StationId), profile);
        LoadPolynomial(profile);
    }

    public bool DeletePolynomial(string stationId)
    {
        _polynomials.TryRemove(stationId, out _);
        return DeleteProfileFile(stationId, "polynomial");
    }

    /// <summary>比例 X/Y 各向异性告警阈值：|kx/ky − 1| 超过 2% 说明存在旋转/透视/畸变，
    /// 线性比例只能近似，建议改用多项式标定。</summary>
    public const double ScaleAnisotropyWarnLimit = 0.02;

    /// <summary>比例档案校验：Id/相机非空，比例 > 0 且有限（0 或负数无物理意义；非有限值 = 档案损坏），
    /// 分辨率 ≥ 0（0 = 未记录，跳过一致性校验）。手动录入无法验证数值真伪，只能挡住明显笔误。</summary>
    public static void ValidateScale(ScaleProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.StationId))
            throw new VisionException(VisionErrorCode.InternalError, "比例档案 StationId 为空（空串 Id 会导致档案互相覆盖）");
        if (string.IsNullOrWhiteSpace(profile.CameraId))
            throw new VisionException(VisionErrorCode.InternalError, $"比例档案 {profile.StationId} 的 CameraId 为空");
        if (!double.IsFinite(profile.ScaleX) || profile.ScaleX <= 0 ||
            !double.IsFinite(profile.ScaleY) || profile.ScaleY <= 0)
            throw new VisionException(VisionErrorCode.InternalError,
                $"比例档案 {profile.StationId} 的比例非法: {profile.ScaleX} / {profile.ScaleY} mm/px（须为正数）");
        if (profile.Width < 0 || profile.Height < 0)
            throw new VisionException(VisionErrorCode.InternalError,
                $"比例档案 {profile.StationId} 的分辨率非法: {profile.Width}x{profile.Height}");
    }

    public void LoadScale(ScaleProfile profile)
    {
        ValidateScale(profile);
        _scales[profile.StationId] = profile;
        var ratio = Math.Max(profile.ScaleX, profile.ScaleY) / Math.Min(profile.ScaleX, profile.ScaleY) - 1;
        if (ratio > ScaleAnisotropyWarnLimit)
            AddQualityWarning($"比例标定 {profile.StationId} X/Y 各向异性 {ratio * 100:0.0}%（>{ScaleAnisotropyWarnLimit * 100:0}%）："
                + "疑似存在旋转/透视/畸变，线性比例仅为近似值，建议改用多项式标定");
        WarnIfDualMapping(profile.StationId);
    }

    public void SaveScale(ScaleProfile profile)
    {
        RequireFolder();
        ValidateScale(profile);
        WriteJson(ProfileFile("scale", profile.StationId), profile);
        LoadScale(profile);
    }

    public bool DeleteScale(string stationId)
    {
        _scales.TryRemove(stationId, out _);
        return DeleteProfileFile(stationId, "scale");
    }

    /// <summary>多项式工位的推理图像分辨率校验：换分辨率后归一化坐标错位，映射整体失效。</summary>
    public void VerifyPolynomialResolution(string stationId, int width, int height)
    {
        if (!_polynomials.TryGetValue(stationId, out var profile))
            throw new VisionException(VisionErrorCode.NotCalibrated, $"工位未做多项式标定: {stationId}");
        if (profile.Width != width || profile.Height != height)
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"图像分辨率 {width}x{height} 与多项式标定档案 {profile.Width}x{profile.Height} 不一致，请重新标定");
    }

    /// <summary>多项式工位的 TRIGGER 位姿前置校验（取图前拦截）：
    /// Check 模式比对 XY/RZ 全部容差；Translate 模式仅比对 RZ（平移是合成参数不是拒绝条件）。
    /// Image 坐标空间（免示教毫米系）无机器人系概念，直接跳过。
    /// Fixed / 未记录位姿 / 未上报位姿 / 关闭校验时放行。</summary>
    public void VerifyPolynomialClientPose(string? stationId, TcpClientPose? pose)
    {
        if (!PoseCheckEnabled || pose is null || string.IsNullOrEmpty(stationId))
            return;
        if (!_polynomials.TryGetValue(stationId, out var profile))
            return;
        if (string.Equals(profile.CoordinateSpace, PolynomialCoordinateSpace.Image, StringComparison.OrdinalIgnoreCase))
            return; // Image 毫米系：不锚定机器人系，位姿校验/合成均不适用
        if (!string.Equals(profile.MountType, CameraMountType.OnArm, StringComparison.OrdinalIgnoreCase) || !profile.HasTeachPose)
            return;

        var rzDeviation = Math.Abs(NormalizeDelta(pose.RzDeg - profile.TeachRzDeg));
        if (rzDeviation > PoseRzToleranceDeg)
            throw new VisionException(VisionErrorCode.PoseMismatch,
                $"拍照姿态不一致: RZ 偏差 {rzDeviation:0.000}° 超容差 {PoseRzToleranceDeg:0.0}°" +
                "（多项式映射依赖固定相机姿态，Translate 模式只允许平移）");

        if (string.Equals(profile.ComposeMode, PoseComposeMode.Check, StringComparison.OrdinalIgnoreCase))
        {
            var dx = pose.X - profile.TeachTcpX;
            var dy = pose.Y - profile.TeachTcpY;
            var xyDeviation = Math.Sqrt(dx * dx + dy * dy);
            if (xyDeviation > PoseXyToleranceMm)
                throw new VisionException(VisionErrorCode.PoseMismatch,
                    $"拍照位姿不一致: XY 偏差 {xyDeviation:0.000}mm 超容差 {PoseXyToleranceMm:0.0}mm" +
                    "（Check 模式要求拍照点与标定一致；相机只有平移可改用 ComposeMode=Translate）");
        }
    }

    /// <summary>
    /// 多项式工位的像素位姿 → 机器人位姿（单图模式管线）：
    /// 位置 = 多项式求值；角度 = 局部雅可比映射（前推 ε 像素的差向量方向，自然包含
    /// 旋转/镜像/非等比缩放，与外参两点映射 Atan2 同语义）。
    /// OnArm + Translate 模式：位置 += (当前TCP − 示教TCP)（相机纯平移下映射精确平移，
    /// 换拍照点不重标）；RZ 一致性由 VerifyPolynomialClientPose 前置拦截。
    /// </summary>
    public RobotPose PixelToRobotPolynomial(string stationId, PixelPose pose, string? cameraId = null, TcpClientPose? clientPose = null)
    {
        if (!_polynomials.TryGetValue(stationId, out var profile))
            throw new VisionException(VisionErrorCode.NotCalibrated, $"工位未做多项式标定: {stationId}");

        if (!string.IsNullOrEmpty(cameraId) &&
            !string.Equals(profile.CameraId, cameraId, StringComparison.OrdinalIgnoreCase))
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"多项式标定相机 {profile.CameraId} 与取图相机 {cameraId} 不一致，请重新标定");

        var (x, y) = profile.Evaluate(pose.Cx, pose.Cy);

        // 角度：前推 ε 像素的两点差方向（局部线性化）
        const double Epsilon = 2.0;
        var rad = pose.AngleDeg * Math.PI / 180.0;
        var (fx, fy) = profile.Evaluate(pose.Cx + Epsilon * Math.Cos(rad), pose.Cy + Epsilon * Math.Sin(rad));
        var angleDeg = Math.Atan2(fy - y, fx - x) * 180.0 / Math.PI;

        // 平移合成（OnArm + Translate + 已上报位姿 + Robot 坐标空间）：映射整体平移。
        // Image 毫米系无机器人系概念，不参与合成。
        var translate = clientPose is not null &&
                        string.Equals(profile.CoordinateSpace, PolynomialCoordinateSpace.Robot, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(profile.MountType, CameraMountType.OnArm, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(profile.ComposeMode, PoseComposeMode.Translate, StringComparison.OrdinalIgnoreCase) &&
                        profile.HasTeachPose;
        if (translate)
        {
            x += clientPose!.X - profile.TeachTcpX;
            y += clientPose.Y - profile.TeachTcpY;
        }

        return new RobotPose(x, y, AngleGeometry.NormalizeSignedDeg(angleDeg));
    }

    /// <summary>
    /// 比例工位的像素位姿 → 图像平面毫米位姿（单图模式管线，无标定板工位的回退路径）：
    /// 位置 = 像素 × 比例，坐标系为图像系（原点=左上角像素 0,0，X 向右 Y 向下，与 UI 预览同向）；
    /// 角度 = 像素方向向量经各向异性缩放后重算（kx=ky 时即像素角不变）。
    /// 无机器人系锚定：不参与 OnArm 位姿校验/平移合成，输出由上位机按图像系毫米解读。
    /// </summary>
    public RobotPose PixelToRobotScale(string stationId, PixelPose pose, string? cameraId = null)
    {
        if (!_scales.TryGetValue(stationId, out var profile))
            throw new VisionException(VisionErrorCode.NotCalibrated, $"工位未录入比例标定: {stationId}");

        if (!string.IsNullOrEmpty(cameraId) &&
            !string.Equals(profile.CameraId, cameraId, StringComparison.OrdinalIgnoreCase))
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"比例标定相机 {profile.CameraId} 与取图相机 {cameraId} 不一致，请重新录入");

        var x = pose.Cx * profile.ScaleX;
        var y = pose.Cy * profile.ScaleY;

        var rad = pose.AngleDeg * Math.PI / 180.0;
        var angleDeg = Math.Atan2(profile.ScaleY * Math.Sin(rad), profile.ScaleX * Math.Cos(rad)) * 180.0 / Math.PI;

        return new RobotPose(x, y, AngleGeometry.NormalizeSignedDeg(angleDeg));
    }

    /// <summary>比例工位的推理图像分辨率校验：比例以像素为基准，换分辨率后 mm/px 整体失效。
    /// 档案未记录分辨率（Width=0，旧档/手填遗漏）时跳过（无从比对）。</summary>
    public void VerifyScaleResolution(string stationId, int width, int height)
    {
        if (!_scales.TryGetValue(stationId, out var profile))
            throw new VisionException(VisionErrorCode.NotCalibrated, $"工位未录入比例标定: {stationId}");
        if (profile.Width > 0 && (profile.Width != width || profile.Height != height))
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"图像分辨率 {width}x{height} 与比例标定档案 {profile.Width}x{profile.Height} 不一致，请重新录入比例");
    }

    // ---- 保存：写回目录 + 立即热加载（无需重启）----

    public void SaveIntrinsic(IntrinsicProfile profile)
    {
        RequireFolder();
        ValidateIntrinsic(profile);
        WriteJson(ProfileFile("intrinsic", profile.CameraId), profile);
        LoadIntrinsic(profile);
    }

    public void SaveExtrinsic(ExtrinsicProfile profile)
    {
        RequireFolder();
        WriteJson(ProfileFile("extrinsic", profile.StationId), profile);
        LoadExtrinsic(profile);
    }

    public void SaveRotationCenter(RotationCenterProfile profile)
    {
        RequireFolder();
        WriteJson(ProfileFile("rotation", profile.StationId), profile);
        LoadRotationCenter(profile);
    }

    // ---- 删除（文件 + 缓存）----

    public bool DeleteIntrinsic(string cameraId)
    {
        _intrinsicLock.EnterWriteLock();
        try
        {
            if (_intrinsics.TryRemove(cameraId, out var old))
            {
                old.MapX.Dispose();
                old.MapY.Dispose();
            }
        }
        finally
        {
            _intrinsicLock.ExitWriteLock();
        }
        return DeleteProfileFile(cameraId, "intrinsic");
    }

    public bool DeleteExtrinsic(string stationId)
    {
        _extrinsics.TryRemove(stationId, out _);
        return DeleteProfileFile(stationId, "extrinsic");
    }

    public bool DeleteRotationCenter(string stationId)
    {
        _rotationCenters.TryRemove(stationId, out _);
        return DeleteProfileFile(stationId, "rotation");
    }

    // ---- 质量评估（与 README 验收参考对齐，供 UI/工具展示）----

    public static CalibrationQuality AssessIntrinsic(IntrinsicProfile p) =>
        p.Rms <= IntrinsicRmsGood ? CalibrationQuality.Good
        : p.Rms <= IntrinsicRmsFair ? CalibrationQuality.Fair
        : CalibrationQuality.Poor;

    public static CalibrationQuality AssessExtrinsic(ExtrinsicProfile p) =>
        p.MaxResidual <= ExtrinsicResidualGood ? CalibrationQuality.Good
        : p.MaxResidual <= ExtrinsicResidualFair ? CalibrationQuality.Fair
        : CalibrationQuality.Poor;

    public static CalibrationQuality AssessPolynomial(PolynomialProfile p) =>
        p.MaxResidual <= ExtrinsicResidualGood ? CalibrationQuality.Good
        : p.MaxResidual <= ExtrinsicResidualFair ? CalibrationQuality.Fair
        : CalibrationQuality.Poor;

    public static CalibrationQuality AssessRotation(RotationCenterProfile p)
    {
        if (p.Rms > RotationRmsFair)
            return CalibrationQuality.Poor;
        if (p.PointCount >= 5 && p.AxisRatio > RotationAxisRatioLimit)
            return CalibrationQuality.Poor;
        return p.Rms <= RotationRmsGood ? CalibrationQuality.Good : CalibrationQuality.Fair;
    }

    /// <summary>标定前置检查：外参/旋转中心标定前该相机必须先完成内参标定（一致性铁律）。</summary>
    public void RequireIntrinsic(string cameraId)
    {
        if (!_intrinsics.ContainsKey(cameraId))
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"相机未做内参标定: {cameraId}（外参/旋转中心标定前必须先完成内参标定）");
    }

    /// <summary>
    /// 档案分辨率与内参档案一致性校验：外参/旋转中心记录的分辨率与当前内参不一致时拒绝使用——
    /// 换相机/改分辨率后内参重标，旧外参的像素坐标系已失效，继续用会静默输出错位坐标。
    /// width/height ≤ 0 视为旧版档案未记录分辨率，跳过校验（向后兼容）。
    /// </summary>
    private void VerifyResolutionConsistency(string cameraId, int width, int height, string profileKind)
    {
        if (width <= 0 || height <= 0)
            return;
        if (_intrinsics.TryGetValue(cameraId, out var state) &&
            (state.Profile.Width != width || state.Profile.Height != height))
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"{profileKind}标定分辨率 {width}x{height} 与相机 {cameraId} 当前内参 {state.Profile.Width}x{state.Profile.Height} 不一致" +
                "（换相机/改分辨率后需重新标定外参/旋转中心）");
    }

    /// <summary>
    /// 工位像素→机器人映射的统一获取（多项式工位优先，其次外参仿射）。
    /// 供旋转轴心映射 / 方向自检 / 偏角实测共用——两类档案的像素坐标系必须与
    /// 旋转中心标定时一致（外参链路=去畸变图；多项式链路=原图）。无映射返回 null。
    /// </summary>
    private (Func<double, double, double> MapX, Func<double, double, double> MapY, string CameraId)? GetMapping(string stationId)
    {
        if (_polynomials.TryGetValue(stationId, out var poly))
            return ((x, y) => poly.Evaluate(x, y).X, (x, y) => poly.Evaluate(x, y).Y, poly.CameraId);

        if (_extrinsics.TryGetValue(stationId, out var ext))
        {
            var a = ext.Affine;
            return ((x, y) => a[0] * x + a[1] * y + a[2], (x, y) => a[3] * x + a[4] * y + a[5], ext.CameraId);
        }

        return null;
    }

    /// <summary>
    /// 旋转轴心的机器人坐标（像素轴心经工位映射：多项式工位直接求值，外参工位仿射映射）。
    /// 仿射把圆映成椭圆时圆心仍映到中心，故像素空间拟合中心再映射是安全的。
    /// 映射来源与旋转中心必须来自同一相机（同一像素坐标系）。
    /// </summary>
    public Point2d RotationCenterRobot(string stationId)
    {
        if (!_rotationCenters.TryGetValue(stationId, out var rc))
            throw new VisionException(VisionErrorCode.NotCalibrated, $"工位未做旋转中心标定: {stationId}");

        var mapping = GetMapping(stationId)
            ?? throw new VisionException(VisionErrorCode.NotCalibrated, $"工位未做外参/多项式标定: {stationId}");

        if (!string.Equals(mapping.CameraId, rc.CameraId, StringComparison.OrdinalIgnoreCase))
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"映射相机 {mapping.CameraId} 与旋转中心相机 {rc.CameraId} 不一致，请重新标定");

        // 分辨率一致性：外参链路校验外参与旋转中心 vs 内参（去畸变坐标系）；
        // 多项式链路校验旋转中心 vs 多项式档案（原图坐标系，无内参概念）
        if (_extrinsics.TryGetValue(stationId, out var ext))
        {
            VerifyResolutionConsistency(ext.CameraId, ext.Width, ext.Height, "外参");
            VerifyResolutionConsistency(rc.CameraId, rc.Width, rc.Height, "旋转中心");
        }
        else if (_polynomials.TryGetValue(stationId, out var poly) &&
                 (rc.Width != poly.Width || rc.Height != poly.Height))
        {
            if (rc.Width > 0 && rc.Height > 0)
                throw new VisionException(VisionErrorCode.NotCalibrated,
                    $"旋转中心标定分辨率 {rc.Width}x{rc.Height} 与多项式档案 {poly.Width}x{poly.Height} 不一致（须在同一原图坐标系下标定）");
        }

        return new Point2d(mapping.MapX(rc.Cx, rc.Cy), mapping.MapY(rc.Cx, rc.Cy));
    }

    /// <summary>
    /// 旋转中心补偿（偏心工具）：在机器人坐标系中把位置绕轴心反转零件角，角度不变。
    /// 机器人先移动到输出位置，再旋转第 4 轴到输出角度，工具尖端恰好落在零件检测位置。
    /// stationId 为空或 mode=None 时原样返回。
    /// </summary>
    public RobotPose CompensateRotation(string? stationId, RotationCompensationMode mode, RobotPose pose)
    {
        if (mode == RotationCompensationMode.None || string.IsNullOrEmpty(stationId))
            return pose;

        var center = RotationCenterRobot(stationId);
        var toolOffset = _rotationCenters[stationId].ToolOffsetDeg;
        return RotationCenterCompensation.Apply(pose, center.X, center.Y, toolOffset);
    }

    /// <summary>
    /// 旋转方向自检（标定后验证步）：把多角度标记点与轴心经外参映射到机器人系，
    /// 比对"标记点绕轴心方位角变化方向"与"记录的第 4 轴角度变化方向"是否一致。
    /// 不一致说明第 4 轴正方向与图像旋转方向相反（各品牌 RZ 正方向不一，装反后误差按 2r·sin 放大，
    /// 而圆拟合 RMS 发现不了——圆本身是好的）。一致对比例 ≥ 60% 通过（容忍标记提取噪声）。
    /// anglesDeg 与 points 一一对应（每点记录当时的第 4 轴角度，录入顺序任意——内部按角度排序）；
    /// 外参缺失/不一致时抛异常。
    /// </summary>
    public void VerifyRotationDirection(string stationId, RotationCenterProfile rc, Point2f[] points, double[] anglesDeg)
    {
        if (points.Length != anglesDeg.Length || points.Length < 3)
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"方向自检需要 ≥3 个带角度记录的标记点，当前 {Math.Min(points.Length, anglesDeg.Length)} 个");
        var verifyMapping = GetMapping(stationId)
            ?? throw new VisionException(VisionErrorCode.NotCalibrated, $"工位未做外参/多项式标定，无法做方向自检: {stationId}");
        if (!string.Equals(verifyMapping.CameraId, rc.CameraId, StringComparison.OrdinalIgnoreCase))
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"映射相机 {verifyMapping.CameraId} 与旋转中心相机 {rc.CameraId} 不一致，无法做方向自检");

        // 像素点与轴心同过工位映射到机器人系（映射保持旋转方向性：含反射时两次映射同时反向，比对仍有效）
        var cx = verifyMapping.MapX(rc.Cx, rc.Cy);
        var cy = verifyMapping.MapY(rc.Cx, rc.Cy);

        // 按第 4 轴角度排序后比对相邻对：与录入顺序无关（乱序点表同样可自检）
        var samples = points.Select((p, i) => (
                Angle: anglesDeg[i],
                Bearing: Math.Atan2(verifyMapping.MapY(p.X, p.Y) - cy, verifyMapping.MapX(p.X, p.Y) - cx) * 180.0 / Math.PI))
            .OrderBy(s => s.Angle)
            .ToArray();

        var pairs = 0;
        var consistent = 0;
        for (var i = 1; i < samples.Length; i++)
        {
            var dBearing = NormalizeDelta(samples[i].Bearing - samples[i - 1].Bearing);
            var dAngle = NormalizeDelta(samples[i].Angle - samples[i - 1].Angle);
            if (Math.Abs(dAngle) < 1e-9)
                continue; // 角度未变的相邻对无判别力
            pairs++;
            if (Math.Sign(dBearing) == Math.Sign(dAngle))
                consistent++;
        }

        if (pairs >= 2 && consistent * 10 < pairs * 6)
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"方向自检失败：{consistent}/{pairs} 个相邻角度对方向一致（需 ≥60%）。" +
                "第 4 轴正方向与图像旋转方向相反或角度记录有误——请在示教侧取反角度，或检查录入的第 4 轴角度");
    }

    /// <summary>归一化角度差到 (-180,180]（跨 ±180° 边界的相邻点不失真）。</summary>
    private static double NormalizeDelta(double delta)
    {
        var d = ((delta + 180.0) % 360.0 + 360.0) % 360.0 - 180.0;
        return d == -180.0 ? 180.0 : d;
    }

    /// <summary>拍照位姿校验开关（appsettings PoseCheck:Enabled，默认开）。关闭时带位姿的 TRIGGER 也不校验。
    /// volatile：UI/设置线程写、管线线程读，保证可见性。</summary>
    private volatile bool _poseCheckEnabled = true;

    public bool PoseCheckEnabled
    {
        get => _poseCheckEnabled;
        set => _poseCheckEnabled = value;
    }

    /// <summary>拍照点 TCP 平面偏差容差（mm，XY 欧氏距离）。Volatile 读写保证热更新跨线程可见
    /// （C# 不允许 volatile double，用 Volatile.Read/Write 等效实现）。</summary>
    private double _poseXyToleranceMm = 0.5;

    public double PoseXyToleranceMm
    {
        get => Volatile.Read(ref _poseXyToleranceMm);
        set => Volatile.Write(ref _poseXyToleranceMm, value);
    }

    /// <summary>拍照点第 4 轴角度容差（deg，归一化差）。Volatile 读写保证热更新跨线程可见。</summary>
    private double _poseRzToleranceDeg = 0.5;

    public double PoseRzToleranceDeg
    {
        get => Volatile.Read(ref _poseRzToleranceDeg);
        set => Volatile.Write(ref _poseRzToleranceDeg, value);
    }

    /// <summary>
    /// OnArm 且已记录示教位姿时，无位姿的 TRIGGER 必须拒绝（1014）。
    /// 多项式 Image 毫米系不锚定机器人系，不要求位姿。
    /// 多项式优先于外参（与生产映射一致）。
    /// </summary>
    public bool ClientPoseRequired(string? stationId)
    {
        if (!PoseCheckEnabled || string.IsNullOrEmpty(stationId))
            return false;
        if (_polynomials.TryGetValue(stationId, out var poly))
        {
            if (string.Equals(poly.CoordinateSpace, PolynomialCoordinateSpace.Image, StringComparison.OrdinalIgnoreCase))
                return false;
            return string.Equals(poly.MountType, CameraMountType.OnArm, StringComparison.OrdinalIgnoreCase)
                   && poly.HasTeachPose;
        }
        if (_extrinsics.TryGetValue(stationId, out var ext))
            return string.Equals(ext.MountType, CameraMountType.OnArm, StringComparison.OrdinalIgnoreCase)
                   && ext.HasTeachPose;
        return false;
    }

    /// <summary>OnArm 工位缺位姿时抛 1014。pose 非空时无操作。</summary>
    public void RequireClientPose(string? stationId, TcpClientPose? pose)
    {
        if (pose is not null || !ClientPoseRequired(stationId))
            return;
        throw new VisionException(VisionErrorCode.PoseRequired,
            "OnArm 工位必须使用 配方名或序列号,X,Y,RZ（未上报拍照位姿，拒绝执行以免输出错位坐标）");
    }

    /// <summary>
    /// PLC 上报拍照位姿与 OnArm 外参档案的标定位姿比对（TRIGGER,配方名,X,Y,RZ 触发）：
    /// Check 模式比对 XY+RZ；Translate 模式仅比对 RZ（平移由 PixelToRobot 合成）。
    /// 不一致抛 1012 PoseMismatch。
    /// 跳过校验（直接返回）：位姿未上报（由 RequireClientPose 另拦）/ Fixed 档案 / 档案未记录位姿
    /// （HasTeachPose=false，含旧档案）/ 工位无外参（后续 PixelToRobot 报 1004）/ PoseCheckEnabled=false。
    /// </summary>
    public void VerifyClientPose(string? stationId, TcpClientPose pose)
    {
        if (!PoseCheckEnabled || string.IsNullOrEmpty(stationId))
            return;
        if (!_extrinsics.TryGetValue(stationId, out var ext))
            return; // 无外参：外参缺失由 PixelToRobot 统一报 1004
        if (!string.Equals(ext.MountType, CameraMountType.OnArm, StringComparison.OrdinalIgnoreCase))
            return; // Fixed 档案与拍照位姿无关
        if (!ext.HasTeachPose)
            return; // 档案未记录位姿（旧档案/标定时未填）——无从比对，放行（档案侧已有提示）

        var rzDeviation = Math.Abs(NormalizeDelta(pose.RzDeg - ext.TeachRzDeg));
        if (rzDeviation > PoseRzToleranceDeg)
            throw new VisionException(VisionErrorCode.PoseMismatch,
                $"拍照姿态不一致: RZ 偏差 {rzDeviation:0.000}° 超容差 {PoseRzToleranceDeg:0.0}°" +
                "（OnArm 工位拍照姿态必须与标定一致；Translate 模式只允许平移）");

        if (string.Equals(ext.ComposeMode, PoseComposeMode.Translate, StringComparison.OrdinalIgnoreCase))
            return;

        var dx = pose.X - ext.TeachTcpX;
        var dy = pose.Y - ext.TeachTcpY;
        var xyDeviation = Math.Sqrt(dx * dx + dy * dy);
        if (xyDeviation > PoseXyToleranceMm)
            throw new VisionException(VisionErrorCode.PoseMismatch,
                $"拍照位姿不一致: 上报 ({pose.X:0.000},{pose.Y:0.000},{pose.RzDeg:0.000}°) " +
                $"与标定 ({ext.TeachTcpX:0.000},{ext.TeachTcpY:0.000},{ext.TeachRzDeg:0.000}°) " +
                $"偏差 XY {xyDeviation:0.000}mm / RZ {rzDeviation:0.000}° 超容差 " +
                $"({PoseXyToleranceMm:0.0}mm/{PoseRzToleranceDeg:0.0}°)。" +
                "OnArm 工位拍照位姿必须与标定一致，请核对拍照点或重标该工位外参；相机只有平移可改用 ComposeMode=Translate");
    }

    /// <summary>
    /// 从旋转中心标定点实测工具零位偏角 δ：标记点绕轴心的方位角 βᵢ = δ + φᵢ（φᵢ 为该点
    /// 第 4 轴角度），δᵢ = βᵢ − φᵢ 取圆均值。需要该工位外参（点与轴心同过仿射映射）。
    /// 返回 (δ, 离散度)：离散度 = δᵢ 两两圆差最大值（偏大说明标记提取噪声大或轴心有误差）。
    /// 180° 歧义无法自动判定——标记取在工具另一端时 δ 偏 180°，由调用方提示人工核对。
    /// </summary>
    public (double OffsetDeg, double SpreadDeg) ComputeToolOffsetDeg(
        string stationId, RotationCenterProfile rc, Point2f[] points, double[] anglesDeg)
    {
        if (points.Length != anglesDeg.Length || points.Length < 2)
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"实测偏角需要 ≥2 个带角度记录的标记点，当前 {Math.Min(points.Length, anglesDeg.Length)} 个");
        var offsetMapping = GetMapping(stationId)
            ?? throw new VisionException(VisionErrorCode.NotCalibrated, $"工位未做外参/多项式标定，无法实测偏角: {stationId}");
        if (!string.Equals(offsetMapping.CameraId, rc.CameraId, StringComparison.OrdinalIgnoreCase))
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"映射相机 {offsetMapping.CameraId} 与旋转中心相机 {rc.CameraId} 不一致，无法实测偏角");

        var cx = offsetMapping.MapX(rc.Cx, rc.Cy);
        var cy = offsetMapping.MapY(rc.Cx, rc.Cy);

        var deltas = new double[points.Length];
        for (var i = 0; i < points.Length; i++)
        {
            var bearing = Math.Atan2(offsetMapping.MapY(points[i].X, points[i].Y) - cy,
                                     offsetMapping.MapX(points[i].X, points[i].Y) - cx) * 180.0 / Math.PI;
            deltas[i] = NormalizeDelta(bearing - anglesDeg[i]);
        }

        // 圆均值（atan2 of mean unit vectors）：跨 ±180° 边界的 δᵢ 不失真
        var cosSum = 0.0;
        var sinSum = 0.0;
        foreach (var d in deltas)
        {
            var rad = d * Math.PI / 180.0;
            cosSum += Math.Cos(rad);
            sinSum += Math.Sin(rad);
        }
        var mean = AngleGeometry.NormalizeSignedDeg(Math.Atan2(sinSum, cosSum) * 180.0 / Math.PI);

        var spread = 0.0;
        for (var i = 0; i < deltas.Length; i++)
            for (var j = i + 1; j < deltas.Length; j++)
                spread = Math.Max(spread, Math.Abs(NormalizeDelta(deltas[i] - deltas[j])));

        return (mean, spread);
    }

    public bool IsCalibrated(string cameraId) => _intrinsics.ContainsKey(cameraId);

    /// <summary>内参去畸变。读锁内执行，与热加载（写锁）互斥，防止 Remap 使用已释放的 Map。</summary>
    public VisionImage Undistort(string cameraId, VisionImage src)
    {
        using var mat = VisionImageCv.AsMat(src);
        return VisionImageCv.FromMat(Undistort(cameraId, mat), ownsMat: true);
    }

    /// <summary>内参去畸变（Mat 重载，标定工具与内部映射用）。</summary>
    public Mat Undistort(string cameraId, Mat src)
    {
        _intrinsicLock.EnterReadLock();
        try
        {
            if (!_intrinsics.TryGetValue(cameraId, out var state))
                throw new VisionException(VisionErrorCode.NotCalibrated, $"相机未做内参标定: {cameraId}");

            if (src.Width != state.Profile.Width || src.Height != state.Profile.Height)
                throw new VisionException(
                    VisionErrorCode.NotCalibrated,
                    $"图像分辨率 {src.Width}x{src.Height} 与内参档案 {state.Profile.Width}x{state.Profile.Height} 不一致，请重新标定");

            var dst = new Mat();
            Cv2.Remap(src, dst, state.MapX, state.MapY, InterpolationFlags.Linear);
            return dst;
        }
        finally
        {
            _intrinsicLock.ExitReadLock();
        }
    }

    /// <summary>
    /// 像素位姿转机器人位姿。
    /// stationId 为空时抛 NotCalibrated——防止像素坐标被误当成机器人坐标。
    /// cameraId 非空时校验外参档案与取图相机一致（坐标系错配会让位姿完全错误且无感知）。
    /// 角度通过"中心点 + 方向前推点"两点映射后求 Atan2 得到，
    /// 自动处理 y 轴向下、镜像、非等比缩放等符号问题。
    /// </summary>
    public RobotPose PixelToRobot(string? stationId, PixelPose pose, string? cameraId = null, TcpClientPose? clientPose = null)
    {
        if (string.IsNullOrEmpty(stationId))
            throw new VisionException(VisionErrorCode.NotCalibrated, "配方未设置 stationId");

        if (!_extrinsics.TryGetValue(stationId, out var profile))
            throw new VisionException(VisionErrorCode.NotCalibrated, $"工位未做外参标定: {stationId}");

        if (!string.IsNullOrEmpty(cameraId) &&
            !string.Equals(profile.CameraId, cameraId, StringComparison.OrdinalIgnoreCase))
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"外参相机 {profile.CameraId} 与取图相机 {cameraId} 不一致，请重新标定");

        // 档案分辨率与内参一致性：换相机/改分辨率后旧外参失效，拒绝静默错位
        VerifyResolutionConsistency(profile.CameraId, profile.Width, profile.Height, "外参");

        var a = profile.Affine;
        double Tx(double x, double y) => a[0] * x + a[1] * y + a[2];
        double Ty(double x, double y) => a[3] * x + a[4] * y + a[5];

        const double ForwardPx = 100.0;
        var rad = pose.AngleDeg * Math.PI / 180.0;
        var qx = pose.Cx + ForwardPx * Math.Cos(rad);
        var qy = pose.Cy + ForwardPx * Math.Sin(rad);

        var angleDeg = Math.Atan2(
            Ty(qx, qy) - Ty(pose.Cx, pose.Cy),
            Tx(qx, qy) - Tx(pose.Cx, pose.Cy)) * 180.0 / Math.PI;

        var x = Tx(pose.Cx, pose.Cy);
        var y = Ty(pose.Cx, pose.Cy);

        // 平移合成（OnArm + Translate + 已上报位姿）：映射整体平移，换拍照点不重标。
        var translate = clientPose is not null &&
                        string.Equals(profile.MountType, CameraMountType.OnArm, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(profile.ComposeMode, PoseComposeMode.Translate, StringComparison.OrdinalIgnoreCase) &&
                        profile.HasTeachPose;
        if (translate)
        {
            x += clientPose!.X - profile.TeachTcpX;
            y += clientPose.Y - profile.TeachTcpY;
        }

        return new RobotPose(x, y, AngleGeometry.NormalizeSignedDeg(angleDeg));
    }

    private string ProfileFile(string kind, string id)
    {
        ValidateProfileId(id);
        return Path.Combine(_folder!, $"{id}.{kind}.json");
    }

    /// <summary>档案 Id 校验：非空、不含路径分隔符/非法字符，且不是 "." 或 ".."——
    /// SaveExtrinsic("..") 会把档案写到上级目录、DeleteExtrinsic 可删任意 json，必须拦截。
    /// Path.GetFileName(id) != id 说明 id 含有路径段（如 "a/.."），同样拒绝。</summary>
    private static void ValidateProfileId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id is "." or ".." ||
            id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || Path.GetFileName(id) != id)
            throw new InvalidDataException($"档案 Id 非法: {id}");
    }

    private void WriteJson(string path, object profile)
    {
        Directory.CreateDirectory(_folder!);
        AtomicWriteAllText(path, JsonSerializer.Serialize(profile, JsonOptions));
    }

    /// <summary>
    /// 原子落盘（临时文件 + File.Replace）：标定档案是产线关键资产，
    /// 直接 Write halfway 崩溃/断电会留下截断 JSON 且无备份，重启后档案不可用。
    /// 与 RobotVision.Hosting.JsonAtomicWrite 同思路（该类为 Hosting internal，此处独立实现）。
    /// </summary>
    public static void AtomicWriteAllText(string path, string content)
    {
        var full = Path.GetFullPath(path);
        var dir = Path.GetDirectoryName(full)!;
        Directory.CreateDirectory(dir);
        var tmp = Path.Combine(dir, $".{Path.GetFileName(full)}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(tmp, content);
            if (File.Exists(full))
                File.Replace(tmp, full, null);
            else
                File.Move(tmp, full);
        }
        finally
        {
            try { File.Delete(tmp); }
            catch (IOException) { }
        }
    }

    private bool DeleteProfileFile(string id, string kind)
    {
        if (string.IsNullOrEmpty(_folder))
            return false;
        // 与 ProfileFile 同样的 Id 校验：非法 Id（含 ".." 路径穿越）拒绝删除并返回失败
        if (string.IsNullOrWhiteSpace(id) || id is "." or ".." ||
            id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || Path.GetFileName(id) != id)
            return false;
        var file = Path.Combine(_folder, $"{id}.{kind}.json");
        if (!File.Exists(file))
            return false;
        File.Delete(file);
        return true;
    }

    private void RequireFolder()
    {
        if (string.IsNullOrEmpty(_folder))
            throw new InvalidOperationException("标定目录未初始化（先调用 LoadDirectory）");
    }

    private static Mat ToMat(double[] values, int rows, int cols)
    {
        var mat = new Mat(rows, cols, MatType.CV_64F);
        var k = 0;
        for (var i = 0; i < rows; i++)
            for (var j = 0; j < cols; j++)
                mat.Set(i, j, values[k++]);
        return mat;
    }

    public void Dispose()
    {
        _intrinsicLock.EnterWriteLock();
        try
        {
            foreach (var state in _intrinsics.Values)
            {
                state.MapX.Dispose();
                state.MapY.Dispose();
            }
            _intrinsics.Clear();
        }
        finally
        {
            _intrinsicLock.ExitWriteLock();
        }
        _intrinsicLock.Dispose();
    }
}
