// 标定工具
//
// 内参（棋盘格）:
//   CalibTool intrinsic --camera cam_file --folder <棋盘图片目录> --cols 9 --rows 6 --square 5.0
//   cols/rows 为棋盘内角点数（格数-1），square 为方格边长 mm
//
// 外参（九点法）:
//   CalibTool extrinsic --camera cam_file --station st1 --file pairs.csv
//   pairs.csv 每行: 像素x,像素y,机器人x,机器人y（# 开头为注释，首行可放表头，推荐 9 点）
//
// 旋转中心（偏心工具补偿，即"九点+3"的 3 点部分）:
//   CalibTool rotation --camera cam_file --station st1 --file points.csv
//   points.csv 每行: 像素x,像素y（第 4 轴转到不同角度后标记点的像素坐标）
//   推荐 5~9 个角度等间隔分布（如每 45°）；像素坐标须与推理同坐标系
//   （外参工位=去畸变图；多项式工位=原图，不要求内参）
//
// 多项式标定（单图模式，替代内参+外参两步）:
//   CalibTool polynomial --camera cam --station st1 --image board.png --ref ref.csv
//                        --cols 9 --rows 6 --square 5.0 [--order 2|3] [--mount fixed|onarm]
//                        [--compose check|translate] [--tcp-x --tcp-y --rz] [--plane-z]
//   ref.csv 两行: 像素x,像素y,机器人x,机器人y（同一行的 2 个参考角点；像素列可粗略，内部吸附）
//
// 模板生成:
//   CalibTool template --kind extrinsic|rotation [--out <文件>]
//
// 结果默认保存到 data/calibration，App 启动时自动加载。

using System.Globalization;
using System.Text.Json;
using OpenCvSharp;
using RobotVision.CalibTool;
using RobotVision.Core;
using RobotVision.Core.IO;
using RobotVision.Core.Models;
using RobotVision.Infrastructure.Calibration;

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

var command = args[0].ToLowerInvariant();
var options = ParseOptions(args.Skip(1));

try
{
    switch (command)
    {
        case "intrinsic":
            RunIntrinsic(options);
            break;
        case "extrinsic":
            RunExtrinsic(options);
            break;
        case "rotation":
            RunRotation(options);
            break;
        case "polynomial":
            RunPolynomial(options);
            break;
        case "template":
            RunTemplate(options);
            break;
        default:
            Console.WriteLine($"未知命令: {command}");
            PrintUsage();
            return 1;
    }
    return 0;
}
catch (VisionException ex)
{
    Console.WriteLine($"标定失败: {ex.Message}");
    return 1;
}
catch (Exception ex)
{
    // 顶层兜底：未预期的异常也输出清晰错误而非崩溃（返回非零退出码供脚本判断）
    Console.WriteLine($"标定异常: {ex.Message}");
    if (ex is FormatException)
        Console.WriteLine("请检查参数格式，详见上方用法说明");
    return 1;
}

static void RunIntrinsic(Dictionary<string, string> options)
{
    var cameraId = Require(options, "camera");
    var folder = Require(options, "folder");
    var cols = int.Parse(Require(options, "cols"));
    var rows = int.Parse(Require(options, "rows"));
    var square = double.Parse(Require(options, "square"), CultureInfo.InvariantCulture);
    var outDir = ResolveOutDir(options.GetValueOrDefault("out", "data/calibration"));

    // 数值边界：内角点数至少 2×2（否则不构成可检测的棋盘格）；方格边长必须为正的有限数
    if (cols < 2)
        throw new FormatException($"--cols 内角点列数必须 ≥2（当前 {cols}）");
    if (rows < 2)
        throw new FormatException($"--rows 内角点行数必须 ≥2（当前 {rows}）");
    if (!double.IsFinite(square) || square <= 0)
        throw new FormatException($"--square 方格边长必须是正的有限数（当前 {square}），请检查是否输入了 NaN/Infinity 或非正值");

    var files = Directory.EnumerateFiles(folder)
        .Where(f => new[] { ".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff" }
            .Contains(Path.GetExtension(f).ToLowerInvariant()))
        .OrderBy(f => f)
        .ToList();

    Console.WriteLine($"内参标定 | 相机 {cameraId} | 图片 {files.Count} 张 | 棋盘 {cols}x{rows} | 方格 {square}mm");

    var profile = ChessboardIntrinsicCalibrator.Calibrate(cameraId, files, new Size(cols, rows), square);

    Directory.CreateDirectory(outDir);
    var path = Path.Combine(outDir, $"{cameraId}.intrinsic.json");
    AtomicFile.WriteAllText(path,
        JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));

    var k = profile.CameraMatrix;
    Console.WriteLine($"有效图像重投影 RMS: {profile.Rms:0.000} px（参考: ≤0.3 优秀, ≤0.5 可用）· 有效图 {profile.ImageCount} 张");
    if (profile.ImageCount < ChessboardIntrinsicCalibrator.RecommendedImageCount)
        Console.WriteLine($"提示: 有效图仅 {profile.ImageCount} 张，建议 ≥{ChessboardIntrinsicCalibrator.RecommendedImageCount} 张（覆盖四角、姿态多样）");
    if (profile.PerImageRms is { Count: > 0 })
        Console.WriteLine($"单图 RMS 范围: {profile.PerImageRms.Min():0.000} ~ {profile.PerImageRms.Max():0.000} px（最大者为疑似坏图）");
    Console.WriteLine($"fx={k[0]:0.0} fy={k[4]:0.0} cx={k[2]:0.0} cy={k[5]:0.0}，分辨率 {profile.Width}x{profile.Height}");
    Console.WriteLine($"已保存: {path}");

    if (profile.Rms > 0.5)
        Console.WriteLine("警告: RMS 偏大，建议重拍（覆盖四角、姿态多样、对焦清晰）");
}

