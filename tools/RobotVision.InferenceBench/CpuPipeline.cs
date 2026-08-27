using RobotVision.Core.Models;
using RobotVision.Infrastructure;
using RobotVision.Infrastructure.Inference;

namespace RobotVision.InferenceBench;

internal sealed class CpuPipeline : IDisposable
{
    private readonly List<IInferenceEngine> _engines = [];
    private readonly Action _infer;
    public BenchTask Task { get; }
    public IReadOnlyList<BoxScore> Fingerprint { get; }
    public int DetectCount { get; }

    private CpuPipeline(List<IInferenceEngine> engines, BenchTask task, Action infer, List<BoxScore> fp)
    {
        _engines.AddRange(engines);
        Task = task;
        _infer = infer;
        Fingerprint = fp;
        DetectCount = fp.Count;
    }

    public static CpuPipeline Create(IReadOnlyList<string> models, IReadOnlyList<VisionImage> images, BenchOptions opt)
    {
        if (images.Count == 0)
            throw new ArgumentException("没有可推理的图像", nameof(images));

        var factory = new YoloDotNetEngineFactory("Cpu");
        var engines = models.Select(factory.Create).ToList();
        try
        {
            var task = ResolveTask(engines[0], opt.Task);
            var cursor = 0;
            VisionImage Next() => images[(Interlocked.Increment(ref cursor) - 1) % images.Count];

            Action pipeline = () =>
            {
                var image = Next();
                foreach (var e in engines)
                    Bind(e, image, task, opt)();
            };

            var fp = Snapshot(engines[0], images[0], task, opt);
            return new CpuPipeline(engines, task, pipeline, fp);
        }
        catch
        {
            foreach (var e in engines)
                e.Dispose();
            throw;
        }
    }

    public Action Infer => _infer;

    public CpuPipeline ClonePair(IReadOnlyList<string> models, IReadOnlyList<VisionImage> images, BenchOptions opt) =>
        Create(models, images, opt with { Task = Task });

    public static BenchTask ResolveTask(IInferenceEngine engine, BenchTask requested)
    {
        var detected = engine.DetectedTask switch
        {
            InferenceTask.ObjectDetection => BenchTask.ObjectDetection,
            InferenceTask.Segmentation => BenchTask.Segmentation,
            InferenceTask.PoseEstimation => BenchTask.PoseEstimation,
            _ => (BenchTask?)null,
        };

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

    public static Action Bind(IInferenceEngine engine, VisionImage image, BenchTask task, BenchOptions opt) =>
        task switch
        {
            BenchTask.ObjectDetection => () => engine.RunObjectDetection(image, opt.Confidence, opt.Iou),
            BenchTask.Segmentation => () => engine.RunSegmentation(image, opt.Confidence, opt.PixelConfidence, opt.Iou),
            BenchTask.PoseEstimation => () => engine.RunPoseEstimation(image, opt.Confidence, opt.Iou),
            _ => throw new InvalidOperationException("未解析任务"),
        };

    public static List<BoxScore> Snapshot(IInferenceEngine engine, VisionImage image, BenchTask task, BenchOptions opt)
    {
        switch (task)
        {
            case BenchTask.ObjectDetection:
                return engine.RunObjectDetection(image, opt.Confidence, opt.Iou)
                    .OrderByDescending(d => d.Confidence)
                    .Take(5)
                    .Select(d => new BoxScore
                    {
                        Label = d.Label,
                        Score = d.Confidence,
                        X = d.Box.X,
                        Y = d.Box.Y,
                        W = d.Box.Width,
                        H = d.Box.Height,
                    })
                    .ToList();
            case BenchTask.Segmentation:
                return engine.RunSegmentation(image, opt.Confidence, opt.PixelConfidence, opt.Iou)
                    .OrderByDescending(s => s.Confidence)
                    .Take(5)
                    .Select(s => new BoxScore
                    {
                        Label = s.Label,
                        Score = s.Confidence,
                        X = s.Box.X,
                        Y = s.Box.Y,
                        W = s.Box.Width,
                        H = s.Box.Height,
                    })
                    .ToList();
            case BenchTask.PoseEstimation:
                return engine.RunPoseEstimation(image, opt.Confidence, opt.Iou)
                    .OrderByDescending(p => p.Confidence)
                    .Take(5)
                    .Select(p => new BoxScore
                    {
                        Label = p.Label,
                        Score = p.Confidence,
                        X = p.Box.X,
                        Y = p.Box.Y,
                        W = p.Box.Width,
                        H = p.Box.Height,
                    })
                    .ToList();
            default:
                throw new InvalidOperationException("未解析任务");
        }
    }

    public void Dispose()
    {
        foreach (var e in _engines)
            e.Dispose();
    }
}
