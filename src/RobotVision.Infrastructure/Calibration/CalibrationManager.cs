using System.Collections.Concurrent;
using System.Text.Json;
using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Assets;
using RobotVision.Core.Geometry;
using RobotVision.Core.IO;
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
/// 3. 旋转中心档案（按工位 Id）——第 4 轴轴心，偏心工具补偿用；
/// 4. 多项式档案（按工位 Id）——单图模式，原图推理 + 多项式映射；
/// 5. 比例档案（按工位 Id）——无标定板工位的回退路径。
/// 一致性铁律：去畸变后的图像 = 推理输入 = 外参/旋转中心标定时的图像坐标系。
/// 并发安全：Undistort 与 LoadIntrinsic/DeleteIntrinsic 通过 ReaderWriterLockSlim
/// 互斥，防止热加载替换档案时 Remap 使用已释放的 OpenCV Mat。
/// <para>
/// 实现说明：外参/旋转中心/多项式/比例四类为纯 JSON 档案，载入-保存-删除-目录扫描
/// 语义完全一致，已下沉到泛型仓储 <see cref="JsonProfileStore{TProfile}"/>，
/// 各自的校验与质量规则由 <see cref="IJsonProfileKind{TProfile}"/> 描述符提供
/// （见 JsonProfileKinds.cs）。本类保留跨档案种类的编排职责（映射优先级、
/// 工位指纹、双档案并存告警、位姿校验、坐标换算）与内参的 OpenCV 生命周期管理。
/// </para>
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

    /// <summary>留一交叉验证最大误差告警阈值（机器人单位）：超过说明可能存在抄错/误点
    /// （单个坏点对整体拟合被均摊，残差好看但留一误差急剧放大）。</summary>
    public const double LeaveOneOutWarnLimit = 1.0;

    /// <summary>比例 X/Y 各向异性告警阈值：|kx/ky − 1| 超过 2% 说明存在旋转/透视/畸变，
    /// 线性比例只能近似，建议改用多项式标定。</summary>
    public const double ScaleAnisotropyWarnLimit = 0.02;

    private sealed record IntrinsicState(IntrinsicProfile Profile, Mat MapX, Mat MapY);

    // 内参：含 OpenCV 映射表与非托管内存，生命周期与其余四类不同，单独管理（不进泛型仓储）
    private readonly ConcurrentDictionary<string, IntrinsicState> _intrinsics = new(StringComparer.OrdinalIgnoreCase);

    private readonly JsonProfileStore<ExtrinsicProfile> _extrinsics = new(ExtrinsicKind.Instance);
    private readonly JsonProfileStore<RotationCenterProfile> _rotationCenters = new(RotationCenterKind.Instance);
    private readonly JsonProfileStore<PolynomialProfile> _polynomials = new(PolynomialKind.Instance);
    private readonly JsonProfileStore<ScaleProfile> _scales = new(ScaleKind.Instance);

    private readonly ConcurrentQueue<string> _qualityWarnings = new();

    /// <summary>质量警告保留上限：只保留最近 N 条，避免加载大量档案时无限累积。</summary>
    private const int MaxQualityWarnings = 50;

    private readonly ReaderWriterLockSlim _intrinsicLock = new();
    private string? _folder;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>钉扎指纹用：紧凑 camelCase，不含质量字段与时间戳。</summary>
    private static readonly JsonSerializerOptions FingerprintJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public int IntrinsicCount => _intrinsics.Count;

    public int ExtrinsicCount => _extrinsics.Count;

    public int RotationCenterCount => _rotationCenters.Count;

    public int PolynomialCount => _polynomials.Count;

    public int ScaleCount => _scales.Count;

    /// <summary>已加载的比例标定档案（供管理界面显示）。</summary>
    public IReadOnlyList<ScaleProfile> ScaleProfiles =>
        _scales.Values.OrderBy(p => p.StationId, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>工位比例档案（像素→毫米换算，测量显示用）。无档案返回 null。</summary>
    public ScaleProfile? GetScale(string? stationId) => _scales.Get(stationId);

    /// <summary>已加载的多项式标定档案（供管理界面显示）。</summary>
    public IReadOnlyList<PolynomialProfile> PolynomialProfiles =>
        _polynomials.Values.OrderBy(p => p.StationId, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>工位是否使用多项式标定（单图模式）：管线据此跳过内参去畸变、走多项式映射。</summary>
    public bool HasPolynomial(string? stationId) => _polynomials.Contains(stationId);

    /// <summary>工位是否已有外参档案。</summary>
    public bool HasExtrinsic(string? stationId) => _extrinsics.Contains(stationId);

    /// <summary>工位映射模式：管线据此选择坐标换算路径与图像预处理（去畸变与否）。
    /// 优先级：多项式 &gt; 外参 &gt; 比例——外参/多项式输出机器人系坐标（生产首选），
    /// 比例仅是无标定板工位的回退：输出图像平面毫米（原点=图像左上角，X 右 Y 下，与 UI 预览同向）。
    /// None 时走外参路径（PixelToRobot 统一报 1004）。</summary>
    public StationMappingMode GetMappingMode(string? stationId)
    {
        if (string.IsNullOrEmpty(stationId))
            return StationMappingMode.None;
        if (_polynomials.Contains(stationId))
            return StationMappingMode.Polynomial;
        if (_extrinsics.Contains(stationId))
            return StationMappingMode.Extrinsic;
        if (_scales.Contains(stationId))
            return StationMappingMode.Scale;
        return StationMappingMode.None;
    }

    /// <summary>
    /// 工位映射指纹：只纳入影响像素→机器人结果的字段（仿射/多项式系数/比例/示教位姿/分辨率）。
    /// 不含 CalibratedAt、Rms、残差数组等质量元数据——重保存或档案加字段不应误报 1017。
    /// 外参工位额外并入去畸变所用内参（<paramref name="undistortCameraId"/>，缺省取外参 CameraId）。
    /// <paramref name="includeRotation"/> 为 true 时并入旋转中心几何（偏心补偿）。
    /// 无映射档案返回 null。
    /// </summary>
    public string? ComputeStationSha256(
        string? stationId, bool includeRotation = false, string? undistortCameraId = null)
    {
        if (string.IsNullOrEmpty(stationId))
            return null;

        var mode = GetMappingMode(stationId);
        MappingFingerprint? mapping = mode switch
        {
            StationMappingMode.Polynomial when _polynomials.Get(stationId) is { } poly =>
                FromPolynomial(poly),
            StationMappingMode.Extrinsic when _extrinsics.Get(stationId) is { } ext =>
                FromExtrinsic(ext),
            StationMappingMode.Scale when _scales.Get(stationId) is { } scale =>
                FromScale(scale),
            _ => null,
        };
        if (mapping is null)
            return null;

        IntrinsicFingerprint? intrinsic = null;
        if (mode == StationMappingMode.Extrinsic)
        {
            var cameraId = string.IsNullOrWhiteSpace(undistortCameraId)
                ? mapping.CameraId
                : undistortCameraId;
            if (_intrinsics.TryGetValue(cameraId, out var state))
                intrinsic = FromIntrinsic(state.Profile);
        }

        RotationFingerprint? rotation = null;
        if (includeRotation && _rotationCenters.Get(stationId) is { } rot)
            rotation = FromRotation(rot);

        return FileSha256.ComputeUtf8(
            JsonSerializer.Serialize(new StationFingerprint(mapping, intrinsic, rotation), FingerprintJson));
    }

    private sealed record StationFingerprint(
        MappingFingerprint Mapping, IntrinsicFingerprint? Intrinsic, RotationFingerprint? Rotation);

    private sealed record MappingFingerprint(
        string Kind,
        string StationId,
        string CameraId,
        int Width,
        int Height,
        double[]? Affine,
        int Order,
        double[]? CoefX,
        double[]? CoefY,
        double ScaleX,
        double ScaleY,
        string MountType,
        string ComposeMode,
        string CoordinateSpace,
        double TeachTcpX,
        double TeachTcpY,
        double TeachRzDeg,
        bool HasTeachPose,
        double CalibrationPlaneZ);

    private sealed record IntrinsicFingerprint(
        string CameraId, int Width, int Height, double[] CameraMatrix, double[] DistCoeffs);

    private sealed record RotationFingerprint(
        string StationId, string CameraId, double Cx, double Cy, double RadiusPx,
        int Width, int Height, double ToolOffsetDeg);

    private static MappingFingerprint FromPolynomial(PolynomialProfile p) => new(
        "polynomial", p.StationId, p.CameraId, p.Width, p.Height,
        Affine: null, p.Order, p.CoefX, p.CoefY, 0, 0,
        p.MountType, p.ComposeMode, p.CoordinateSpace,
        p.TeachTcpX, p.TeachTcpY, p.TeachRzDeg, p.HasTeachPose, p.CalibrationPlaneZ);

    private static MappingFingerprint FromExtrinsic(ExtrinsicProfile p) => new(
        "extrinsic", p.StationId, p.CameraId, p.Width, p.Height,
        p.Affine, Order: 0, CoefX: null, CoefY: null, 0, 0,
        p.MountType, p.ComposeMode, CoordinateSpace: "",
        p.TeachTcpX, p.TeachTcpY, p.TeachRzDeg, p.HasTeachPose, p.CalibrationPlaneZ);

    private static MappingFingerprint FromScale(ScaleProfile p) => new(
        "scale", p.StationId, p.CameraId, p.Width, p.Height,
        Affine: null, Order: 0, CoefX: null, CoefY: null, p.ScaleX, p.ScaleY,
        MountType: "", ComposeMode: "", CoordinateSpace: "",
        0, 0, 0, false, 0);

    private static IntrinsicFingerprint FromIntrinsic(IntrinsicProfile p) =>
        new(p.CameraId, p.Width, p.Height, p.CameraMatrix, p.DistCoeffs);

    private static RotationFingerprint FromRotation(RotationCenterProfile p) =>
        new(p.StationId, p.CameraId, p.Cx, p.Cy, p.RadiusPx, p.Width, p.Height, p.ToolOffsetDeg);

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
    /// 扫描目录加载 *.intrinsic.json / *.extrinsic.json / *.rotation.json / *.polynomial.json / *.scale.json。
    /// 返回加载失败清单（坏档案隔离，不影响其他档案；同时记录质量超标警告）。
    /// </summary>
    public IReadOnlyList<(string File, string Error)> LoadDirectory(string folder)
    {
        _folder = folder;
        var errors = new List<(string, string)>();
        if (!Directory.Exists(folder))
            return errors;

        // 加载顺序与原实现保持一致：内参 → 外参 → 旋转中心 → 多项式 → 比例。
        // 五类共用同一份扫描实现（排序遍历 + Id 去重 + 文件名一致性告警），
        // 仅"取 Id"与"载入动作"不同——内参要即时构建去畸变映射表，故单独传 LoadIntrinsic。
        LoadKindFromDirectory<IntrinsicProfile>(folder, "intrinsic", errors, p => p.CameraId, LoadIntrinsic);
        LoadKindFromDirectory<ExtrinsicProfile>(folder, _extrinsics.Kind, errors, _extrinsics.IdOf, LoadExtrinsic);
        LoadKindFromDirectory<RotationCenterProfile>(folder, _rotationCenters.Kind, errors, _rotationCenters.IdOf, LoadRotationCenter);
        LoadKindFromDirectory<PolynomialProfile>(folder, _polynomials.Kind, errors, _polynomials.IdOf, LoadPolynomial);
        LoadKindFromDirectory<ScaleProfile>(folder, _scales.Kind, errors, _scales.IdOf, LoadScale);

        return errors;
    }

    /// <summary>
    /// 目录扫描的统一实现：按文件名排序遍历 + 档案 Id 去重 + 文件名一致性告警 + 坏档案隔离。
    /// 原先五类档案各有一份近乎复制粘贴的循环（约 120 行），现只保留一份，
    /// 杜绝"某一类漏了排序/去重"这类只会在现场暴露的偏差。
    /// </summary>
    /// <param name="kind">文件名中缀，扫描 <c>*.{kind}.json</c>。</param>
    /// <param name="idOf">取档案主键（内参按相机 Id，其余按工位 Id）。</param>
    /// <param name="load">载入动作（含校验与质量告警），异常由本方法捕获并计入错误清单。</param>
    private void LoadKindFromDirectory<TProfile>(
        string folder,
        string kind,
        List<(string File, string Error)> errors,
        Func<TProfile, string> idOf,
        Action<TProfile> load)
        where TProfile : class
    {
        if (!Directory.Exists(folder))
            return;

        // 按文件名排序遍历 + 档案 Id 去重：手工重命名产生同 Id 双档案时，
        // 确定性地"文件名排序在先者生效、后者报错"（此前是枚举序后者覆盖，结果不可预测）
        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(folder, $"*.{kind}.json")
                     .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var profile = JsonSerializer.Deserialize<TProfile>(File.ReadAllText(file))
                    ?? throw new InvalidDataException("档案内容为空");
                var id = idOf(profile);
                if (seen.TryGetValue(id, out var firstFile))
                {
                    errors.Add((Path.GetFileName(file),
                        $"档案 Id 重复: {id} 已由 {Path.GetFileName(firstFile)} 加载（按文件名排序先者生效），请删除多余档案"));
                    continue;
                }
                load(profile);
                seen[id] = file;
                WarnIfFileNameMismatch(file, kind, id);
            }
            catch (Exception ex)
            {
                errors.Add((Path.GetFileName(file), ex.Message));
            }
        }
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

    // ---- 载入：纯 JSON 档案统一走泛型仓储，跨种类的"双档案并存"告警由本类编排 ----

    public void LoadExtrinsic(ExtrinsicProfile profile)
    {
        _extrinsics.Load(profile, AddQualityWarning);
        WarnIfDualMapping(profile.StationId);
    }

    public void LoadRotationCenter(RotationCenterProfile profile) =>
        _rotationCenters.Load(profile, AddQualityWarning);

    public void LoadPolynomial(PolynomialProfile profile)
    {
        _polynomials.Load(profile, AddQualityWarning);
        WarnIfDualMapping(profile.StationId);
    }

    public void LoadScale(ScaleProfile profile)
    {
        _scales.Load(profile, AddQualityWarning);
        WarnIfDualMapping(profile.StationId);
    }

    /// <summary>同一工位映射档案并存警告：多项式+外参 → 管线只走多项式；
    /// (多项式|外参)+比例 → 比例不参与管线（仅测量显示）。必须警告，否则操作员以为被忽略的那份仍生效。</summary>
    private void WarnIfDualMapping(string stationId)
    {
        if (_polynomials.Contains(stationId) && _extrinsics.Contains(stationId))
            AddQualityWarning($"工位 {stationId} 同时存在多项式与外参档案：生产优先使用多项式（原图+多项式映射），外参/去畸变被忽略。请删除不用的那份以免坐标系混淆");
        if (_scales.Contains(stationId) &&
            (_polynomials.Contains(stationId) || _extrinsics.Contains(stationId)))
            AddQualityWarning($"工位 {stationId} 同时存在比例与外参/多项式档案：管线优先使用外参/多项式（机器人系坐标），比例档案仅用于测量显示");
    }

    // ---- 保存：写回目录 + 立即热加载（无需重启）----

    public void SaveExtrinsic(ExtrinsicProfile profile)
    {
        RequireFolder();
        _extrinsics.Save(profile, AddQualityWarning, ProfileFile, WriteJson);
        WarnIfDualMapping(profile.StationId);
    }

    public void SaveRotationCenter(RotationCenterProfile profile)
    {
        RequireFolder();
        _rotationCenters.Save(profile, AddQualityWarning, ProfileFile, WriteJson);
    }

    public void SavePolynomial(PolynomialProfile profile)
    {
        RequireFolder();
        _polynomials.Save(profile, AddQualityWarning, ProfileFile, WriteJson);
        WarnIfDualMapping(profile.StationId);
    }

    public void SaveScale(ScaleProfile profile)
    {
        RequireFolder();
        _scales.Save(profile, AddQualityWarning, ProfileFile, WriteJson);
        WarnIfDualMapping(profile.StationId);
    }

    // ---- 删除（文件 + 缓存）----

    public bool DeleteExtrinsic(string stationId) => _extrinsics.Delete(stationId, DeleteProfileFile);

    public bool DeleteRotationCenter(string stationId) => _rotationCenters.Delete(stationId, DeleteProfileFile);

    public bool DeletePolynomial(string stationId) => _polynomials.Delete(stationId, DeleteProfileFile);

    public bool DeleteScale(string stationId) => _scales.Delete(stationId, DeleteProfileFile);

    // ---- 值域校验 / 质量评估：规则已下沉到各档案种类描述符，此处保留 public 门面 ----

    /// <summary>外参档案值域校验。规则见 <see cref="ExtrinsicKind"/>。</summary>
    public static void ValidateExtrinsic(ExtrinsicProfile profile) => ExtrinsicKind.Instance.Validate(profile);

    /// <summary>旋转中心档案值域校验。规则见 <see cref="RotationCenterKind"/>。</summary>
    public static void ValidateRotationCenter(RotationCenterProfile profile) => RotationCenterKind.Instance.Validate(profile);

    /// <summary>多项式档案值域校验。规则见 <see cref="PolynomialKind"/>。</summary>
    public static void ValidatePolynomial(PolynomialProfile profile) => PolynomialKind.Instance.Validate(profile);

    /// <summary>比例档案值域校验。规则见 <see cref="ScaleKind"/>。</summary>
    public static void ValidateScale(ScaleProfile profile) => ScaleKind.Instance.Validate(profile);

    public static CalibrationQuality AssessExtrinsic(ExtrinsicProfile p) => ExtrinsicKind.Instance.Assess(p);

    public static CalibrationQuality AssessRotation(RotationCenterProfile p) => RotationCenterKind.Instance.Assess(p);

    public static CalibrationQuality AssessPolynomial(PolynomialProfile p) => PolynomialKind.Instance.Assess(p);

    // ---- 内参：含 OpenCV 映射表与非托管内存，生命周期与纯 JSON 档案不同，单独实现 ----

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

    public void SaveIntrinsic(IntrinsicProfile profile)
    {
        RequireFolder();
        ValidateIntrinsic(profile);
        WriteJson(ProfileFile("intrinsic", profile.CameraId), profile);
        LoadIntrinsic(profile);
    }

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

    public static CalibrationQuality AssessIntrinsic(IntrinsicProfile p) =>
        p.Rms <= IntrinsicRmsGood ? CalibrationQuality.Good
        : p.Rms <= IntrinsicRmsFair ? CalibrationQuality.Fair
        : CalibrationQuality.Poor;

    /// <summary>标定前置检查：外参/旋转中心标定前该相机必须先完成内参标定（一致性铁律）。</summary>
    public void RequireIntrinsic(string cameraId)
    {
        if (!_intrinsics.ContainsKey(cameraId))
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"相机未做内参标定: {cameraId}（外参/旋转中心标定前必须先完成内参标定）");
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

    // ---- 坐标映射与位姿校验（跨档案种类的编排职责）----

    /// <summary>质量警告入队，超过保留上限时丢弃最旧条目（固定容量，避免无限累积）。</summary>
    private void AddQualityWarning(string message)
    {
        _qualityWarnings.Enqueue(message);
        while (_qualityWarnings.Count > MaxQualityWarnings)
            _qualityWarnings.TryDequeue(out _);
    }

    /// <summary>多项式工位的推理图像分辨率校验：换分辨率后归一化坐标错位，映射整体失效。</summary>
    public void VerifyPolynomialResolution(string stationId, int width, int height)
    {
        var profile = _polynomials.Get(stationId)
            ?? throw new VisionException(VisionErrorCode.NotCalibrated, $"工位未做多项式标定: {stationId}");
        if (profile.Width != width || profile.Height != height)
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"图像分辨率 {width}x{height} 与多项式标定档案 {profile.Width}x{profile.Height} 不一致，请重新标定");
    }

    /// <summary>比例工位的推理图像分辨率校验：比例以像素为基准，换分辨率后 mm/px 整体失效。
    /// 档案未记录分辨率（Width=0，旧档/手填遗漏）时跳过（无从比对）。</summary>
    public void VerifyScaleResolution(string stationId, int width, int height)
    {
        var profile = _scales.Get(stationId)
            ?? throw new VisionException(VisionErrorCode.NotCalibrated, $"工位未录入比例标定: {stationId}");
        if (profile.Width > 0 && (profile.Width != width || profile.Height != height))
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"图像分辨率 {width}x{height} 与比例标定档案 {profile.Width}x{profile.Height} 不一致，请重新录入比例");
    }

    /// <summary>多项式工位的 TRIGGER 位姿前置校验（取图前拦截）：
    /// Check 模式比对 XY/RZ 全部容差；Translate 模式仅比对 RZ（平移是合成参数不是拒绝条件）。
    /// Image 坐标空间（免示教毫米系）无机器人系概念，直接跳过。
    /// Fixed / 未记录位姿 / 未上报位姿 / 关闭校验时放行。</summary>
    public void VerifyPolynomialClientPose(string? stationId, TcpClientPose? pose)
    {
        if (!PoseCheckEnabled || pose is null || string.IsNullOrEmpty(stationId))
            return;
        if (_polynomials.Get(stationId) is not { } profile)
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
        if (_polynomials.Get(stationId) is not { } profile)
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
        if (_scales.Get(stationId) is not { } profile)
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
        if (_polynomials.Get(stationId) is { } poly)
            return ((x, y) => poly.Evaluate(x, y).X, (x, y) => poly.Evaluate(x, y).Y, poly.CameraId);

        if (_extrinsics.Get(stationId) is { } ext)
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
        if (_rotationCenters.Get(stationId) is not { } rc)
            throw new VisionException(VisionErrorCode.NotCalibrated, $"工位未做旋转中心标定: {stationId}");

        var mapping = GetMapping(stationId)
            ?? throw new VisionException(VisionErrorCode.NotCalibrated, $"工位未做外参/多项式标定: {stationId}");

        if (!string.Equals(mapping.CameraId, rc.CameraId, StringComparison.OrdinalIgnoreCase))
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"映射相机 {mapping.CameraId} 与旋转中心相机 {rc.CameraId} 不一致，请重新标定");

        // 分辨率一致性：外参链路校验外参与旋转中心 vs 内参（去畸变坐标系）；
        // 多项式链路校验旋转中心 vs 多项式档案（原图坐标系，无内参概念）
        if (_extrinsics.Get(stationId) is { } ext)
        {
            VerifyResolutionConsistency(ext.CameraId, ext.Width, ext.Height, "外参");
            VerifyResolutionConsistency(rc.CameraId, rc.Width, rc.Height, "旋转中心");
        }
        else if (_polynomials.Get(stationId) is { } poly &&
                 (rc.Width != poly.Width || rc.Height != poly.Height) &&
                 rc.Width > 0 && rc.Height > 0)
        {
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
        var toolOffset = (_rotationCenters.Get(stationId)
            ?? throw new VisionException(VisionErrorCode.NotCalibrated, $"工位未做旋转中心标定: {stationId}")).ToolOffsetDeg;
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
        if (_polynomials.Get(stationId) is { } poly)
        {
            if (string.Equals(poly.CoordinateSpace, PolynomialCoordinateSpace.Image, StringComparison.OrdinalIgnoreCase))
                return false;
            return string.Equals(poly.MountType, CameraMountType.OnArm, StringComparison.OrdinalIgnoreCase)
                   && poly.HasTeachPose;
        }
        if (_extrinsics.Get(stationId) is { } ext)
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
        if (_extrinsics.Get(stationId) is not { } ext)
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

        if (_extrinsics.Get(stationId) is not { } profile)
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

    // ---- 落盘基础设施 ----

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
        // 原子落盘统一走 Core/IO/AtomicFile（原本类自带一份 public static 实现，
        // 已删除——工具方法不该挂在管理器上，跨层调用方改用 AtomicFile）
        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(profile, JsonOptions));
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
