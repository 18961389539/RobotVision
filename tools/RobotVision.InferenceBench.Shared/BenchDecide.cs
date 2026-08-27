namespace RobotVision.InferenceBench;

/// <summary>
/// 与约定判定表一致：A 选 EP；B 决定要不要第二个同类会话；混池只作对照。
/// </summary>
public static class BenchDecide
{
    public const double SwitchMargin = 0.10;
    public const double OverlapGain = 0.15;
    public const double ScoreWarn = 0.05;
    public const double CenterWarnPx = 5.0;

    public const string InProcOpenVinoCpu = "OpenVINO CPU (in-proc)";

    public static bool IsInProcCpuName(string? name) =>
        !string.IsNullOrEmpty(name) &&
        (name.Equals("CPU", StringComparison.OrdinalIgnoreCase)
         || name.Equals(InProcOpenVinoCpu, StringComparison.OrdinalIgnoreCase)
         || name.Contains("in-proc", StringComparison.OrdinalIgnoreCase));

    public static Decision From(
        IReadOnlyList<EpSample> phaseA,
        OverlapSummary? serialWinner,
        OverlapSummary? parallelSameEp,
        OverlapSummary? mixedControl,
        IReadOnlyList<string>? extraNotes = null)
    {
        var lines = new List<string>();
        if (extraNotes is not null)
            lines.AddRange(extraNotes);

        var ok = phaseA.Where(s => s.Error is null && !double.IsNaN(s.Latency.P50)).ToList();
        if (ok.Count == 0)
        {
            lines.Add("Phase A 没有成功样本，无法推荐。请检查模型、图片与 OpenVINO 运行库。");
            return new Decision { Lines = lines };
        }

        var cpu = ok.FirstOrDefault(s => IsInProcCpuName(s.Name));
        var winner = ok.MinBy(s => s.Latency.P50)!;
        var switchFromCpu = cpu is null
            || (!IsInProcCpuName(winner.Name)
                && winner.Latency.P50 <= cpu.Latency.P50 * (1 - SwitchMargin));

        if (cpu is not null && !IsInProcCpuName(winner.Name))
        {
            var ratio = cpu.Latency.P50 / winner.Latency.P50;
            lines.Add($"Phase A 赢家：{winner.Name}（p50={Stats.Fmt(winner.Latency.P50)} ms，约为进程内 OpenVINO CPU 的 {ratio:F2}× 快）。");
        }
        else
        {
            lines.Add($"Phase A 赢家：{winner.Name}（p50={Stats.Fmt(winner.Latency.P50)} ms）。");
        }

        if (switchFromCpu)
            lines.Add($"建议：全局换成 {winner.Name}，先保持一个会话（改 Inference:Provider，不要混池）。");
        else if (cpu is not null && IsInProcCpuName(winner.Name))
            lines.Add("建议：维持 OpenVINO CPU。它已是最快，或其它 EP 未测到。");
        else
            lines.Add($"建议：维持 OpenVINO CPU。赢家相对进程内 CPU 的优势不足 {SwitchMargin:P0}，换 EP 的运维成本不划算。");

        var addSecond = false;
        var dropMixed = true;
        if (serialWinner is not null && parallelSameEp is not null
            && !double.IsNaN(serialWinner.Makespan.P50)
            && !double.IsNaN(parallelSameEp.Makespan.P50))
        {
            var gain = 1 - parallelSameEp.Makespan.P50 / serialWinner.Makespan.P50;
            lines.Add($"Phase B 两枪齐活：串行 p50={Stats.Fmt(serialWinner.Makespan.P50)} ms，双同类会话并行 p50={Stats.Fmt(parallelSameEp.Makespan.P50)} ms（相对串行 {gain:P0}）。");
            if (gain >= OverlapGain)
            {
                addSecond = true;
                lines.Add("建议：仅当现场确有两路 TRIGGER 重叠时，再开第二个相同 EP 会话。同一相机取图仍是串行的。");
            }
            else
            {
                lines.Add("建议：不必为池子改架构。并行齐活几乎没有短于 2T，或现场是拍完等结果再拍。");
            }
        }

        if (mixedControl is not null && serialWinner is not null
            && !double.IsNaN(mixedControl.Makespan.P50)
            && !double.IsNaN(serialWinner.Makespan.P50))
        {
            dropMixed = mixedControl.Makespan.P50 + 0.5 >= serialWinner.Makespan.P50;
            lines.Add($"混池对照（CPU+OpenVINO 各一枪）齐活 p50={Stats.Fmt(mixedControl.Makespan.P50)} ms，赢家串行两枪 p50={Stats.Fmt(serialWinner.Makespan.P50)} ms。");
            lines.Add(dropMixed
                ? "建议：去掉 CPU+OpenVINO 混池。慢的那路拖到齐活，不比赢家串行两枪短。"
                : "混池齐活略短于赢家串行两枪，但仍不要作默认：调度复杂，且单枪可能落到慢路上。");
        }

        return new Decision
        {
            WinnerEp = winner.Name,
            SwitchFromCpu = switchFromCpu,
            AddSecondSameEpSession = addSecond,
            DropMixedPool = dropMixed,
            Lines = lines,
        };
    }
}