static void RunExtrinsic(Dictionary<string, string> options)
{
    var cameraId = Require(options, "camera");
    var stationId = Require(options, "station");
    var file = Require(options, "file");
    var outDir = ResolveOutDir(options.GetValueOrDefault("out", "data/calibration"));

    var intrinsic = RequireIntrinsic(cameraId, outDir);

    var (pixelPoints, robotPoints) = CsvPointParser.ParsePairs(File.ReadAllLines(file));

    // 安装模式/拍照位姿/平面 Z（OnArm 档案仅在标定拍照位姿下有效）
    var mount = options.GetValueOrDefault("mount", "Fixed");
    if (!RobotVision.Core.Models.CameraMountType.IsValid(mount))
        throw new FormatException($"--mount 非法: {mount}（仅支持 Fixed / OnArm）");
    var mountUpper = mount.ToUpperInvariant();
    var teachX = double.Parse(options.GetValueOrDefault("tcp-x", "0"), CultureInfo.InvariantCulture);
    var teachY = double.Parse(options.GetValueOrDefault("tcp-y", "0"), CultureInfo.InvariantCulture);
    var teachRz = double.Parse(options.GetValueOrDefault("rz", "0"), CultureInfo.InvariantCulture);
    var planeZ = double.Parse(options.GetValueOrDefault("plane-z", "0"), CultureInfo.InvariantCulture);
    if (mountUpper == "ONARM" && (teachX == 0 && teachY == 0 && teachRz == 0))
        Console.WriteLine("提示: OnArm 未提供 --tcp-x/--tcp-y/--rz，档案将不记录拍照位姿（生产拍照一致性无法核对）");

    Console.WriteLine($"外参标定 | 相机 {cameraId} | 工位 {stationId} | 点数 {pixelPoints.Length} | 安装 {mountUpper}");

    // 记录标定时分辨率：换相机/改分辨率后与内参比对，不一致时 App 拒绝使用旧外参
    var calibrated = NinePointExtrinsicCalibrator.Calibrate(stationId, cameraId,
        [.. pixelPoints], [.. robotPoints], intrinsic.Width, intrinsic.Height);
    var teachPoseProvided = options.ContainsKey("tcp-x") && options.ContainsKey("tcp-y") && options.ContainsKey("rz");
    var profile = calibrated with
    {
        MountType = mountUpper == "ONARM"
            ? RobotVision.Core.Models.CameraMountType.OnArm
            : RobotVision.Core.Models.CameraMountType.Fixed,
        TeachTcpX = teachX,
        TeachTcpY = teachY,
        TeachRzDeg = teachRz,
        // 显式标志：OnArm 且三项位姿都提供才算已记录（拍照点恰为原点也不误判为未记录）
        HasTeachPose = mountUpper == "ONARM" && teachPoseProvided,
        CalibrationPlaneZ = planeZ,
    };

    Directory.CreateDirectory(outDir);
    var path = Path.Combine(outDir, $"{stationId}.extrinsic.json");
    AtomicFile.WriteAllText(path,
        JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));

    Console.WriteLine($"RMS 残差: {profile.Rms:0.0000}，最大残差: {profile.MaxResidual:0.0000}（机器人单位）");
    if (profile.LeaveOneOutMax > 0)
        Console.WriteLine($"留一最大误差: {profile.LeaveOneOutMax:0.0000}（偏大说明存在抄错的点对）");
    if (profile.PointResiduals is { Length: > 0 })
        Console.WriteLine("逐点残差: " + string.Join(" ", profile.PointResiduals.Select(r => r.ToString("0.0000", CultureInfo.InvariantCulture))));
    Console.WriteLine($"已保存: {path}");

    if (profile.MaxResidual > 0.1)
        Console.WriteLine("警告: 残差偏大，请核对点对数据（像素点与机器人点必须一一对应）");
}

