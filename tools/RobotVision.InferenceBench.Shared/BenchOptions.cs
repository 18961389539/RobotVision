using RobotVision.Core.Models;

namespace RobotVision.InferenceBench;

public enum BenchTask
{
    Auto,
    ObjectDetection,
    Segmentation,
    PoseEstimation,
}

public enum WorkerMode
{
    One,
    Serial2,
    Parallel2,
    Gated,
}

public sealed record BenchOptions
{
    public required IReadOnlyList<string> Models { get; init; }
    public required string Image { get; init; }
    public BenchTask Task { get; init; } = BenchTask.Auto;
    public Roi? Roi { get; init; }
    public int Warmup { get; init; } = 8;
    public int Iters { get; init; } = 40;
    public int Pairs { get; init; } = 20;
    public double Confidence { get; init; } = 0.25;
    public double Iou { get; init; } = 0.45;
    public double PixelConfidence { get; init; } = 0.5;
    public string Phase { get; init; } = "all";
    public bool SkipOpenVino { get; init; }
    public string? JsonOut { get; init; }
    public string? OvWorkerDir { get; init; }
    public string OvDevice { get; init; } = "CPU";
    public string OvPrecision { get; init; } = "";
    public WorkerMode Mode { get; init; } = WorkerMode.One;
    public string? OutFile { get; init; }

    public static BenchOptions From(CliArgs args)
    {
        var models = args.GetAll("--model").ToList();
        var model2 = args.Get("--model2");
        if (!string.IsNullOrWhiteSpace(model2))
            models.Add(model2);

        var image = args.Get("--image") ?? "";
        var task = ParseTask(args.Get("--task", "auto"));
        var phase = args.Get("--phase", "all").ToLowerInvariant();
        if (phase is not ("a" or "b" or "all"))
            throw new FormatException("--phase 只能是 A、B 或 all");
        // B 仍会先做 A：重叠测试需要赢家 EP。

        return new BenchOptions
        {
            Models = models,
            Image = image,
            Task = task,
            Roi = BenchImage.ParseRoi(args.Get("--roi")),
            Warmup = Math.Max(0, args.GetInt("--warmup", 8)),
            Iters = Math.Max(1, args.GetInt("--iters", 40)),
            Pairs = Math.Max(1, args.GetInt("--pairs", 20)),
            Confidence = args.GetDouble("--confidence", 0.25),
            Iou = args.GetDouble("--iou", 0.45),
            PixelConfidence = args.GetDouble("--pixel-confidence", 0.5),
            Phase = phase,
            SkipOpenVino = args.Has("--skip-openvino"),
            JsonOut = args.Get("--json-out"),
            OvWorkerDir = args.Get("--ov-worker"),
            OvDevice = args.Get("--ov-device", "CPU"),
            OvPrecision = args.Get("--ov-precision", ""),
            Mode = ParseMode(args.Get("--mode", "single")),
            OutFile = args.Get("--out"),
        };
    }

    public static BenchTask ParseTask(string text) => text.Trim().ToLowerInvariant() switch
    {
        "auto" => BenchTask.Auto,
        "det" or "detect" or "detection" or "objectdetection" => BenchTask.ObjectDetection,
        "seg" or "segmentation" => BenchTask.Segmentation,
        "pose" or "kpt" or "poseestimation" => BenchTask.PoseEstimation,
        _ => throw new FormatException($"未知 --task: {text}（auto|det|seg|pose）"),
    };

    public static WorkerMode ParseMode(string text) => text.Trim().ToLowerInvariant() switch
    {
        "single" => WorkerMode.One,
        "serial2" => WorkerMode.Serial2,
        "parallel2" => WorkerMode.Parallel2,
        "gated" => WorkerMode.Gated,
        _ => throw new FormatException($"未知 --mode: {text}"),
    };

    public static string TaskLabel(BenchTask task) => task switch
    {
        BenchTask.ObjectDetection => "检测",
        BenchTask.Segmentation => "分割",
        BenchTask.PoseEstimation => "关键点",
        _ => "自动",
    };
}
