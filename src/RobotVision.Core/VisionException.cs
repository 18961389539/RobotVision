using RobotVision.Core.Models;

namespace RobotVision.Core;

/// <summary>携带错误码的业务异常，由 VisionService 统一转换为 VisionResult。</summary>
public sealed class VisionException : Exception
{
    public VisionErrorCode ErrorCode { get; }

    public VisionException(VisionErrorCode code, string message) : base(message) => ErrorCode = code;

    public VisionException(VisionErrorCode code, string message, Exception inner) : base(message, inner) =>
        ErrorCode = code;
}