static void RunRotation(Dictionary<string, string> options)
{
    var cameraId = Require(options, "camera");
    var stationId = Require(options, "station");
    var file = Require(options, "file");
    var outDir = ResolveOutDir(options.GetValueOrDefault("out", "data/calibration"));

    using var probe = new CalibrationManager();
    probe.LoadDirectory(outDir);
    int width, height;
    if (probe.HasPolynomial(stationId))
    {
        var poly = probe.PolynomialProfiles.First(p =>
            string.Equals(p.StationId, stationId, StringComparison.OrdinalIgnoreCase));
        width = poly.Width;
        height = poly.Height;
        Console.WriteLine("多项式工位：旋转中心按原图像素标定（不要求内参）");
    }
    else
    {
        var intrinsic = RequireIntrinsic(cameraId, outDir);
        width = intrinsic.Width;
        height = intrinsic.Height;
    }

    var (points, angles) = CsvPointParser.ParsePoints(File.ReadAllLines(file));
    var toolOffsetText = options.GetValueOrDefault("tool-offset", "0");

    Console.WriteLine($"旋转中心标定 | 相机 {cameraId} | 工位 {stationId} | 点数 {points.Length}"
        + (angles is null ? "" : " | 含第4轴角度（将做方向自检+偏角实测）"));

    // 记录标定时分辨率：换相机/改分辨率后与内参比对，不一致时 App 拒绝使用旧档案
    var calibrated = RotationCenterCalibrator.Calibrate(stationId, cameraId, [.. points],
        width, height);

    // 工具零位偏角：--tool-offset auto = 从带角度的标定点实测（βᵢ−φᵢ 圆均值）；数值 = 手动指定
    double toolOffset;
    double measuredSpread = 0;
    var useMeasured = string.Equals(toolOffsetText, "auto", StringComparison.OrdinalIgnoreCase);
    if (useMeasured)
    {
        if (angles is not { Length: >= 2 })
            throw new FormatException("--tool-offset auto 需要带第4轴角度列（≥2 行）的 CSV");
        using var precheck = new CalibrationManager();
        precheck.LoadDirectory(outDir);
        (toolOffset, measuredSpread) = precheck.ComputeToolOffsetDeg(stationId, calibrated, [.. points], angles);
        Console.WriteLine($"实测工具零位偏角 δ = {toolOffset:0.00}°（离散度 {measuredSpread:0.00}°；若与预期差约 180°，说明标记取在工具另一端，请改用手动数值）");
        if (measuredSpread > 5.0)
            Console.WriteLine("警告: 偏角离散度偏大，标记提取噪声或轴心误差影响实测值");
    }
    else
    {
        toolOffset = double.Parse(toolOffsetText, CultureInfo.InvariantCulture);
        if (!double.IsFinite(toolOffset))
            throw new FormatException("--tool-offset 必须是有限数或 auto");
    }

    var profile = calibrated with { ToolOffsetDeg = toolOffset };

    // 方向自检（CSV 带第4轴角度列时）：外参与轴心经同一映射比对旋转方向
    if (angles is { Length: >= 3 })
    {
        using var manager = new CalibrationManager();
        manager.LoadDirectory(outDir);
        manager.VerifyRotationDirection(stationId, profile, [.. points], angles);
        Console.WriteLine("方向自检通过: 第 4 轴正方向与图像旋转方向一致");
    }

    Directory.CreateDirectory(outDir);
    var path = Path.Combine(outDir, $"{stationId}.rotation.json");
    AtomicFile.WriteAllText(path,
        JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));

    Console.WriteLine($"轴心像素坐标: ({profile.Cx:0.00}, {profile.Cy:0.00})，半径 {profile.RadiusPx:0.00} px");
    Console.WriteLine($"半径残差 RMS: {profile.Rms:0.000} px（参考: ≤0.3 优秀, ≤0.5 可用）");
    if (profile.PointCount >= 5)
        Console.WriteLine($"椭圆长短轴比: {profile.AxisRatio:0.000}（1=正圆，偏离大说明标记提取不稳或机械抖动）");
    Console.WriteLine($"已保存: {path}");

    if (profile.Rms > 0.5)
        Console.WriteLine("警告: 半径残差偏大，建议增加角度数量并检查标记提取稳定性");
    if (profile.PointCount >= 5 && profile.AxisRatio > 1.2)
        Console.WriteLine("警告: 长短轴比偏离 1，标记轨迹不是正圆，请检查标记提取或机械间隙");
    if (profile.PointCount is 3 or 4)
        Console.WriteLine("提示: 3~4 个点无椭圆质检能力，建议 5~9 个角度");
}

