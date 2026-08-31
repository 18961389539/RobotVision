using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace RobotVision.InferenceBench;

internal static class OpenVinoClient
{
    public static string? FindWorker(string? overrideDir)
    {
        var names = new[] { "RobotVision.InferenceBench.OpenVino.exe", "RobotVision.InferenceBench.OpenVino.dll" };
        var dirs = new List<string>();
        if (!string.IsNullOrWhiteSpace(overrideDir))
            dirs.Add(overrideDir);
        dirs.Add(Path.Combine(AppContext.BaseDirectory, "ov-worker"));

        foreach (var dir in dirs)
        {
            var exe = Path.Combine(dir, names[0]);
            if (File.Exists(exe))
                return exe;
        }

        return null;
    }

    public static WorkerReport Run(string workerExe, BenchOptions opt, string device, WorkerMode mode)
    {
        var outFile = Path.Combine(Path.GetTempPath(), $"rv-ov-{Guid.NewGuid():N}.json");
        var args = BuildArgs(opt, device, mode, outFile);
        var psi = BaseStartInfo(workerExe, args);
        psi.RedirectStandardInput = false;

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                Console.Error.WriteLine(e.Data);
        };
        proc.OutputDataReceived += (_, _) => { };
        if (!proc.Start())
            throw new InvalidOperationException("无法启动 OpenVINO worker");
        proc.BeginErrorReadLine();
        proc.BeginOutputReadLine();

