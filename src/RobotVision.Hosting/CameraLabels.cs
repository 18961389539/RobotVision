namespace RobotVision.Hosting;

/// <summary>相机 Id / 可选显示名的界面文案。</summary>
public static class CameraLabels
{
  public static string ListTitle(CameraConfig camera)
  {
    var name = camera.Name.Trim();
    return name.Length > 0 ? name : camera.Id;
  }

  public static string ComboLabel(CameraConfig camera)
  {
    var name = camera.Name.Trim();
    return name.Length > 0 ? $"{name} ({camera.Id})" : camera.Id;
  }

  public static string ComboLabel(string id, IEnumerable<CameraConfig> cameras)
  {
    var camera = cameras.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
    return camera is null ? id : ComboLabel(camera);
  }
}

/// <summary>相机下拉项：值仍为稳定 Id，Label 供界面展示。</summary>
public sealed record CameraOption(string Id, string Label)
{
  public static IReadOnlyList<CameraOption> FromRegistered(
    IEnumerable<CameraConfig> configs,
    IEnumerable<string> registeredIds)
  {
    var byId = configs.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);
    return registeredIds
      .Select(id => byId.TryGetValue(id, out var camera)
        ? new CameraOption(id, CameraLabels.ComboLabel(camera))
        : new CameraOption(id, id))
      .ToList();
  }
}
