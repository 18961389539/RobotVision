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
    private Task<ChatToolResult> ListFailures(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var doc = Parse(args);
        var limit = Math.Clamp(Int(doc, "limit", 15), 1, 50);
        if (!Directory.Exists(_failures.Folder))
            return Task.FromResult(Ok(new { ok = true, count = 0, files = Array.Empty<string>(), folder = _failures.Folder }));
        var files = Directory.EnumerateFiles(_failures.Folder, "*.png", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTime)
            .Take(limit)
            .Select(p => new { path = p, name = Path.GetFileName(p), written = File.GetLastWriteTime(p) })
            .ToList();
        return Task.FromResult(Ok(new { ok = true, count = files.Count, folder = _failures.Folder, files }));
    }

    private Task<ChatToolResult> ListCalib(string _, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(Ok(new
        {
            ok = true,
            intrinsic = _calib.IntrinsicProfiles.Select(p => new { p.CameraId, p.Width, p.Height, p.Rms, p.CalibratedAt }),
            extrinsic = _calib.ExtrinsicProfiles.Select(p => new { p.StationId, p.CameraId, p.MountType, p.Rms, p.CalibratedAt }),
            polynomial = _calib.PolynomialProfiles.Select(p => new { p.StationId, p.CameraId, p.Width, p.Height, p.CalibratedAt }),
            scale = _calib.ScaleProfiles.Select(p => new { p.StationId, p.CameraId, p.ScaleX, p.ScaleY }),
            rotation = _calib.RotationCenterProfiles.Select(p => new { p.StationId, p.CameraId, p.Cx, p.Cy, p.Rms }),
            warnings = _calib.QualityWarnings,
        }));
    }

    private Task<ChatToolResult> SetLight(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var doc = Parse(args);
        var id = Str(doc, "id");
        var action = Str(doc, "action").ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(id))
            return Task.FromResult(Fail("请提供光源 id"));
        if (action is "raw")
        {
            var command = Str(doc, "command");
            if (string.IsNullOrWhiteSpace(command))
                return Task.FromResult(Fail("raw 需要 command"));
            _lights.Get(id).SendRaw(command);
            return Task.FromResult(Ok(new { ok = true, id, action, command }));
        }
        if (action is not ("on" or "off"))
            return Task.FromResult(Fail("action 必须是 on、off 或 raw"));
        if (action == "off")
        {
            _lights.TurnOff(id);
            return Task.FromResult(Ok(new { ok = true, id, action }));
        }
        var channel = Math.Max(1, Int(doc, "channel", 1));
        var brightness = Math.Clamp(Int(doc, "brightness", 128), 0, 255);
        _lights.TurnOn(id, channel, brightness);
        return Task.FromResult(Ok(new { ok = true, id, action, channel, brightness }));
    }

    private Task<ChatToolResult> ClearInhibit(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var doc = Parse(args);
        var recipe = EmptyToNull(Str(doc, "recipe"));
        _vision.ClearInhibit(recipe);
        return Task.FromResult(Ok(new { ok = true, recipe, inhibited = _vision.AnyInhibited }));
    }

    private Task<ChatToolResult> TcpControl(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var doc = Parse(args);
        var action = Str(doc, "action").ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(action) || action == "status")
        {
            return Task.FromResult(Ok(new
            {
                ok = true,
                running = _tcp.IsRunning,
                endpoint = _tcp.ListenEndPoint,
                port = _tcp.Port,
                clients = _tcp.GetClients().Select(c => new { c.Id, c.Remote, c.Requests, c.LastRequest }),
                recent = _tcp.GetRecentRequests().TakeLast(8).Select(r => new { r.Time, r.Request, r.Reply, r.Ok, r.ElapsedMs }),
                _tcp.TotalRequests,
                _tcp.RejectedConnections,
            }));
        }
        switch (action)
        {
            case "stop":
                _tcp.Stop();
                return Task.FromResult(Ok(new { ok = true, action, running = _tcp.IsRunning }));
            case "start":
                _tcp.Start();
                return Task.FromResult(Ok(new { ok = true, action, running = _tcp.IsRunning, endpoint = _tcp.ListenEndPoint }));
            case "restart":
                var ok = _tcp.Restart(_cfg.IpAddress, _cfg.TcpPort);
                return Task.FromResult(Ok(new { ok, action, running = _tcp.IsRunning, endpoint = _tcp.ListenEndPoint }));
            case "disconnect":
                if (!doc.RootElement.TryGetProperty("client_id", out var idNode) || !idNode.TryGetInt64(out var cid))
                    return Task.FromResult(Fail("disconnect 需要 client_id"));
                return Task.FromResult(Ok(new { ok = _tcp.DisconnectClient(cid), action, clientId = cid }));
            default:
                return Task.FromResult(Fail("action 必须是 status|stop|start|restart|disconnect"));
        }
    }

    private Task<ChatToolResult> ManageCalibration(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var doc = Parse(args);
        var action = Str(doc, "action").ToLowerInvariant();
        if (action is "" or "list")
            return ListCalib("{}", ct);
        if (action != "delete")
            return Task.FromResult(Fail("action 必须是 list 或 delete"));
        var kind = Str(doc, "kind").ToLowerInvariant();
        var id = Str(doc, "id");
        if (string.IsNullOrWhiteSpace(id))
            return Task.FromResult(Fail("delete 需要 id（相机或工位）"));
        var done = kind switch
        {
            "intrinsic" => _calib.DeleteIntrinsic(id),
            "extrinsic" => _calib.DeleteExtrinsic(id),
            "polynomial" => _calib.DeletePolynomial(id),
            "scale" => _calib.DeleteScale(id),
            "rotation" => _calib.DeleteRotationCenter(id),
            _ => (bool?)null,
        };
        if (done is null)
            return Task.FromResult(Fail("kind 必须是 intrinsic|extrinsic|polynomial|scale|rotation"));
        return Task.FromResult(Ok(new { ok = done.Value, action, kind, id }));
    }

    private Task<ChatToolResult> ManageFiles(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var doc = Parse(args);
        var action = Str(doc, "action").ToLowerInvariant();
        if (action is "" or "list_captures")
        {
            var limit = Math.Clamp(Int(doc, "limit", 15), 1, 50);
            var chatDir = _cfg.ResolveChatCapturesFolder();
            var capDir = _cfg.ResolveDataPath(_cfg.CaptureSuccess.Folder);
            return Task.FromResult(Ok(new
            {
                ok = true,
                chatCaptures = ListPng(chatDir, limit),
                successCaptures = ListPng(capDir, limit),
                successEnabled = _captures.Enabled,
            }));
        }
        var path = Str(doc, "path");
        if (string.IsNullOrWhiteSpace(path))
            return Task.FromResult(Fail("删除需要 path"));
        var root = action switch
        {
            "delete_failure" => _failures.Folder,
            "delete_capture" => _cfg.ResolveDataPath(_cfg.CaptureSuccess.Folder),
            "delete_chat" => _cfg.ResolveChatCapturesFolder(),
            _ => "",
        };
        if (root.Length == 0)
            return Task.FromResult(Fail("action 必须是 list_captures|delete_failure|delete_capture|delete_chat"));
        if (!IsSafeUnder(root, path))
            return Task.FromResult(Fail("路径不在允许的目录内"));
        if (!File.Exists(path))
            return Task.FromResult(Fail("文件不存在"));
        File.Delete(path);
        var meta = Path.ChangeExtension(path, ".json");
        if (File.Exists(meta))
            File.Delete(meta);
        return Task.FromResult(Ok(new { ok = true, action, path }));
    }

    private Task<ChatToolResult> ConvertPose(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var doc = Parse(args);
        var station = Str(doc, "station_id");
        if (string.IsNullOrWhiteSpace(station))
            return Task.FromResult(Fail("需提供 station_id"));
        if (!Has(doc, "px") || !Has(doc, "py"))
            return Task.FromResult(Fail("需提供 px 与 py"));
        var pixel = new PixelPose(Dbl(doc, "px", 0), Dbl(doc, "py", 0), Dbl(doc, "angle_deg", 0), 1);
        var cameraId = EmptyToNull(Str(doc, "camera_id"));
        try
        {
            var mode = _calib.GetMappingMode(station);
            var robot = mode switch
            {
                StationMappingMode.Polynomial => _calib.PixelToRobotPolynomial(station, pixel, cameraId),
                StationMappingMode.Scale => _calib.PixelToRobotScale(station, pixel, cameraId),
                _ => _calib.PixelToRobot(station, pixel, cameraId),
            };
            return Task.FromResult(Ok(new
            {
                ok = true,
                station,
                mode = mode.ToString(),
                pixel = new { pixel.Cx, pixel.Cy, pixel.AngleDeg },
                robot = new { robot.X, robot.Y, robot.AngleDeg },
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Fail(ex.Message));
        }
    }

    private Task<ChatToolResult> SystemInfo(string _, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var proc = System.Diagnostics.Process.GetCurrentProcess();
        var now = DateTimeOffset.Now;
        return Task.FromResult(Ok(new
        {
            ok = true,
            now = now.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
            today = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            weekday = now.ToString("dddd", new System.Globalization.CultureInfo("zh-CN")),
            machine = Environment.MachineName,
            processors = Environment.ProcessorCount,
            workingSetMb = Math.Round(proc.WorkingSet64 / 1024d / 1024d, 1),
            gcMb = Math.Round(GC.GetTotalMemory(false) / 1024d / 1024d, 1),
            inference = _cfg.Inference.Provider,
            recipes = _recipes.LoadedCount,
            recipeStats = _vision.GetRecipeStats(),
            consecutiveFails = _vision.MaxConsecutiveFails,
            inhibited = _vision.AnyInhibited,
            healthFolder = _vision.ProcessHealth?.Folder,
            resultDb = new
            {
                path = _sqlite.DatabasePath,
                writeEnabled = _sqlite.Enabled,
                retainedDays = _sqlite.RetainedDays,
                total = _sqlite.Count(),
            },
        }));
    }

    private Task<ChatToolResult> ListFiles(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var doc = Parse(args);
        var key = Str(doc, "folder").ToLowerInvariant();
        var folder = key switch
        {
            "recipes" => _recipes.Folder,
            "models" => _models.ModelsFolder,
            "calibration" => _cfg.ResolveCalibrationFolder(),
            "failures" => _failures.Folder,
            "captures" => _cfg.ResolveDataPath(_cfg.CaptureSuccess.Folder),
            "chat" => _cfg.ResolveChatCapturesFolder(),
            "results" => _sqlite.Folder,
            _ => null,
        };
        if (folder is null)
            return Task.FromResult(Fail("folder 必须是 recipes|models|calibration|failures|captures|chat|results"));
        if (!Directory.Exists(folder))
            return Task.FromResult(Ok(new { ok = true, folder, files = Array.Empty<string>() }));
        var files = Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
            .Take(80)
            .Select(Path.GetFileName)
            .ToArray();
        return Task.FromResult(Ok(new { ok = true, folder, count = files.Length, files }));
    }

    private Task<ChatToolResult> LightSendRaw(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var doc = Parse(args);
        var id = Str(doc, "id");
        var command = Str(doc, "command");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(command))
            return Task.FromResult(Fail("需要 id 和 command"));
        _lights.Get(id).SendRaw(command);
        return Task.FromResult(Ok(new { ok = true, id, command }));
    }
}
