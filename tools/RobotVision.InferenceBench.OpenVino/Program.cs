using System.Diagnostics;
using System.Globalization;
using OpenCvSharp;
using RobotVision.InferenceBench;
using RobotVision.Infrastructure.Inference;
using SkiaSharp;
using YoloDotNet;
using YoloDotNet.Enums;
using YoloDotNet.ExecutionProvider.OpenVino;
using YoloDotNet.Models;

namespace RobotVision.InferenceBench.OpenVinoWorker;

/// <summary>
/// OpenVINO 专用进程：不得引用 YoloDotNet.ExecutionProvider.Cpu / Infrastructure，
/// 否则会和 CPU 编排进程抢同一份 onnxruntime.dll。
/// stdout 只走协议（READY/DONE）或保持安静；报告写 --out JSON。诊断走 stderr。
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        try
        {
            var cli = CliArgs.Parse(args);
            if (cli.Help)
            {
                PrintHelp();
                return 0;
            }

            var opt = BenchOptions.From(cli);
            if (opt.Models.Count == 0 || string.IsNullOrWhiteSpace(opt.Image))
            {
                PrintHelp();
                return 1;
            }

            foreach (var model in opt.Models)
            {
                if (!File.Exists(model))
                    throw new FileNotFoundException($"模型不存在: {model}");
            }

            var frames = BenchImage.LoadAll(opt.Image, opt.Roi);
            var cacheDir = Path.Combine(Path.GetTempPath(), "RobotVision.InferenceBench.ovcache");
            Directory.CreateDirectory(cacheDir);

            var sessions = opt.Models.Select(m => CreateSession(m, opt.OvDevice, opt.OvPrecision, cacheDir)).ToList();
            try
            {
                var task = ResolveTask(sessions[0].Yolo, opt.Task);
                var cursorA = 0;
                Mat NextA() => frames[(Interlocked.Increment(ref cursorA) - 1) % frames.Count].Inference;

                Action pipeline = () =>
                {
                    var mat = NextA();
                    foreach (var s in sessions)
                        s.Run(mat, task, opt);
                };

                var fingerprint = sessions[0].Snapshot(frames[0].Inference, task, opt);
                var detectCount = fingerprint.Count;

                var report = new WorkerReport
                {
                    Ok = true,
                    Provider = "OpenVINO",
                    Device = opt.OvDevice,
                    Task = BenchOptions.TaskLabel(task),
                    ImageWidth = frames[0].Full.Width,
                    ImageHeight = frames[0].Full.Height,
                    CropWidth = frames[0].Inference.Width,
                    CropHeight = frames[0].Inference.Height,
                    DetectCount = detectCount,
                    Fingerprint = fingerprint.ToArray(),
                };

                if (opt.Mode == WorkerMode.Gated)
                {
                    RunGated(pipeline, opt.Warmup);
                    return 0;
                }

                switch (opt.Mode)
                {
                    case WorkerMode.Single:
                        report.TimesMs = TimedRun.Single(pipeline, opt.Warmup, opt.Iters);
                        break;
                    case WorkerMode.Serial2:
                    {
                        var samples = TimedRun.SerialTwo(pipeline, opt.Warmup, opt.Pairs);
                        FillOverlap(report, samples);
                        break;
                    }
                    case WorkerMode.Parallel2:
                    {
                        var extra = opt.Models.Select(m => CreateSession(m, opt.OvDevice, opt.OvPrecision, cacheDir)).ToList();
                        try
                        {
                            var cursorB = 0;
                            Mat NextB() => frames[(Interlocked.Increment(ref cursorB) - 1) % frames.Count].Inference;
                            Action pipeB = () =>
                            {
                                var mat = NextB();
                                foreach (var s in extra)
                                    s.Run(mat, task, opt);
                            };

                            var samples = TimedRun.ParallelTwo(pipeline, pipeB, opt.Warmup, opt.Pairs);
                            FillOverlap(report, samples);
                        }
                        finally
                        {
                            foreach (var s in extra)
                                s.Dispose();
                        }

                        break;
                    }
                }

                if (!string.IsNullOrWhiteSpace(opt.OutFile))
                    ReportJson.Write(opt.OutFile, report);
                else
                    Console.Error.WriteLine(System.Text.Json.JsonSerializer.Serialize(report, ReportJson.Options));

                return 0;
            }
            finally
            {
                foreach (var s in sessions)
                    s.Dispose();
                foreach (var f in frames)
                    f.Dispose();
            }
        }
        catch (Exception ex)
        {
            var report = new WorkerReport { Ok = false, Error = ex.Message, Provider = "OpenVINO" };
            var outFile = CliArgs.Parse(args).Get("--out");
            if (!string.IsNullOrWhiteSpace(outFile))
            {
                try { ReportJson.Write(outFile, report); }
                catch { /* 仍走 stderr */ }
            }

            Console.Error.WriteLine(ex.ToString());
            return 2;
        }
    }

    private static void RunGated(Action pipeline, int warmup)
    {
        for (var i = 0; i < warmup; i++)
            pipeline();

        Console.Out.WriteLine("READY");
        Console.Out.Flush();
        while (true)
        {
            var line = Console.In.ReadLine();
            if (line is null || line.Equals("QUIT", StringComparison.OrdinalIgnoreCase))
                break;
            if (!line.Equals("GO", StringComparison.OrdinalIgnoreCase))
                continue;

            var sw = Stopwatch.StartNew();
            pipeline();
            Console.Out.WriteLine(string.Create(CultureInfo.InvariantCulture, $"DONE {sw.Elapsed.TotalMilliseconds:G17}"));
            Console.Out.Flush();
        }
    }

    private static void FillOverlap(WorkerReport report, OverlapSample[] samples)
    {
        report.MakespanMs = samples.Select(s => s.MakespanMs).ToArray();
        report.WaitAMs = samples.Select(s => s.WaitAMs).ToArray();
        report.WaitBMs = samples.Select(s => s.WaitBMs).ToArray();
    }

    private static Session CreateSession(string model, string device, string precision, string cacheDir)
    {
        var ov = new OpenVino
        {
            DeviceType = device,
            Precision = ParsePrecision(precision, device),
            CachePath = cacheDir,
            ModelPriority = ModelPriority.HIGH,
        };

        var yolo = new Yolo(new YoloOptions
        {
            ExecutionProvider = new OpenVinoExecutionProvider(model, ov),
        });
        return new Session(yolo);
    }

    private static Precision ParsePrecision(string text, string device)
    {
        if (text.Equals("FP16", StringComparison.OrdinalIgnoreCase))
            return Precision.FP16;
        if (text.Equals("FP32", StringComparison.OrdinalIgnoreCase))
            return Precision.FP32;
        return device.StartsWith("GPU", StringComparison.OrdinalIgnoreCase)
            || device.StartsWith("NPU", StringComparison.OrdinalIgnoreCase)
            ? Precision.FP16
            : Precision.FP32;
    }

    private static BenchTask ResolveTask(Yolo yolo, BenchTask requested)
    {
        var detected = Map(yolo.OnnxModel.ModelType);
        if (requested == BenchTask.Auto)
        {
            if (detected is null)
                throw new InvalidOperationException("无法从 ONNX 识别任务，请显式传 --task det|seg|pose");
            return detected.Value;
        }

        if (detected is not null && detected != requested)
        {
            throw new InvalidOperationException(
                $"模型任务为 {BenchOptions.TaskLabel(detected.Value)}，与 --task {BenchOptions.TaskLabel(requested)} 不一致");
        }

        return requested;
    }

    private static BenchTask? Map(ModelType modelType)
    {
        var name = modelType.ToString();
        if (name.Contains("Segment", StringComparison.OrdinalIgnoreCase))
            return BenchTask.Segmentation;
        if (name.Contains("Pose", StringComparison.OrdinalIgnoreCase))
            return BenchTask.PoseEstimation;
        if (name.Contains("Obb", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Classification", StringComparison.OrdinalIgnoreCase))
            return null;
        if (name.Contains("Detection", StringComparison.OrdinalIgnoreCase))
            return BenchTask.ObjectDetection;
        return null;
    }

    private static void PrintHelp()
    {
        Console.Error.WriteLine("""
            RobotVision.InferenceBench.OpenVino — 由编排进程启动，不要单独当产线用。
              --model <onnx> [--model2 <onnx>] --image <图>
              --mode single|serial2|parallel2|gated
              --ov-device CPU|GPU|NPU|AUTO  --ov-precision FP16|FP32
              --task auto|det|seg|pose  --roi x,y,w,h  --out report.json
            """);
    }

    private sealed class Session(Yolo yolo) : IDisposable
    {
        public Yolo Yolo { get; } = yolo;

        public void Run(Mat mat, BenchTask task, BenchOptions opt)
        {
            using var bitmap = MatSkiaConverter.ToSKBitmap(mat);
            _ = RunCore(bitmap, task, opt);
        }

        public List<BoxScore> Snapshot(Mat mat, BenchTask task, BenchOptions opt)
        {
            using var bitmap = MatSkiaConverter.ToSKBitmap(mat);
            return RunCore(bitmap, task, opt);
        }

        private List<BoxScore> RunCore(SKBitmap bitmap, BenchTask task, BenchOptions opt)
        {
            switch (task)
            {
                case BenchTask.ObjectDetection:
                    return Yolo.RunObjectDetection(bitmap, opt.Confidence, opt.Iou)
                        .OrderByDescending(d => d.Confidence)
                        .Take(5)
                        .Select(d => new BoxScore
                        {
                            Label = d.Label.Name,
                            Score = d.Confidence,
                            X = d.BoundingBox.Left,
                            Y = d.BoundingBox.Top,
                            W = d.BoundingBox.Width,
                            H = d.BoundingBox.Height,
                        })
                        .ToList();
                case BenchTask.Segmentation:
                    return Yolo.RunSegmentation(bitmap, opt.Confidence, opt.PixelConfidence, opt.Iou)
                        .OrderByDescending(s => s.Confidence)
                        .Take(5)
                        .Select(s => new BoxScore
                        {
                            Label = s.Label.Name,
                            Score = s.Confidence,
                            X = s.BoundingBox.Left,
                            Y = s.BoundingBox.Top,
                            W = s.BoundingBox.Width,
                            H = s.BoundingBox.Height,
                        })
                        .ToList();
                case BenchTask.PoseEstimation:
                    return Yolo.RunPoseEstimation(bitmap, opt.Confidence, opt.Iou)
                        .OrderByDescending(p => p.Confidence)
                        .Take(5)
                        .Select(p => new BoxScore
                        {
                            Label = p.Label.Name,
                            Score = p.Confidence,
                            X = p.BoundingBox.Left,
                            Y = p.BoundingBox.Top,
                            W = p.BoundingBox.Width,
                            H = p.BoundingBox.Height,
                        })
                        .ToList();
                default:
                    throw new InvalidOperationException("未解析任务");
            }
        }

        public void Dispose() => Yolo.Dispose();
    }
}
