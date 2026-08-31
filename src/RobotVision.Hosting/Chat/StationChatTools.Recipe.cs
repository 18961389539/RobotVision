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
    private Task<ChatToolResult> GetRecipe(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var doc = Parse(args);
        var name = Str(doc, "name");
        if (string.IsNullOrWhiteSpace(name))
            return Task.FromResult(Fail("请提供配方 name"));
        try
        {
            var r = _recipes.Get(name);
            return Task.FromResult(Ok(new
            {
                ok = true,
                r.Name,
                r.Enabled,
                r.SerialNumber,
                r.Description,
                r.CameraId,
                r.StationId,
                angleMode = r.AngleMode.ToString(),
                r.Models,
                r.Confidence,
                r.Iou,
                r.LightControllerId,
                mapping = r.StationId is null ? "none" : _calib.GetMappingMode(r.StationId).ToString(),
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Fail(ex.Message));
        }
    }

    private async Task<ChatToolResult> RunRecipe(string args, CancellationToken ct)
    {
        using var doc = Parse(args);
        var name = Str(doc, "name");
        if (string.IsNullOrWhiteSpace(name))
            return Fail("请提供配方 name");
        var (resolved, resolveErr) = _recipes.ResolveTriggerKey(name);
        if (resolved is null)
            return Fail(resolveErr ?? "未知配方");
        var result = await _vision.RunAsync(resolved, ct).ConfigureAwait(false);
        return Ok(new
        {
            ok = result.Ok,
            recipe = result.RecipeName,
            code = (int)result.ErrorCode,
            error = result.ErrorCode.ToString(),
            result.Message,
            result.ElapsedMs,
            count = result.Poses.Count,
            poses = result.Poses.Select((p, i) => new
            {
                i,
                p.X,
                p.Y,
                p.AngleDeg,
                score = i < result.Confidences.Count ? result.Confidences[i] : (double?)null,
            }),
        });
    }

    private Task<ChatToolResult> ManageRecipe(string args, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var doc = Parse(args);
        var action = Str(doc, "action").ToLowerInvariant();
        var name = Str(doc, "name");
        if (string.IsNullOrWhiteSpace(name))
            return Task.FromResult(Fail("请提供配方 name"));
        var (resolved, resolveErr) = _recipes.ResolveTriggerKey(name);
        if (resolved is null)
            return Task.FromResult(Fail(resolveErr ?? "未知配方"));
        name = resolved;
        try
        {
            switch (action)
            {
                case "enable":
                case "disable":
                {
                    var r = _recipes.Get(name);
                    r.Enabled = action == "enable";
                    _recipes.Save(r);
                    return Task.FromResult(Ok(new { ok = true, action, r.Name, r.Enabled }));
                }
                case "delete":
                    return Task.FromResult(Ok(new { ok = _recipes.Delete(name), action, name }));
                case "duplicate":
                {
                    var newName = Str(doc, "new_name");
                    if (string.IsNullOrWhiteSpace(newName))
                        return Task.FromResult(Fail("duplicate 需要 new_name"));
                    var copy = _recipes.Get(name).Clone();
                    copy.Name = newName;
                    copy.SerialNumber = 0;
                    _recipes.Save(copy);
                    return Task.FromResult(Ok(new { ok = true, action, from = name, copy.Name }));
                }
                case "validate":
                {
                    var r = _recipes.Get(name);
                    RecipeLoader.Validate(r);
                    _recipes.ValidateReferences(r);
                    return Task.FromResult(Ok(new
                    {
                        ok = true,
                        action,
                        r.Name,
                        asset = _assets.Check(r) ?? "ok",
                        integrityEnabled = _assets.IsEnabled,
                    }));
                }
                case "patch":
                {
                    var r = _recipes.Get(name);
                    if (MaybeBool(doc, "enabled") is { } en)
                        r.Enabled = en;
                    if (Has(doc, "confidence"))
                        r.Confidence = Dbl(doc, "confidence", r.Confidence);
                    if (Has(doc, "iou"))
                        r.Iou = Dbl(doc, "iou", r.Iou);
                    if (Has(doc, "camera_id"))
                        r.CameraId = Str(doc, "camera_id");
                    if (Has(doc, "description"))
                        r.Description = Str(doc, "description");
                    if (Has(doc, "serial"))
                        r.SerialNumber = Int(doc, "serial", r.SerialNumber);
                    _recipes.Save(r);
                    return Task.FromResult(Ok(new { ok = true, action, r.Name, r.Enabled, r.Confidence, r.Iou, r.CameraId, r.SerialNumber }));
                }
                default:
                    return Task.FromResult(Fail("action 必须是 enable|disable|delete|duplicate|validate|patch"));
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(Fail(ex.Message));
        }
    }

}