/// <summary>多项式标定（单图模式）：一张棋盘格图 + 2 个同行参考角点（csv 两行）。
/// 替代"内参+外参"两步，适合小畸变/单平面/统一高度场景。该工位推理直接用原图。</summary>
static void RunPolynomial(Dictionary<string, string> options)
{
    var cameraId = Require(options, "camera");
    var stationId = Require(options, "station");
    var imageFile = Require(options, "image");
    var cols = int.Parse(Require(options, "cols"), CultureInfo.InvariantCulture);
    var rows = int.Parse(Require(options, "rows"), CultureInfo.InvariantCulture);
    var square = double.Parse(Require(options, "square"), CultureInfo.InvariantCulture);
    var order = int.Parse(options.GetValueOrDefault("order", "2"), CultureInfo.InvariantCulture);
    var outDir = ResolveOutDir(options.GetValueOrDefault("out", "data/calibration"));

    // 坐标空间：image = 棋盘毫米系（免示教，无需 --ref）；robot = 机器人系（需 --ref 2 参考点）
    var space = options.GetValueOrDefault("space", "Robot");
    if (!RobotVision.Core.Models.PolynomialCoordinateSpace.IsValid(space))
        throw new FormatException($"--space 非法: {space}（仅支持 Robot / Image）");
    var imageSpace = string.Equals(space, "Image", StringComparison.OrdinalIgnoreCase);

    // 安装模式/位姿（与外参命令同参）
    var mount = options.GetValueOrDefault("mount", "Fixed");
    if (!RobotVision.Core.Models.CameraMountType.IsValid(mount))
        throw new FormatException($"--mount 非法: {mount}（仅支持 Fixed / OnArm）");
    var compose = options.GetValueOrDefault("compose", "Check");
    if (!RobotVision.Core.Models.PoseComposeMode.IsValid(compose))
        throw new FormatException($"--compose 非法: {compose}（仅支持 Check / Translate）");
    var mountUpper = mount.ToUpperInvariant();
    var teachX = double.Parse(options.GetValueOrDefault("tcp-x", "0"), CultureInfo.InvariantCulture);
    var teachY = double.Parse(options.GetValueOrDefault("tcp-y", "0"), CultureInfo.InvariantCulture);
    var teachRz = double.Parse(options.GetValueOrDefault("rz", "0"), CultureInfo.InvariantCulture);
    var planeZ = double.Parse(options.GetValueOrDefault("plane-z", "0"), CultureInfo.InvariantCulture);

    if (cols < 3 || rows < 3)
        throw new FormatException("--cols/--rows 至少 3（多项式拟合需要足够网格点）");

    // 参考点 csv（仅机器人系需要）：两行 "像素x,像素y,机器人x,机器人y"
    Point2f[] refPixels = [];
    Point2f[] refRobots = [];
    if (!imageSpace)
    {
        var refFile = Require(options, "ref");
        (refPixels, refRobots) = CsvPointParser.ParsePairs(File.ReadAllLines(refFile));
        if (refPixels.Length != 2)
            throw new FormatException($"--ref 需要 2 行参考点（像素x,像素y,机器人x,机器人y），当前 {refPixels.Length} 行");
    }

    Console.WriteLine($"多项式标定 | 相机 {cameraId} | 工位 {stationId} | 棋盘 {cols}x{rows} | {square}mm | {order} 阶 | 坐标 {(imageSpace ? "棋盘毫米系(免示教)" : "机器人系")} | 安装 {mountUpper}");

    // 读图 + 角点检测（imdecode 兼容中文路径；多项式不依赖内参）
    using var img = Cv2.ImDecode(File.ReadAllBytes(imageFile), ImreadModes.Color);
    if (img.Empty())
        throw new VisionException(RobotVision.Core.Models.VisionErrorCode.InternalError, $"图像读取失败: {imageFile}");
    using var gray = new Mat();
    Cv2.CvtColor(img, gray, ColorConversionCodes.BGR2GRAY);
    if (!Cv2.FindChessboardCornersSB(gray, new Size(cols, rows), out var corners))
        throw new VisionException(RobotVision.Core.Models.VisionErrorCode.InternalError,
            $"未检测到棋盘（{cols}x{rows}）：请核对内角点数与图像");

    var teachPoseProvided = options.ContainsKey("tcp-x") && options.ContainsKey("tcp-y") && options.ContainsKey("rz");
    var calibrated = imageSpace
        ? PolynomialCalibrator.CalibrateImageSpace(
            stationId, cameraId, corners, new Size(cols, rows), square, img.Width, img.Height, order)
        : PolynomialCalibrator.Calibrate(
            stationId, cameraId, corners, new Size(cols, rows), square,
            refPixels[0], refRobots[0], refPixels[1], refRobots[1],
            img.Width, img.Height, order);
    // Image 毫米系无机器人系概念：安装模式/位姿固定为 Fixed/Check/不记录
    var profile = calibrated with
    {
        MountType = imageSpace || mountUpper != "ONARM"
            ? RobotVision.Core.Models.CameraMountType.Fixed
            : RobotVision.Core.Models.CameraMountType.OnArm,
        ComposeMode = !imageSpace && mountUpper == "ONARM" && compose.ToUpperInvariant() == "TRANSLATE"
            ? RobotVision.Core.Models.PoseComposeMode.Translate
            : RobotVision.Core.Models.PoseComposeMode.Check,
        TeachTcpX = imageSpace ? 0 : teachX,
        TeachTcpY = imageSpace ? 0 : teachY,
        TeachRzDeg = imageSpace ? 0 : teachRz,
        HasTeachPose = !imageSpace && mountUpper == "ONARM" && teachPoseProvided,
        CalibrationPlaneZ = planeZ,
    };

    Console.WriteLine($"拟合 {profile.PointCount} 网格点 · {profile.CoefficientCount} 系数/轴 · RMS {profile.Rms:0.0000} · 最大 {profile.MaxResidual:0.0000}（mm）");
    if (profile.MaxResidual > 0.5)
        Console.WriteLine("警告: 残差偏大，请重拍（棋盘放平、正对镜头）或核对参数");

    Directory.CreateDirectory(outDir);
    var polyPath = Path.Combine(outDir, $"{stationId}.polynomial.json");
    AtomicFile.WriteAllText(polyPath,
        JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"已保存: {polyPath}（该工位走单图模式：推理直接用原图，无需内参/外参档案）");
    if (imageSpace)
        Console.WriteLine("坐标语义: 棋盘平面毫米系（原点=首角点，轴=棋盘行列）——适合上位机自行换算");
    else if (mountUpper == "ONARM")
        Console.WriteLine(teachPoseProvided
            ? $"OnArm {profile.ComposeMode}: 拍照位姿 {teachX:0.000}/{teachY:0.000} RZ {teachRz:0.0}° 已记录"
            : "提示: OnArm 未提供 --tcp-x/--tcp-y/--rz，档案不记录拍照位姿（位姿校验/合成不可用）");
}