        if (!proc.WaitForExit(30 * 60 * 1000))
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
            throw new TimeoutException("OpenVINO worker 超时（30 分钟）");
        }

        if (!File.Exists(outFile))
        {
            return new WorkerReport
            {
                Ok = false,
                Error = $"worker 退出码 {proc.ExitCode}，未写出报告",
                Provider = "OpenVINO",
                Device = device,
            };
        }

        try
        {
            var report = ReportJson.Read(outFile);
            if (!report.Ok && string.IsNullOrEmpty(report.Error))
                report.Error = $"worker 退出码 {proc.ExitCode}";
            return report;
        }
        finally
        {
            try { File.Delete(outFile); } catch { /* ignore */ }
        }
    }

    public static OverlapSummary? RunMixed(
        string workerExe,
        BenchOptions opt,
        string ovDevice,
        Action cpuInfer,
        int warmupCpu)
    {
        var args = BuildArgs(opt with { Mode = WorkerMode.Gated, Warmup = opt.Warmup }, ovDevice, WorkerMode.Gated, outFile: null);
        var psi = BaseStartInfo(workerExe, args);
        psi.RedirectStandardInput = true;

        using var proc = new Process { StartInfo = psi };
        var stderr = new StringBuilder();
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                stderr.AppendLine(e.Data);
        };
        if (!proc.Start())
            throw new InvalidOperationException("无法启动 OpenVINO worker");
        proc.BeginErrorReadLine();

        try
        {
            var ready = proc.StandardOutput.ReadLine();
            if (!string.Equals(ready, "READY", StringComparison.Ordinal))
            {
                proc.StandardInput.WriteLine("QUIT");
                proc.WaitForExit(10_000);
                Console.Error.WriteLine($"混池：worker 未 READY（{ready}）。{stderr}");
                return null;
            }

            for (var i = 0; i < warmupCpu; i++)
                cpuInfer();

            TimedRun.PrepareGc();
            var samples = new OverlapSample[opt.Pairs];
            for (var i = 0; i < opt.Pairs; i++)
            {
                double waitCpu = 0, waitOv = 0;
                var gate = new ManualResetEventSlim(false);
                var cpuTask = Task.Run(() =>
                {
                    gate.Wait();
                    var sw = Stopwatch.StartNew();
                    cpuInfer();
                    waitCpu = sw.Elapsed.TotalMilliseconds;
                });

                while (cpuTask.Status is TaskStatus.Created or TaskStatus.WaitingForActivation or TaskStatus.WaitingToRun)
                    Thread.SpinWait(20);

                var pair = Stopwatch.StartNew();
                gate.Set();
                proc.StandardInput.WriteLine("GO");
                proc.StandardInput.Flush();
                var done = proc.StandardOutput.ReadLine();
                cpuTask.Wait();
                var makespan = pair.Elapsed.TotalMilliseconds;

                if (done is null || !done.StartsWith("DONE ", StringComparison.Ordinal))
                    throw new InvalidDataException($"混池 worker 应答异常: {done}");

                var ovText = done["DONE ".Length..].Trim();
                waitOv = double.Parse(ovText, CultureInfo.InvariantCulture);
                samples[i] = new OverlapSample(makespan, waitCpu, waitOv);
            }

            proc.StandardInput.WriteLine("QUIT");
            proc.StandardInput.Flush();
            proc.WaitForExit(30_000);

            return new OverlapSummary
            {
                Name = $"混池 CPU + OpenVINO {ovDevice}",
                Makespan = Stats.Summarize(samples.Select(s => s.MakespanMs).ToArray()),
                WaitA = Stats.Summarize(samples.Select(s => s.WaitAMs).ToArray()),
                WaitB = Stats.Summarize(samples.Select(s => s.WaitBMs).ToArray()),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"混池对照失败: {ex.Message}");
            if (stderr.Length > 0)
                Console.Error.WriteLine(stderr.ToString());
            try
            {
                if (!proc.HasExited)
                    proc.Kill(entireProcessTree: true);
            }
            catch { /* ignore */ }
            return null;
        }
    }

    private static ProcessStartInfo BaseStartInfo(string workerExe, string arguments)
    {
        var workDir = Path.GetDirectoryName(workerExe)!;
        return new ProcessStartInfo
        {
            FileName = workerExe,
            Arguments = arguments,
            WorkingDirectory = workDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
        };
    }

    private static string BuildArgs(BenchOptions opt, string device, WorkerMode mode, string? outFile)
    {
        var sb = new StringBuilder();
        foreach (var m in opt.Models)
            sb.Append(CultureInfo.InvariantCulture, $" --model {Quote(m)}");
        sb.Append(CultureInfo.InvariantCulture, $" --image {Quote(opt.Image)}");
        sb.Append(CultureInfo.InvariantCulture, $" --task {TaskArg(opt.Task)}");
        sb.Append(CultureInfo.InvariantCulture, $" --mode {ModeArg(mode)}");
        sb.Append(CultureInfo.InvariantCulture, $" --ov-device {Quote(device)}");
        if (!string.IsNullOrWhiteSpace(opt.OvPrecision))
            sb.Append(CultureInfo.InvariantCulture, $" --ov-precision {opt.OvPrecision}");
        sb.Append(CultureInfo.InvariantCulture, $" --warmup {opt.Warmup}");
        sb.Append(CultureInfo.InvariantCulture, $" --iters {opt.Iters}");
        sb.Append(CultureInfo.InvariantCulture, $" --pairs {opt.Pairs}");
        sb.Append(CultureInfo.InvariantCulture, $" --confidence {opt.Confidence.ToString(CultureInfo.InvariantCulture)}");
        sb.Append(CultureInfo.InvariantCulture, $" --iou {opt.Iou.ToString(CultureInfo.InvariantCulture)}");
        sb.Append(CultureInfo.InvariantCulture, $" --pixel-confidence {opt.PixelConfidence.ToString(CultureInfo.InvariantCulture)}");
        if (opt.Roi is not null)
        {
            sb.Append(CultureInfo.InvariantCulture,
                $" --roi {opt.Roi.X.ToString(CultureInfo.InvariantCulture)},{opt.Roi.Y.ToString(CultureInfo.InvariantCulture)},{opt.Roi.Width.ToString(CultureInfo.InvariantCulture)},{opt.Roi.Height.ToString(CultureInfo.InvariantCulture)}");
        }

        if (!string.IsNullOrWhiteSpace(outFile))
            sb.Append(CultureInfo.InvariantCulture, $" --out {Quote(outFile)}");

        return sb.ToString();
    }

    private static string TaskArg(BenchTask task) => task switch
    {
        BenchTask.ObjectDetection => "det",
        BenchTask.Segmentation => "seg",
        BenchTask.PoseEstimation => "pose",
        _ => "auto",
    };

    private static string ModeArg(WorkerMode mode) => mode switch
    {
        WorkerMode.Serial2 => "serial2",
        WorkerMode.Parallel2 => "parallel2",
        WorkerMode.Gated => "gated",
        _ => "single",
    };

    private static string Quote(string path) => $"\"{path.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
}
