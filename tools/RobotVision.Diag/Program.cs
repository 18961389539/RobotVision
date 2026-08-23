// RobotVision.Diag — 管线复刻诊断工具（一次性排查/对照回归）
// 用途：把 App 的「FileCamera 取图 → 去畸变 → Mat→SKBitmap → 推理」与
// 「SKBitmap.Decode 直读」做像素级对照，定位推理输入不一致问题。
// 用法：
//   RobotVision.Diag [--model <onnx路径>] [--replay <回放目录>] [--image <图片路径>]
// 退出码：0 = 对照一致（像素偏差在容差内）；1 = 对照不一致或执行失败（供 CI/脚本判断）。
using Microsoft.ML.OnnxRuntime;
using YoloDotNet;
using YoloDotNet.ExecutionProvider.Cpu;
using YoloDotNet.Models;
using SkiaSharp;
using OpenCvSharp;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Inference;

var argsList = args.ToList();
string Arg(string key, string fallback)
{
    var i = argsList.IndexOf(key);
    return i >= 0 && i + 1 < argsList.Count ? argsList[i + 1] : fallback;
}

var modelPath = Arg("--model", @"D:\Code\RobotVision\models\a01_kpt.onnx");
var replayDir = Arg("--replay", @"D:\Code\RobotVision\data\replay");
var imagePath = Arg("--image", Path.Combine(replayDir, "people.jpg"));

const int PixelTolerance = 10; // 每通道允许偏差（0~255）

var failures = 0;
void Check(bool ok, string what)
{
    if (ok)
        Console.WriteLine($"  通过: {what}");
    else
    {
        failures++;
        Console.WriteLine($"  失败: {what}");
    }
}

Console.WriteLine("== 复刻 App 管线 ==");

// 1. FileCamera 取图
var camera = new FileCamera("cam_file", replayDir);
using var frame = camera.Grab();
var image = frame.Image; // CameraFrame.Dispose 已释放 Image，这里不再 using 避免对同一 Mat 双释放
Console.WriteLine($"FileCamera: {image.Width}x{image.Height} channels={image.Channels()} type={image.Type()}");

// 2. 内参去畸变（读取与 App 相同的标定档案）
var calib = new CalibrationManager();
calib.LoadDirectory(@"D:\Code\RobotVision\data\calibration");
using var undistorted = calib.Undistort("cam_file", image);
Console.WriteLine($"Undistort: {undistorted.Width}x{undistorted.Height} channels={undistorted.Channels()}");

// 3. Mat → SKBitmap
using var bitmap = MatSkiaConverter.ToSKBitmap(undistorted);
Console.WriteLine($"SKBitmap: {bitmap.Width}x{bitmap.Height} colorType={bitmap.ColorType} alpha={bitmap.AlphaType}");

// 4. 推理
using var yolo = new Yolo(new YoloOptions { ExecutionProvider = new CpuExecutionProvider(modelPath) });

var results = yolo.RunPoseEstimation(bitmap, 0.5, 0.7);
Console.WriteLine($"复刻管线 conf=0.5: 检出 {results.Count} 个目标");
foreach (var r in results.Take(3))
{
    Console.WriteLine($"  label={r.Label?.Name} conf={r.Confidence:F3} kpts={r.KeyPoints?.Length ?? -1}");
    if (r.KeyPoints is { Length: > 2 })
    {
        Console.WriteLine($"    kpt0 ({r.KeyPoints[0].X},{r.KeyPoints[0].Y}) conf={r.KeyPoints[0].Confidence:F3}");
        Console.WriteLine($"    kpt1 ({r.KeyPoints[1].X},{r.KeyPoints[1].Y}) conf={r.KeyPoints[1].Confidence:F3}");
    }
}

// 5. 对照：SKBitmap.Decode 直读（管线输入应与直读像素一致）
Console.WriteLine("\n== 像素对照（管线 vs 直读） ==");
using var decoded = SKBitmap.Decode(imagePath);
Console.WriteLine($"SKBitmap.Decode: {decoded.Width}x{decoded.Height} colorType={decoded.ColorType} alpha={decoded.AlphaType}");
Check(bitmap.Width == decoded.Width && bitmap.Height == decoded.Height, $"尺寸一致（{bitmap.Width}x{bitmap.Height}）");

foreach (var (x, y) in new[] { (100, 100), (68, 541), (640, 426), (300, 200) })
{
    if (x < bitmap.Width && y < bitmap.Height && x < decoded.Width && y < decoded.Height)
    {
        var a = bitmap.GetPixel(x, y);
        var b = decoded.GetPixel(x, y);
        var maxDelta = Math.Max(Math.Abs(a.Red - b.Red), Math.Max(Math.Abs(a.Green - b.Green), Math.Abs(a.Blue - b.Blue)));
        Console.WriteLine($"  ({x},{y}): 管线=({a.Red},{a.Green},{a.Blue}) 直读=({b.Red},{b.Green},{b.Blue}) Δmax={maxDelta}");
        Check(maxDelta <= PixelTolerance, $"采样点 ({x},{y}) 像素偏差 ≤{PixelTolerance}");
    }
}

// 6. 推理结果对照：同一图像两种路径检出数应一致
var results2 = yolo.RunPoseEstimation(decoded, 0.5, 0.7);
Console.WriteLine($"直读 conf=0.5: 检出 {results2.Count} 个目标");
Check(results.Count == results2.Count, $"推理检出数一致（{results.Count} vs {results2.Count}）");

Console.WriteLine(failures == 0
    ? "\n== 诊断结论: 全部通过（管线输入与直读一致） =="
    : $"\n== 诊断结论: {failures} 项不一致（详见上方） ==");
return failures == 0 ? 0 : 1;