static void RunTemplate(Dictionary<string, string> options)
{
    var kind = options.GetValueOrDefault("kind", "extrinsic").ToLowerInvariant();
    var outFile = options.GetValueOrDefault("out",
        kind == "rotation" ? "points.csv" : "pairs.csv");

    var lines = kind switch
    {
        "extrinsic" => new[]
        {
            "# 九点外参标定模板：像素x,像素y,机器人x,机器人y",
            "# 依次示教 9 个点，把示教器上的机器人坐标填入后两列",
            "pixel_x,pixel_y,robot_x,robot_y",
            "100.0,100.0,0.0,0.0",
            "600.0,100.0,200.0,0.0",
            "1100.0,100.0,400.0,0.0",
            "100.0,400.0,0.0,150.0",
            "600.0,400.0,200.0,150.0",
            "1100.0,400.0,400.0,150.0",
            "100.0,700.0,0.0,300.0",
            "600.0,700.0,200.0,300.0",
            "1100.0,700.0,400.0,300.0",
        },
        "rotation" => new[]
        {
            "# 旋转中心标定模板：像素x,像素y[,第4轴角]",
            "# 第 4 轴每转一个角度取一次去畸变图并记录标记点，建议 5~9 个等间隔角度",
            "# 第三列可选：当时的第 4 轴角度（填 ≥3 行，且该工位已有外参档案时自动做旋转方向自检）",
            "pixel_x,pixel_y,rz_deg",
            "600.0,350.0,0",
            "750.0,420.0,45",
            "680.0,540.0,90",
            "520.0,540.0,135",
            "450.0,420.0,180",
        },
        _ => throw new VisionException(RobotVision.Core.Models.VisionErrorCode.InternalError,
            $"未知模板类型: {kind}（可选 extrinsic / rotation）"),
    };

    File.WriteAllLines(outFile, lines);
    Console.WriteLine($"已生成模板: {outFile}");
}

