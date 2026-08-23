using System.Collections.Concurrent;
using System.Text.Json;
using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Geometry;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;

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
    private readonly ConcurrentQueue<string> _qualityWarnings = new();

    /// <summary>质量警告保留上限：只保留最近 N 条，避免加载大量档案时无限累积。</summary>
    private const int MaxQualityWarnings = 50;

    private readonly ReaderWriterLockSlim _intrinsicLock = new();
    private string? _folder;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public int IntrinsicCount => _intrinsics.Count;

    public int ExtrinsicCount => _extrinsics.Count;

    public int RotationCenterCount => _rotationCenters.Count;

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

        foreach (var file in Directory.EnumerateFiles(folder, "*.intrinsic.json"))
        {
            try
            {
                var profile = JsonSerializer.Deserialize<IntrinsicProfile>(File.ReadAllText(file))
                    ?? throw new InvalidDataException("档案内容为空");
                LoadIntrinsic(profile);
            }
            catch (Exception ex)
            {
                errors.Add((Path.GetFileName(file), ex.Message));
            }
        }

        foreach (var file in Directory.EnumerateFiles(folder, "*.extrinsic.json"))
        {
            try
            {
                var profile = JsonSerializer.Deserialize<ExtrinsicProfile>(File.ReadAllText(file))
                    ?? throw new InvalidDataException("档案内容为空");
                LoadExtrinsic(profile);
            }
            catch (Exception ex)
            {
                errors.Add((Path.GetFileName(file), ex.Message));
            }
        }

        foreach (var file in Directory.EnumerateFiles(folder, "*.rotation.json"))
        {
            try
            {
                var profile = JsonSerializer.Deserialize<RotationCenterProfile>(File.ReadAllText(file))
                    ?? throw new InvalidDataException("档案内容为空");
                LoadRotationCenter(profile);
            }
            catch (Exception ex)
            {
                errors.Add((Path.GetFileName(file), ex.Message));
            }
        }

        return errors;
    }

    public void LoadIntrinsic(IntrinsicProfile profile)
    {
        ValidateIntrinsic(profile);

        using var cameraMatrix = ToMat(profile.CameraMatrix, 3, 3);
        using var distCoeffs = ToMat(profile.DistCoeffs, 1, profile.DistCoeffs.Length);
        using var noRotation = new Mat();

        var mapX = new Mat();
        var mapY = new Mat();
        Cv2.InitUndistortRectifyMap(
            cameraMatrix, distCoeffs, noRotation, cameraMatrix,
            new Size(profile.Width, profile.Height), MatType.CV_32FC1, mapX, mapY);

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

    /// <summary>内参字段校验（CameraMatrix 9 元素 / 分辨率 / 畸变系数长度）。非法档案拒绝加载，避免越界崩溃。
    /// DistCoeffs 允许为空（空数组视为零畸变，与标准输出一致）。</summary>
    public static void ValidateIntrinsic(IntrinsicProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.CameraId))
            throw new InvalidDataException("内参 CameraId 为空（空串 Id 会导致档案互相覆盖）");
        if (profile.CameraMatrix is not { Length: 9 })
            throw new InvalidDataException($"内参 CameraMatrix 必须为 9 元素，当前 {profile.CameraMatrix?.Length ?? 0}");
        if (profile.Width <= 0 || profile.Height <= 0)
            throw new InvalidDataException($"内参分辨率非法: {profile.Width}x{profile.Height}");
        if (profile.DistCoeffs.Length > 14)
            throw new InvalidDataException($"内参畸变系数长度非法: {profile.DistCoeffs.Length}");
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
    }

    public void LoadExtrinsic(ExtrinsicProfile profile)
    {
        ValidateExtrinsic(profile);
        _extrinsics[profile.StationId] = profile;
    }

    public void LoadRotationCenter(RotationCenterProfile profile)
    {
        ValidateRotationCenter(profile);
        _rotationCenters[profile.StationId] = profile;
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
    /// 旋转轴心的机器人坐标（像素轴心经外参仿射映射）。
    /// 仿射把圆映成椭圆时圆心仍映到中心，故像素空间拟合中心再映射是安全的。
    /// 外参与旋转中心必须来自同一相机（同一像素坐标系）。
    /// </summary>
    public Point2d RotationCenterRobot(string stationId)
    {
        if (!_extrinsics.TryGetValue(stationId, out var ext))
            throw new VisionException(VisionErrorCode.NotCalibrated, $"工位未做外参标定: {stationId}");

        if (!_rotationCenters.TryGetValue(stationId, out var rc))
            throw new VisionException(VisionErrorCode.NotCalibrated, $"工位未做旋转中心标定: {stationId}");

        if (!string.Equals(ext.CameraId, rc.CameraId, StringComparison.OrdinalIgnoreCase))
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"外参相机 {ext.CameraId} 与旋转中心相机 {rc.CameraId} 不一致，请重新标定");

        var a = ext.Affine;
        return new Point2d(a[0] * rc.Cx + a[1] * rc.Cy + a[2], a[3] * rc.Cx + a[4] * rc.Cy + a[5]);
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
        return RotationCenterCompensation.Apply(pose, center.X, center.Y);
    }

    public bool IsCalibrated(string cameraId) => _intrinsics.ContainsKey(cameraId);

    /// <summary>内参去畸变。读锁内执行，与热加载（写锁）互斥，防止 Remap 使用已释放的 Map。</summary>
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
    /// stationId 为空且 allowPassthrough=false 时抛 NotCalibrated——
    /// 静默直通会让像素坐标（数值量级与机器人坐标相近）被误当成机器人坐标，属安全问题。
    /// cameraId 非空时校验外参档案与取图相机一致（坐标系错配会让位姿完全错误且无感知）。
    /// 角度通过"中心点 + 方向前推点"两点映射后求 Atan2 得到，
    /// 自动处理 y 轴向下、镜像、非等比缩放等符号问题。
    /// </summary>
    public RobotPose PixelToRobot(string? stationId, PixelPose pose, bool allowPassthrough = false, string? cameraId = null)
    {
        if (string.IsNullOrEmpty(stationId))
        {
            if (!allowPassthrough)
                throw new VisionException(VisionErrorCode.NotCalibrated,
                    "配方未设置 stationId（如需台架调试直通像素坐标，请显式设置 debugPassthrough=true）");
            return new RobotPose(pose.Cx, pose.Cy, pose.AngleDeg);
        }

        if (!_extrinsics.TryGetValue(stationId, out var profile))
            throw new VisionException(VisionErrorCode.NotCalibrated, $"工位未做外参标定: {stationId}");

        if (!string.IsNullOrEmpty(cameraId) &&
            !string.Equals(profile.CameraId, cameraId, StringComparison.OrdinalIgnoreCase))
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"外参相机 {profile.CameraId} 与取图相机 {cameraId} 不一致，请重新标定");

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

        return new RobotPose(Tx(pose.Cx, pose.Cy), Ty(pose.Cx, pose.Cy), AngleGeometry.NormalizeSignedDeg(angleDeg));
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
        File.WriteAllText(path, JsonSerializer.Serialize(profile, JsonOptions));
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
