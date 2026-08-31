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
    private Task<ChatToolResult> GetCamera(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var doc = Parse(args);
        var id = Str(doc, "camera_id");
        if (string.IsNullOrWhiteSpace(id))
            return Task.FromResult(Fail("请提供 camera_id"));
        var cfg = _cfg.Cameras.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
        if (!_cameras.TryGet(id, out var cam) || cam is null)
        {
            if (cfg is null)
                return Task.FromResult(Fail($"相机未注册: {id}"));
            return Task.FromResult(Ok(new
            {
                ok = true,
                id,
                registered = false,
                type = cfg.Type,
                name = cfg.Name,
                deviceId = cfg.DeviceId,
                folder = cfg.Folder,
                exposureUs = cfg.ExposureTimeUs,
                gain = cfg.Gain,
                grabTimeoutMs = cfg.GrabTimeoutMs,
            }));
        }
        var live = cam as IExposureControl;
        return Task.FromResult(Ok(new
        {
            ok = true,
            id,
            registered = true,
            kind = cam.Kind.ToString(),
            type = cfg?.Type,
            name = cfg?.Name,
            deviceId = cfg?.DeviceId,
            folder = cfg?.Folder,
            exposureUs = cfg?.ExposureTimeUs,
            gain = cfg?.Gain,
            liveExposureUs = live?.GetExposureTimeUs(),
            liveGain = live?.GetGain(),
            exposureRange = live?.GetExposureRange() is { } er ? new { er.Min, er.Max } : null,
            gainRange = live?.GetGainRange() is { } gr ? new { gr.Min, gr.Max } : null,
            grabTimeoutMs = cfg?.GrabTimeoutMs,
            grabHint = _cameras.GetGrabErrorHint(id),
            failed = cam is FailedCamera,
        }));
    }

    private async Task<ChatToolResult> Capture(string args, CancellationToken ct)
    {
        using var doc = Parse(args);
        var id = Str(doc, "camera_id");
        if (string.IsNullOrWhiteSpace(id))
        {
            var ids = _cameras.CameraIds.ToList();
            if (ids.Count == 1)
                id = ids[0];
            else if (ids.Count == 0)
                return Fail("没有已注册相机");
            else
                return Fail("有多台相机，请指定 camera_id: " + string.Join(", ", ids));
        }

        var hint = _cameras.GetGrabErrorHint(id);
        if (hint is not null)
            return Fail(hint);

        using var frame = await _cameras.GrabAsync(id, ct).ConfigureAwait(false);
        using var mat = VisionImageCv.AsMat(frame.Image);
        var folder = _cfg.ResolveChatCapturesFolder();
        Directory.CreateDirectory(folder);
        var safeId = string.Join("_", id.Split(Path.GetInvalidFileNameChars()));
        var path = Path.Combine(folder, $"chat_{DateTime.Now:yyyyMMdd_HHmmss}_{safeId}.png");
        if (!Cv2.ImWrite(path, mat))
            return Fail("写 PNG 失败: " + path);
        return Ok(new
        {
            ok = true,
            path,
            cameraId = id,
            width = mat.Width,
            height = mat.Height,
            capturedAtUtc = frame.CapturedAtUtc,
            acquireMs = frame.AcquireMs,
        }, path);
    }

    private Task<ChatToolResult> SetCamera(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var doc = Parse(args);
        var id = Str(doc, "camera_id");
        if (string.IsNullOrWhiteSpace(id))
            return Task.FromResult(Fail("请提供 camera_id"));
        var action = Str(doc, "action").ToLowerInvariant();
        if (action is "unregister")
        {
            var removed = _cameras.Unregister(id);
            var list = _cfg.Cameras.Where(c => !string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase)).ToList();
            _cameraStore.Save(list);
            return Task.FromResult(Ok(new { ok = removed, action, id }));
        }

        var cfg = _cfg.Cameras.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
        if (cfg is null)
            return Task.FromResult(Fail($"配置中没有相机 {id}"));
        if (Has(doc, "exposure_us"))
            cfg.ExposureTimeUs = Dbl(doc, "exposure_us", cfg.ExposureTimeUs ?? 0);
        if (Has(doc, "gain"))
            cfg.Gain = Dbl(doc, "gain", cfg.Gain ?? 0);
        if (Has(doc, "grab_timeout_ms"))
            cfg.GrabTimeoutMs = Int(doc, "grab_timeout_ms", cfg.GrabTimeoutMs);
        _cameraStore.Save(_cfg.Cameras);

        var live = false;
        if (_cameras.TryGet(id, out var cam) && cam is IExposureControl exp)
        {
            if (cfg.ExposureTimeUs is { } us)
                live |= exp.TrySetExposureTimeUs(us);
            if (cfg.Gain is { } g)
                live |= exp.TrySetGain(g);
        }

        return Task.FromResult(Ok(new
        {
            ok = true,
            id,
            cfg.ExposureTimeUs,
            cfg.Gain,
            cfg.GrabTimeoutMs,
            liveApplied = live,
        }));
    }

}