/// <summary>外参/旋转中心标定前置硬校验：必须有内参档案（与 CalibrationManager 一致，缺失直接失败）。</summary>
static IntrinsicProfile RequireIntrinsic(string cameraId, string outDir)
{
    var path = Path.Combine(outDir, $"{cameraId}.intrinsic.json");
    if (!File.Exists(path))
        throw new VisionException(RobotVision.Core.Models.VisionErrorCode.InternalError,
            $"未找到内参档案 {path}——外参/旋转中心标定必须基于去畸变图像，请先完成内参标定");
    var profile = JsonSerializer.Deserialize<IntrinsicProfile>(File.ReadAllText(path))
        ?? throw new VisionException(RobotVision.Core.Models.VisionErrorCode.InternalError,
            $"内参档案损坏: {path}");
    Console.WriteLine($"内参档案: {profile.Width}x{profile.Height}（{profile.CalibratedAt:yyyy-MM-dd} 标定）");
    return profile;
}

/// <summary>
/// 输出目录解析：与 App 的目录解析规则保持一致（exe 目录优先，工作目录回退），
/// 保证标定产物落到 App 实际加载的目录，而不是随启动 CWD 漂移。
/// </summary>
static string ResolveOutDir(string path)
{
    if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
        return path;

    var exeAnchored = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
    if (Directory.Exists(exeAnchored))
        return exeAnchored;

    var cwdAnchored = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, path));
    return Directory.Exists(cwdAnchored) ? cwdAnchored : exeAnchored;
}

static Dictionary<string, string> ParseOptions(IEnumerable<string> args)
{
    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var list = args.ToList();
    for (var i = 0; i + 1 < list.Count; i += 2)
        dict[list[i].TrimStart('-')] = list[i + 1];
    return dict;
}

static string Require(Dictionary<string, string> options, string key) =>
    options.TryGetValue(key, out var value)
        ? value
        : throw new VisionException(RobotVision.Core.Models.VisionErrorCode.InternalError, $"缺少参数 --{key}");

static void PrintUsage()
{
    Console.WriteLine("""
        用法:
          CalibTool intrinsic --camera <相机Id> --folder <棋盘图片目录> --cols <内角点列数> --rows <内角点行数> --square <方格边长mm> [--out <输出目录>]
          CalibTool extrinsic --camera <相机Id> --station <工位Id> --file <点对csv> [--out <输出目录>]
              点对csv 每行: 像素x,像素y,机器人x,机器人y（# 注释，首行可放表头，推荐 9 点）
          CalibTool rotation --camera <相机Id> --station <工位Id> --file <标记点csv> [--out <输出目录>]
              标记点csv 每行: 像素x,像素y（第 4 轴多角度下标记点坐标，取自去畸变图像，推荐 5~9 个等间隔角度）
          CalibTool polynomial --camera <相机Id> --station <工位Id> --image <棋盘单图> --ref <参考点csv>
                                --cols <内角点列数> --rows <内角点行数> --square <方格边长mm>
                                [--order 2|3] [--mount fixed|onarm] [--compose check|translate]
                                [--tcp-x <x> --tcp-y <y> --rz <deg>] [--plane-z <z>] [--out <输出目录>]
              单图模式：一个多项式替代"内参+外参"，推理直接用原图。适用小畸变/单平面/统一高度。
              refcsv 两行: 像素x,像素y,机器人x,机器人y（同一行的 2 个参考角点，机器人带针对准示教）
          CalibTool template --kind extrinsic|rotation [--out <文件>]
              生成示例 CSV 模板（默认 pairs.csv / points.csv）
        """);
}
