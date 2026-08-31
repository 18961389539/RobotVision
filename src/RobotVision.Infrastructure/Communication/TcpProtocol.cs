using System;
using System.Globalization;
using System.Net;
using System.Text;
using RobotVision.Core.Models;

namespace RobotVision.Infrastructure.Communication;

/// <summary>TCP 行协议解析与应答格式化（纯函数，可单测）。</summary>
public static class TcpProtocol
{
    /// <summary>
    /// 触发行解析（纯函数，可单测）：
    /// - 1 段（配方名或序列号）→ pose = null；
    /// - 4 段（键,X,Y,RZ）→ 带拍照位姿（OnArm 工位一致性校验用）；
    /// - 其他段数 / 数值非有限 → formatError（ERR,1013 模板）。
    /// </summary>
    public static (string RecipeKey, TcpClientPose? Pose, string? FormatError) ParseTriggerLine(string line)
    {
        line = line.Trim();
        if (line.Length == 0)
            return ("", null, "MISSING_RECIPE");

        var parts = line.Split(',');
        if (parts.Length == 1)
            return (NormalizeTriggerKey(parts[0]), null, null);

        if (parts.Length != 4)
            return ("", null, "TRIGGER_ARGUMENT_COUNT");

        var key = NormalizeTriggerKey(parts[0]);
        if (key.Length == 0)
            return ("", null, "MISSING_RECIPE");

        if (!TryParseFinite(parts[1], out var x) ||
            !TryParseFinite(parts[2], out var y) ||
            !TryParseFinite(parts[3], out var rz))
            return ("", null, "INVALID_POSE_NUMBER");

        return (key, new TcpClientPose(x, y, rz), null);
    }

  [Obsolete("Use ParseTriggerLine")]
    public static (string RecipeName, TcpClientPose? Pose, string? FormatError) ParseTriggerArgument(string argument)
    {
        if (argument.Trim().Length == 0)
            return ("", null, "MISSING_RECIPE");
        return ParseTriggerLine(argument);
    }

    public static string FormatStatus(TcpServerManager.TcpServerState? state)
    {
        if (state is null)
            return "OK,ready,0,0,0";
        var inhibited = state.Inhibited != 0 ? 1 : 0;
        return $"OK,{(state.Ready ? "ready" : "busy")},{state.QueueDepth},{state.MaxQueueDepth},{state.LastElapsedMs:0},{state.ConsecutiveFails},{inhibited}";
    }

    public static bool IsClearInhibitCommand(string line)
    {
        var trimmed = line.Trim();
        if (string.Equals(trimmed, "CLEARINHIBIT", StringComparison.OrdinalIgnoreCase))
            return true;
        return trimmed.StartsWith("CLEARINHIBIT,", StringComparison.OrdinalIgnoreCase);
    }

    public static string? ParseClearInhibitRecipe(string line)
    {
        var trimmed = line.Trim();
        var comma = trimmed.IndexOf(',', StringComparison.Ordinal);
        if (comma < 0)
            return null;
        var recipe = trimmed[(comma + 1)..].Trim();
        var next = recipe.IndexOf(',', StringComparison.Ordinal);
        if (next >= 0)
            recipe = recipe[..next].Trim();
        return recipe.Length == 0 ? null : recipe;
    }

    public static string FormatReply(VisionResult result)
    {
        if (!result.Ok)
        {
            var message = result.ErrorCode == VisionErrorCode.InternalError
                ? "INTERNAL_ERROR"
                : Sanitize(result.Message);
            return $"ERR,{(int)result.ErrorCode},{message}";
        }

        var builder = new StringBuilder("OK");
        foreach (var pose in result.Poses)
        {
            if (!double.IsFinite(pose.X) || !double.IsFinite(pose.Y) || !double.IsFinite(pose.AngleDeg))
                return $"ERR,{(int)VisionErrorCode.InternalError},INVALID_POSE";
            builder.Append(CultureInfo.InvariantCulture, $",{pose.X:0.000},{pose.Y:0.000},{pose.AngleDeg:0.000}");
        }
        builder.Append(',').Append(result.RecipeName)
            .Append(CultureInfo.InvariantCulture, $",{result.Poses.Count},{result.ElapsedMs:0}");
        return builder.ToString();
    }

    public static string CoerceAlwaysOkReply(
        string reply,
        string requestLine,
        double elapsedMs,
        double defaultX = 0,
        double defaultY = 0,
        double defaultRz = 0)
    {
        if (!reply.StartsWith("ERR", StringComparison.Ordinal))
            return reply;

        var line = requestLine.Trim();
        if (string.Equals(line, "PING", StringComparison.OrdinalIgnoreCase))
            return "PONG";

        if (string.Equals(line, "STATUS", StringComparison.OrdinalIgnoreCase))
            return FormatStatus(null);

        if (IsClearInhibitCommand(line))
            return "OK,CLEARED";

        var (recipeKey, _, _) = ParseTriggerLine(line);
        var recipe = string.IsNullOrEmpty(recipeKey) ? "DEBUG" : recipeKey;
        return FormatReply(VisionResult.Success(
            recipe,
            [new RobotPose(defaultX, defaultY, defaultRz)],
            elapsedMs));
    }

    public static bool TryParseWhitelistEntry(string entry)
    {
        var e = entry.Trim();
        if (e.Length == 0)
            return false;

        if (e.EndsWith('*'))
        {
            var prefix = e[..^1].TrimEnd('.');
            var parts = prefix.Split('.');
            if (parts.Length is < 1 or > 3)
                return false;
            return parts.All(p => byte.TryParse(p, out _));
        }

        return IPAddress.TryParse(e, out _);
    }

    private static string NormalizeTriggerKey(string key)
    {
        key = key.Trim();
        if (key.StartsWith('#') && key.Length > 1)
            key = key[1..].Trim();
        return key;
    }

    private static bool TryParseFinite(string text, out double value)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
            double.IsFinite(value))
            return true;
        value = 0;
        return false;
    }

    private static string Sanitize(string message)
    {
        var cleaned = message.Replace(',', ' ').Replace('\n', ' ').Replace('\r', ' ');
        return new string(cleaned.Where(static c => c < 128).ToArray());
    }
}
