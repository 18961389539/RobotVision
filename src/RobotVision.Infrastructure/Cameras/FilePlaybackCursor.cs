namespace RobotVision.Infrastructure.Cameras;

/// <summary>File 相机最近一次取到的回放帧（Index 从 1 计）。</summary>
public readonly record struct FilePlaybackCursor(int Index, int Total, string FileName);
