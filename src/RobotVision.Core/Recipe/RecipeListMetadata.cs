using RobotVision.Core.Models;

namespace RobotVision.Core.Recipe;

/// <summary>配方列表项元数据（不读 templateImageBase64，供 UI 列表摘要与轻量校验）。</summary>
public sealed class RecipeListMetadata
{
    public required string Name { get; init; }
    public bool ParseSucceeded { get; init; }
    public string? ParseError { get; init; }
    public bool Enabled { get; init; } = true;
    public string? Description { get; init; }
    public AngleMode AngleMode { get; init; }
    public SegmentRefineMethod RefineMethod { get; init; }
    public string CameraId { get; init; } = "";
    public string PrimaryModel { get; init; } = "";
    public int SerialNumber { get; init; }
    public string? StationId { get; init; }
    public bool HasDetectionRoi { get; init; }
    public bool HasFeatureRoi { get; init; }
    public bool HasTemplateImage { get; init; }
    public bool HasLighting { get; init; }
    public string? LightControllerId { get; init; }
    public bool HasOutputOffset { get; init; }
    public bool HasModelPin { get; init; }
    public bool HasStationPin { get; init; }

    public static RecipeListMetadata ParseFailed(string name, string error) => new()
    {
        Name = name,
        ParseSucceeded = false,
        ParseError = error,
    };

    /// <summary>供 <see cref="RecipeLoader.Validate"/> / 引用校验用的最小配方壳（不携带模板图）。</summary>
    public RecipeConfig ToValidationStub()
    {
        var template = new TemplateOptions
        {
            RefineMethod = RefineMethod,
            TemplateImageBase64 = HasTemplateImage ? "." : "",
            Roi = HasFeatureRoi ? new Roi(0.25, 0.25, 0.5, 0.5) : null,
        };
        InstanceGeometry.EnsureRatioDefaults(template);

        return new RecipeConfig
        {
            Name = Name,
            Enabled = Enabled,
            Description = Description ?? "",
            SerialNumber = SerialNumber,
            CameraId = CameraId,
            StationId = StationId ?? "",
            AngleMode = AngleMode,
            Models = string.IsNullOrWhiteSpace(PrimaryModel) ? [] : [PrimaryModel],
            Roi = HasDetectionRoi ? new Roi(0.1, 0.1, 0.8, 0.8) : null,
            Template = template,
            LightControllerId = LightControllerId ?? "",
            Lighting = HasLighting ? new LightingConfig() : null,
            OutputOffset = HasOutputOffset ? new OutputOffsetOptions { X = 1 } : new(),
            ModelSha256 = HasModelPin ? ["."] : [],
            StationSha256 = HasStationPin ? "." : null,
        };
    }
}
