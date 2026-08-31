using System.Text.Json;

namespace RobotVision.Hosting.Chat;

public sealed record ChatToolInvocationContext(string? LastUserMessage);

public readonly record struct DangerousActionCheck(bool IsBlocked, string? Reason)
{
    public static DangerousActionCheck Allow() => new(false, null);
    public static DangerousActionCheck Block(string reason) => new(true, reason);
}

/// <summary>危险工具调用：须 confirm:true，且用户最近一条消息含明确意图与对象。</summary>
public static class ChatDangerousActionGuard
{
    private static readonly string[] IntentKeywords =
    [
        "确认", "同意", "执行", "删除", "停用", "停止", "卸载", "解除", "断开",
        "修改", "更新", "注销", "改", "停", "确认执行",
    ];

    public static DangerousActionCheck Evaluate(string toolName, JsonElement args, string? lastUserMessage)
    {
        if (!TryDescribe(toolName, args, out var summary, out var targets))
            return DangerousActionCheck.Allow();

        if (!ChatToolArguments.GetBool(args, "confirm"))
        {
            return DangerousActionCheck.Block(
                $"危险操作已拦截（{summary}）：须在参数中传 confirm:true，且用户在最近一条消息中明确同意并点名对象。");
        }

        if (!UserExplicitlyConfirmed(lastUserMessage, targets))
        {
            return DangerousActionCheck.Block(
                $"危险操作已拦截（{summary}）：用户最近一条消息未明确确认（须含操作意图并提到对象，如配方名/路径/TCP）。");
        }

        return DangerousActionCheck.Allow();
    }

    private static bool UserExplicitlyConfirmed(string? userText, IReadOnlyList<string> targets)
    {
        if (string.IsNullOrWhiteSpace(userText))
            return false;
        var text = userText.Trim();
        if (!IntentKeywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase)))
            return false;
        if (targets.Count == 0)
            return true;
        return targets.Any(t =>
            !string.IsNullOrWhiteSpace(t)
            && text.Contains(t, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryDescribe(
        string toolName,
        JsonElement args,
        out string summary,
        out IReadOnlyList<string> targets)
    {
        summary = "";
        targets = [];
        var action = ChatToolArguments.GetString(args, "action").ToLowerInvariant();

        switch (toolName)
        {
            case "tcp_control" when action is "stop" or "restart" or "disconnect":
                summary = $"tcp_control.{action}";
                targets = ["tcp", "通信", "9999", action, action switch { "stop" => "停", "restart" => "重启", _ => "断开" }];
                return true;
            case "manage_recipe" when action is "delete" or "disable" or "enable" or "patch" or "duplicate":
                summary = $"manage_recipe.{action}";
                targets = Collect(
                    ChatToolArguments.GetString(args, "name"),
                    ChatToolArguments.GetString(args, "new_name"));
                return true;
            case "manage_calibration" when action == "delete":
                summary = "manage_calibration.delete";
                targets = Collect(
                    ChatToolArguments.GetString(args, "id"),
                    ChatToolArguments.GetString(args, "kind"));
                return true;
            case "manage_files" when action is "delete_failure" or "delete_capture" or "delete_chat":
                summary = $"manage_files.{action}";
                targets = Collect(
                    ChatToolArguments.GetString(args, "path"),
                    Path.GetFileName(ChatToolArguments.GetString(args, "path")));
                return true;
            case "manage_model" when action is "unload" or "unload_all":
                summary = $"manage_model.{action}";
                targets = Collect(
                    ChatToolArguments.GetString(args, "file"),
                    action);
                return true;
            case "set_camera" when action == "unregister":
                summary = "set_camera.unregister";
                targets = Collect(ChatToolArguments.GetString(args, "camera_id"));
                return true;
            case "update_settings":
                if (ChatToolArguments.PropertyCount(args) == 0
                    || (ChatToolArguments.PropertyCount(args) == 1 && ChatToolArguments.HasProperty(args, "confirm")))
                {
                    return false;
                }
                summary = "update_settings";
                targets = ["设置", "参数", "端口", "tcp", "timeout", "队列", "whitelist", "白名单"];
                return true;
            case "clear_inhibit":
                summary = "clear_inhibit";
                targets = Collect(
                    ChatToolArguments.GetString(args, "recipe"),
                    "1018", "联锁", "解除");
                return true;
            case "light_send_raw":
                summary = "light_send_raw";
                targets = Collect(
                    ChatToolArguments.GetString(args, "id"),
                    ChatToolArguments.GetString(args, "command"));
                return true;
            case "set_light" when action == "raw":
                summary = "set_light.raw";
                targets = Collect(
                    ChatToolArguments.GetString(args, "id"),
                    ChatToolArguments.GetString(args, "command"));
                return true;
            default:
                return false;
        }
    }

    private static IReadOnlyList<string> Collect(params string?[] values) =>
        values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}
