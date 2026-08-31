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
    private Task<ChatToolResult> GetLogs(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_logs is null)
            return Task.FromResult(Fail("日志源未接入（非 WPF 宿主）"));
        using var doc = Parse(args);
        var limit = Math.Clamp(Int(doc, "limit", 30), 1, 100);
        return Task.FromResult(Ok(new { ok = true, lines = _logs.Recent(limit) }));
    }

    private Task<ChatToolResult> GetSettings(string _, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(Ok(new
        {
            ok = true,
            _cfg.IpAddress,
            _cfg.TcpPort,
            _cfg.TimeoutMs,
            _cfg.IdleTimeoutMs,
            _cfg.MaxQueueDepth,
            _cfg.MaxConcurrent,
            _cfg.TcpBacklog,
            _cfg.MaxConnections,
            whitelist = _cfg.IpWhitelist,
            poseCheck = _cfg.PoseCheck,
            processHealth = _cfg.ProcessHealth,
            failureImage = _cfg.FailureImage,
            captureSuccess = _cfg.CaptureSuccess,
            inference = _cfg.Inference,
            chat = new { _cfg.Chat.Endpoint, _cfg.Chat.Model, _cfg.Chat.MaxTokens, _cfg.Chat.ContextSize },
        }));
    }

    private Task<ChatToolResult> ManageModel(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var doc = Parse(args);
        var action = Str(doc, "action").ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(action) || action == "list")
        {
            return Task.FromResult(Ok(new
            {
                ok = true,
                folder = _models.ModelsFolder,
                files = _models.ModelFileNames,
                loaded = _models.LoadedKeys.Select(k => new { path = k.Path, task = k.Task.ToString() }),
                loadedCount = _models.LoadedCount,
            }));
        }
        if (action == "unload_all")
        {
            _models.UnloadAll();
            return Task.FromResult(Ok(new { ok = true, action, loadedCount = _models.LoadedCount }));
        }
        if (action != "unload")
            return Task.FromResult(Fail("action 必须是 list|unload|unload_all"));
        var file = Str(doc, "file");
        if (string.IsNullOrWhiteSpace(file))
            return Task.FromResult(Fail("unload 需要 file"));
        var taskName = Str(doc, "task");
        if (string.IsNullOrWhiteSpace(taskName))
            _models.UnloadAll(file);
        else if (Enum.TryParse<InferenceTask>(taskName, ignoreCase: true, out var task))
            _models.Unload(file, task);
        else
            return Task.FromResult(Fail("task 必须是 ObjectDetection、Segmentation 或 PoseEstimation"));
        return Task.FromResult(Ok(new { ok = true, action, file, task = taskName, loadedCount = _models.LoadedCount }));
    }

    private Task<ChatToolResult> UpdateSettings(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var doc = Parse(args);
        var values = CurrentSettings();
        if (Has(doc, "timeout_ms"))
            values = values with { TimeoutMs = Int(doc, "timeout_ms", values.TimeoutMs) };
        if (Has(doc, "idle_timeout_ms"))
            values = values with { IdleTimeoutMs = Long(doc, "idle_timeout_ms", values.IdleTimeoutMs) };
        if (Has(doc, "max_queue"))
            values = values with { MaxQueueDepth = Int(doc, "max_queue", values.MaxQueueDepth) };
        if (Has(doc, "max_concurrent"))
            values = values with { MaxConcurrent = Int(doc, "max_concurrent", values.MaxConcurrent) };
        if (Has(doc, "tcp_backlog"))
            values = values with { TcpBacklog = Int(doc, "tcp_backlog", values.TcpBacklog) };
        if (Has(doc, "max_connections"))
            values = values with { MaxConnections = Int(doc, "max_connections", values.MaxConnections) };
        if (Has(doc, "tcp_port"))
            values = values with { TcpPort = Int(doc, "tcp_port", values.TcpPort) };
        if (Has(doc, "ip"))
            values = values with { IpAddress = Str(doc, "ip") };
        if (MaybeBool(doc, "pose_check") is { } pc)
            values = values with { PoseCheckEnabled = pc };
        if (Has(doc, "xy_tol"))
            values = values with { PoseXyToleranceMm = Dbl(doc, "xy_tol", values.PoseXyToleranceMm) };
        if (Has(doc, "rz_tol"))
            values = values with { PoseRzToleranceDeg = Dbl(doc, "rz_tol", values.PoseRzToleranceDeg) };
        if (Has(doc, "whitelist"))
            values = values with { IpWhitelist = ReadStringList(doc, "whitelist") };
        if (MaybeBool(doc, "failure_enabled") is { } fe)
            values = values with { FailureEnabled = fe };
        if (Has(doc, "failure_retained"))
            values = values with { FailureRetainedCount = Int(doc, "failure_retained", values.FailureRetainedCount) };
        if (MaybeBool(doc, "process_health") is { } ph)
            values = values with { ProcessHealthEnabled = ph };
        if (Has(doc, "consecutive_fail_limit"))
            values = values with { ConsecutiveFailLimit = Int(doc, "consecutive_fail_limit", values.ConsecutiveFailLimit) };
        if (MaybeBool(doc, "inhibit_on_limit") is { } inhib)
            values = values with { InhibitOnLimit = inhib };
        try
        {
            var restart = _settings.Save(values);
            return Task.FromResult(Ok(new { ok = true, restartHint = restart, values }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Fail(ex.Message));
        }
    }

    private ServiceSettingsValues CurrentSettings() => new(
        TimeoutMs: _cfg.TimeoutMs,
        MaxQueueDepth: _cfg.MaxQueueDepth,
        MaxConcurrent: _cfg.MaxConcurrent,
        TcpBacklog: _cfg.TcpBacklog,
        MaxConnections: _cfg.MaxConnections,
        FailureEnabled: _cfg.FailureImage.Enabled,
        FailureRetainedCount: _cfg.FailureImage.RetainedCount,
        IpAddress: _cfg.IpAddress,
        TcpPort: _cfg.TcpPort,
        IpWhitelist: _cfg.IpWhitelist,
        IdleTimeoutMs: _cfg.IdleTimeoutMs,
        PoseCheckEnabled: _cfg.PoseCheck.Enabled,
        PoseXyToleranceMm: _cfg.PoseCheck.XyToleranceMm,
        PoseRzToleranceDeg: _cfg.PoseCheck.RzToleranceDeg,
        ProcessHealthEnabled: _cfg.ProcessHealth.Enabled,
        ConsecutiveFailLimit: _cfg.ProcessHealth.ConsecutiveFailLimit,
        InhibitOnLimit: _cfg.ProcessHealth.InhibitOnLimit,
        UiTheme: _cfg.UiTheme);
}
