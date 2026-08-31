using RobotVision.Infrastructure;

namespace RobotVision.InferenceBench;

internal static class Program
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var cli = CliArgs.Parse(args);
        if (cli.Help || args.Length == 0)
        {
            PrintHelp();
            return args.Length == 0 ? 1 : 0;
        }

        try
        {
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

            if (BenchImage.ListImageFiles(opt.Image).Count == 0)
                throw new FileNotFoundException($"图片不存在: {opt.Image}");

            Console.WriteLine("RobotVision 推理 EP 对比（进程内 OpenVINO CPU vs ov-worker OpenVINO）");
            Console.WriteLine($"模型: {string.Join(" + ", opt.Models.Select(Path.GetFileName))}");
            Console.WriteLine($"图片: {opt.Image}");
            if (opt.Roi is not null)
                Console.WriteLine($"ROI: ({opt.Roi.X:G4},{opt.Roi.Y:G4}) {opt.Roi.Width:G4}×{opt.Roi.Height:G4}（相对）");
            Console.WriteLine($"预热 {opt.Warmup} / 单枪 {opt.Iters} 次 / 两枪 {opt.Pairs} 对；任务 {BenchOptions.TaskLabel(opt.Task)}");
            Console.WriteLine("产线与本工具进程内路径都是 OpenVINO EP；ov-worker 用来对照 AUTO:CPU / GPU，避免和主进程抢同一套 native。");
            Console.WriteLine();

            var frames = BenchImage.LoadAll(opt.Image, opt.Roi);
            var visions = frames.Select(f => VisionImageCv.FromMat(f.Inference, ownsMat: false)).ToList();
            try
            {
            var sizes = frames
                .Select(f => $"{f.Inference.Width}×{f.Inference.Height}")
                .Distinct()
                .ToArray();
            Console.WriteLine($"共 {frames.Count} 张，轮流推理；尺寸: {string.Join(", ", sizes)} px");
            Console.WriteLine();

            using var cpu = CpuPipeline.Create(opt.Models, visions, opt);
            var resolved = opt with { Task = cpu.Task };

            var phaseA = new List<EpSample>();
            OverlapSummary? serialWinner = null;
            OverlapSummary? parallelWinner = null;
            OverlapSummary? mixed = null;
            var notes = new List<string>();

            var runB = resolved.Phase is not "a";

            Console.WriteLine("=== Phase A  单枪（预热不计） ===");
            Console.WriteLine("加载进程内 OpenVINO CPU 会话…");
            var cpuTimes = TimedRun.RunOne(cpu.Infer, resolved.Warmup, resolved.Iters);
            var cpuSample = new EpSample
            {
                Name = BenchDecide.InProcOpenVinoCpu,
                Latency = Stats.Summarize(cpuTimes),
                DetectCount = cpu.DetectCount,
                Fingerprint = cpu.Fingerprint.ToArray(),
            };
            phaseA.Add(cpuSample);
            PrintLatency(cpuSample);

            if (!resolved.SkipOpenVino)
            {
                var workerA = OpenVinoClient.FindWorker(resolved.OvWorkerDir);
                if (workerA is null)
                {
                    notes.Add("未找到 ov-worker（RobotVision.InferenceBench.OpenVino.exe）。已跳过 OpenVINO。请 Release 编译本工具。");
                    Console.WriteLine(notes[^1]);
                }
                else
                {
                    foreach (var (device, display) in new[] { ("AUTO:CPU", "OpenVINO CPU"), ("GPU", "OpenVINO GPU") })
                    {
                        Console.WriteLine($"加载 {display}（独立进程，device={device}）…");
                        var report = OpenVinoClient.Run(workerA, resolved, device, WorkerMode.One);
                        var name = display;
                        if (!report.Ok && device == "AUTO:CPU")
                        {
                            Console.WriteLine($"  AUTO:CPU 失败，回退 YoloDotNet 默认 CPU 会话: {report.Error}");
                            report = OpenVinoClient.Run(workerA, resolved, "CPU", WorkerMode.One);
                            name = "OpenVINO CPU";
                        }

                        if (!report.Ok)
                        {
                            Console.WriteLine($"  跳过 {name}: {report.Error}");
                            phaseA.Add(new EpSample
                            {
                                Name = name,
                                Latency = default,
                                Error = report.Error,
                            });
                            continue;
                        }

                        var sample = ToEpSample(report, name);
                        phaseA.Add(sample);
                        PrintLatency(sample);
                        PrintFingerprintDelta(cpuSample, sample);
                    }
                }
            }

            Console.WriteLine();

            var winnerName = phaseA
                .Where(s => s.Error is null && !double.IsNaN(s.Latency.P50))
                .OrderBy(s => s.Latency.P50)
                .FirstOrDefault()?.Name ?? BenchDecide.InProcOpenVinoCpu;

            if (runB)
            {
                Console.WriteLine($"=== Phase B  两枪重叠（赢家 {winnerName}） ===");
                var worker = resolved.SkipOpenVino ? null : OpenVinoClient.FindWorker(resolved.OvWorkerDir);
                var winnerIsCpu = BenchDecide.IsInProcCpuName(winnerName);
                var winnerDevice = ToWorkerDevice(phaseA, winnerName);

                if (winnerIsCpu)
                {
                    serialWinner = RunCpuSerial(cpu, resolved);
                    PrintOverlap(serialWinner);
                    using var cpuB = cpu.ClonePair(resolved.Models, visions, resolved);
                    parallelWinner = ToOverlap("OpenVINO CPU 双会话并行", TimedRun.ParallelTwo(cpu.Infer, cpuB.Infer, resolved.Warmup, resolved.Pairs));
                    PrintOverlap(parallelWinner);
                }
                else if (worker is not null)
                {
                    Console.WriteLine($"{winnerName} 串行两枪…");
                    var serial = OpenVinoClient.Run(worker, resolved, winnerDevice, WorkerMode.Serial2);
                    if (serial.Ok)
                    {
                        serialWinner = ToOverlap($"{winnerName} 串行", serial);
                        PrintOverlap(serialWinner);
                    }
                    else
                    {
                        Console.WriteLine($"  失败: {serial.Error}");
                    }

                    Console.WriteLine($"{winnerName} 双会话并行…");
                    var par = OpenVinoClient.Run(worker, resolved, winnerDevice, WorkerMode.Parallel2);
                    if (par.Ok)
                    {
                        parallelWinner = ToOverlap($"{winnerName} 双会话并行", par);
                        PrintOverlap(parallelWinner);
                    }
                    else
                    {
                        Console.WriteLine($"  失败: {par.Error}");
                    }
                }

                var ovAlive = phaseA.Any(s => s.Name.StartsWith("OpenVINO ", StringComparison.OrdinalIgnoreCase) && s.Error is null);
                if (worker is not null && ovAlive)
                {
                    var mixedPeer = phaseA
                        .Where(s => s.Name.StartsWith("OpenVINO ", StringComparison.OrdinalIgnoreCase) && s.Error is null)
                        .MinBy(s => s.Latency.P50)!;
                    var mixedDevice = winnerIsCpu
                        ? ToWorkerDevice(phaseA, mixedPeer.Name)
                        : winnerDevice;
                    Console.WriteLine($"混池对照：CPU 进程内 + {mixedPeer.Name} worker 同时开一枪…");
                    mixed = OpenVinoClient.RunMixed(worker, resolved, mixedDevice, cpu.Infer, resolved.Warmup);
                    if (mixed is not null)
                        PrintOverlap(mixed);
                }

                Console.WriteLine();
            }

            var decision = BenchDecide.From(phaseA, serialWinner, parallelWinner, mixed, notes);
            Console.WriteLine("=== 建议 ===");
            foreach (var line in decision.Lines)
                Console.WriteLine("· " + line);

            if (!string.IsNullOrWhiteSpace(resolved.JsonOut))
            {
                var dump = new
                {
                    imageCount = frames.Count,
                    sizes = sizes,
                    phaseA,
                    serialWinner,
                    parallelWinner,
                    mixed,
                    decision,
                };
                File.WriteAllText(resolved.JsonOut,
                    System.Text.Json.JsonSerializer.Serialize(dump, ReportJson.Options));
                Console.WriteLine();
                Console.WriteLine("JSON: " + resolved.JsonOut);
            }

            return 0;
            }
            finally
            {
                foreach (var v in visions)
                    v.Dispose();
                foreach (var f in frames)
                    f.Dispose();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static string ToWorkerDevice(IReadOnlyList<EpSample> phaseA, string winnerName)
    {
        var hit = phaseA.FirstOrDefault(s => s.Name.Equals(winnerName, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(hit?.WorkerDevice))
            return hit.WorkerDevice;

        return winnerName switch
        {
            "OpenVINO GPU" => "GPU",
            "OpenVINO CPU" => "AUTO:CPU",
            _ => "CPU",
        };
    }

    private static EpSample ToEpSample(WorkerReport report, string name) => new()
    {
        Name = name,
        Latency = Stats.Summarize(report.TimesMs),
        DetectCount = report.DetectCount,
        Fingerprint = report.Fingerprint,
        Error = report.Ok ? null : report.Error,
        WorkerDevice = report.Device,
    };

    private static OverlapSummary RunCpuSerial(CpuPipeline cpu, BenchOptions opt) =>
        ToOverlap("CPU 串行两枪", TimedRun.SerialTwo(cpu.Infer, opt.Warmup, opt.Pairs));

    private static OverlapSummary ToOverlap(string name, OverlapSample[] samples) => new()
    {
        Name = name,
        Makespan = Stats.Summarize(samples.Select(s => s.MakespanMs).ToArray()),
        WaitA = Stats.Summarize(samples.Select(s => s.WaitAMs).ToArray()),
        WaitB = Stats.Summarize(samples.Select(s => s.WaitBMs).ToArray()),
    };

    private static OverlapSummary ToOverlap(string name, WorkerReport report) => new()
    {
        Name = name,
        Makespan = Stats.Summarize(report.MakespanMs),
        WaitA = Stats.Summarize(report.WaitAMs),
        WaitB = Stats.Summarize(report.WaitBMs),
    };

    private static void PrintLatency(EpSample sample)
    {
        if (sample.Error is not null)
        {
            Console.WriteLine($"  {sample.Name,-22} 失败  {sample.Error}");
            return;
        }

        var p = sample.Latency;
        Console.WriteLine(
            $"  {sample.Name,-22} p50={Stats.Fmt(p.P50),8}  p95={Stats.Fmt(p.P95),8}  p99={Stats.Fmt(p.P99),8}  mean={Stats.Fmt(p.Mean),8}  min={Stats.Fmt(p.Min),6}  max={Stats.Fmt(p.Max),6}  ms  检出={sample.DetectCount}");
    }

    private static void PrintOverlap(OverlapSummary s)
    {
        Console.WriteLine(
            $"  {s.Name,-28} 齐活 p50={Stats.Fmt(s.Makespan.P50),8}  p95={Stats.Fmt(s.Makespan.P95),8}  枪A p50={Stats.Fmt(s.WaitA.P50),8}  枪B p50={Stats.Fmt(s.WaitB.P50),8}  ms");
    }

    private static void PrintFingerprintDelta(EpSample cpu, EpSample other)
    {
        var (score, center, n) = FingerprintCompare.Compare(cpu.Fingerprint, other.Fingerprint);
        if (n == 0)
        {
            Console.WriteLine("    坐标对照: 一侧无检出，请换图或放宽 --confidence");
            return;
        }

        var warn = score > BenchDecide.ScoreWarn || center > BenchDecide.CenterWarnPx ? "  ← 建议再抽几张看 XY/分数" : "";
        Console.WriteLine(
            $"    相对进程内 OpenVINO CPU：max|Δscore|={Stats.Fmt(score)}  max中心距={Stats.Fmt(center)} px  对照 {n} 框{warn}");
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            RobotVision.InferenceBench — 进程内 OpenVINO CPU vs ov-worker OpenVINO（不要与 BenchmarkDotNet 那套混用）

            必须在目标工控机上、用现场 ONNX 和现场分辨率/ROI 图跑。

              dotnet run -c Release --project tools/RobotVision.InferenceBench -- ^
                --model models\xxx.onnx --image data\replay\a.bmp

            参数：
              --model <onnx>          可重复；第二个模型也可用 --model2（双模型配方把两枪时间加总）
              --image <path>          现场图或目录（目录内全部 bmp/jpg/png 轮流推理）
              --task auto|det|seg|pose
              --roi x,y,w,h           相对 0~1，与配方检测 ROI 相同
              --warmup 8  --iters 40  --pairs 20
              --confidence 0.25  --iou 0.45  --pixel-confidence 0.5
              --phase A|B|all         A 只做单枪；B/all 先做 A 再做两枪重叠
              --skip-openvino         只测进程内 OpenVINO CPU
              --ov-worker <dir>       默认 exe 旁 ov-worker\
              --ov-precision FP16|FP32
              --json-out report.json

            Phase A：进程内 OpenVINO CPU / worker OpenVINO CPU / OpenVINO GPU（若有）的 p50/p95/p99。
            Phase B：用 A 的赢家做 串行两枪、双同类会话并行、混池对照。
            产线与进程内路径都是 OpenVINO EP（DeviceType=CPU 或 GPU）。ov-worker 的 AUTO:CPU 用于对照插件路径。
            本工具不改 appsettings 的 Inference:Provider。测完按屏幕「建议」再改。
            """);
    }
}
