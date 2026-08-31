namespace RobotVision.WpfHost.Features.Cameras;

public sealed record CameraListItem(
    string Id,
    string Title,
    string? Subtitle,
    string Type,
    string Summary,
    string Status,
    bool Registered,
    string? UnregisteredReason = null);
