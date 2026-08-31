using System.Globalization;
using System.Text.Json;
using OpenCvSharp;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Communication;
using RobotVision.Infrastructure.Inference;
using RobotVision.Infrastructure.Lighting;

namespace RobotVision.Hosting.Chat;
public sealed partial class StationChatTools
{
    private Task<ChatToolResult> Overview(string _, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var cameras = _cameras.CameraIds.Select(id =>
        {
            _cameras.TryGet(id, out var cam);
            var cfg = _cfg.Cameras.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
            return new
            {
                id,
                kind = cam?.Kind.ToString(),
                type = cfg?.Type,
                name = string.IsNullOrWhiteSpace(cfg?.Name) ? id : cfg!.Name,
                grabHint = _cameras.GetGrabErrorHint(id),
            };
        }).ToList();
        var health = _vision.Health;
        return Task.FromResult(Ok(new
        {
            ok = true,
            cameraCount = cameras.Count,
            configured = _cfg.Cameras.Select(c => new { c.Id, c.Type, name = string.IsNullOrWhiteSpace(c.Name) ? c.Id : c.Name }).ToList(),
            cameras,
            lights = _lights.ControllerIds.ToArray(),
            recipes = _recipes.ListNames(),
            tcp = new
            {
                running = _tcp.IsRunning,
                endpoint = _tcp.ListenEndPoint,
                port = _tcp.Port,
                clients = _tcp.ConnectedClients,
                totalRequests = _tcp.TotalRequests,
            },
            vision = new
            {
                queue = _vision.QueueDepth,
                processing = _vision.IsProcessing,
                lastMs = _vision.LastElapsedMs,
                maxQueue = _vision.MaxQueueDepth,
                health.Total,
                health.Failed,
                health.TimedOut,
                health.AvgMs,
                inhibited = _vision.AnyInhibited,
            },
            models = new { files = _models.ModelFileNames, loaded = _models.LoadedCount },
        }));
    }

}
