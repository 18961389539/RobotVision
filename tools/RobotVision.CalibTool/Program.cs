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
//   推荐 5~9 个角度等间隔分布（如每 45°）；像素坐标须取自去畸变后的图像
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
    File.WriteAllText(path, JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));

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

    RequireIntrinsic(cameraId, outDir);

    var (pixelPoints, robotPoints) = CsvPointParser.ParsePairs(File.ReadAllLines(file));

    Console.WriteLine($"外参标定 | 相机 {cameraId} | 工位 {stationId} | 点数 {pixelPoints.Length}");

    var profile = NinePointExtrinsicCalibrator.Calibrate(stationId, cameraId, [.. pixelPoints], [.. robotPoints]);

    Directory.CreateDirectory(outDir);
    var path = Path.Combine(outDir, $"{stationId}.extrinsic.json");
    File.WriteAllText(path, JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));

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

    RequireIntrinsic(cameraId, outDir);

    var points = CsvPointParser.ParsePoints(File.ReadAllLines(file));

    Console.WriteLine($"旋转中心标定 | 相机 {cameraId} | 工位 {stationId} | 点数 {points.Length}");

    var profile = RotationCenterCalibrator.Calibrate(stationId, cameraId, [.. points]);

    Directory.CreateDirectory(outDir);
    var path = Path.Combine(outDir, $"{stationId}.rotation.json");
    File.WriteAllText(path, JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));

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
            "# 旋转中心标定模板：像素x,像素y",
            "# 第 4 轴每转一个角度取一次去畸变图并记录标记点，建议 5~9 个等间隔角度",
            "pixel_x,pixel_y",
            "600.0,350.0",
            "750.0,420.0",
            "680.0,540.0",
            "520.0,540.0",
            "450.0,420.0",
        },
        _ => throw new VisionException(RobotVision.Core.Models.VisionErrorCode.InternalError,
            $"未知模板类型: {kind}（可选 extrinsic / rotation）"),
    };

    File.WriteAllLines(outFile, lines);
    Console.WriteLine($"已生成模板: {outFile}");
}

/// <summary>外参/旋转中心标定前置硬校验：必须有内参档案（与 CalibrationManager 一致，缺失直接失败）。</summary>
static void RequireIntrinsic(string cameraId, string outDir)
{
    var path = Path.Combine(outDir, $"{cameraId}.intrinsic.json");
    if (!File.Exists(path))
        throw new VisionException(RobotVision.Core.Models.VisionErrorCode.InternalError,
            $"未找到内参档案 {path}——外参/旋转中心标定必须基于去畸变图像，请先完成内参标定");
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

/// <summary>行内是否全部为可解析的有限数字（表头/说明行检测；NaN/Infinity 视为非法）。</summary>
static bool IsNumericLine(string line) =>
    line.Split(',').Select(p => p.Trim())
        .All(p => TryParseFinite(p, out _));

/// <summary>CSV 数值解析：可解析且为有限实数（NaN/Infinity 会污染标定结果，一律拒绝）。</summary>
static bool TryParseFinite(string text, out double value) =>
    double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

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
          CalibTool template --kind extrinsic|rotation [--out <文件>]
              生成示例 CSV 模板（默认 pairs.csv / points.csv）
        """);
}
