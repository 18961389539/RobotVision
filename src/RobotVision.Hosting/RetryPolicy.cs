using RobotVision.Core.Models;

namespace RobotVision.Hosting;

/// <summary>
/// TRIGGER 失败自动重拍策略：配置规范化 + 可重试错误码判定。
/// 仅抖动型失败（默认 1019 精修未过质量门、1007 未检出）值得重拍；
/// 标定/配置/联锁等确定性失败码重拍无意义，一律不重试。
/// </summary>
public sealed class RetryPolicy
{
    public bool Enabled { get; }

    /// <summary>最大尝试次数（含首次），钳制 [1,5]。</summary>
    public int MaxAttempts { get; }

    /// <summary>重拍间隔（ms），钳制 [0,5000]。0 = 立即重拍。</summary>
    public int DelayMs { get; }

    private readonly HashSet<int> _retryableCodes;

    public RetryPolicy(RetryConfig cfg)
    {
        Enabled = cfg.Enabled;
        MaxAttempts = Math.Clamp(cfg.MaxAttempts, 1, 5);
        DelayMs = Math.Clamp(cfg.DelayMs, 0, 5000);
        _retryableCodes = cfg.ErrorCodes is { Count: > 0 }
            ? new HashSet<int>(cfg.ErrorCodes)
            : [];
    }

    public bool IsRetryable(VisionErrorCode code) =>
        Enabled && _retryableCodes.Contains((int)code);
}
