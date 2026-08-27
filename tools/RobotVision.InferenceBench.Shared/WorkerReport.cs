namespace RobotVision.InferenceBench;

public sealed class WorkerReport
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public string Provider { get; set; } = "";
    public string Device { get; set; } = "";
    public string Task { get; set; } = "";
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public int CropWidth { get; set; }
    public int CropHeight { get; set; }
    public int DetectCount { get; set; }
    public double[] TimesMs { get; set; } = [];
    public double[] MakespanMs { get; set; } = [];
    public double[] WaitAMs { get; set; } = [];
    public double[] WaitBMs { get; set; } = [];
    public BoxScore[] Fingerprint { get; set; } = [];
}

public sealed class EpSample
{
    public required string Name { get; init; }
    public Percentiles Latency { get; init; }
    public int DetectCount { get; init; }
    public BoxScore[] Fingerprint { get; init; } = [];
    public string? Error { get; init; }
    /// <summary>传给 ov-worker 的 --ov-device（可能是 AUTO:CPU / GPU / CPU）。</summary>
    public string? WorkerDevice { get; init; }
}

public sealed class OverlapSummary
{
    public required string Name { get; init; }
    public Percentiles Makespan { get; init; }
    public Percentiles WaitA { get; init; }
    public Percentiles WaitB { get; init; }
}

public sealed class Decision
{
    public string WinnerEp { get; init; } = "CPU";
    public bool SwitchFromCpu { get; init; }
    public bool AddSecondSameEpSession { get; init; }
    public bool DropMixedPool { get; init; } = true;
    public IReadOnlyList<string> Lines { get; init; } = [];
}
