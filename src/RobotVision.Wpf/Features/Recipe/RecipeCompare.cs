using System.Text.Json;
using System.Text.Json.Serialization;
using RobotVision.Core.Recipe;

namespace RobotVision.WpfHost.Features.Recipe;

/// <summary>配方比较（脏标记）：序列化比对，新增字段时不必手写清单。</summary>
internal static class RecipeCompare
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static bool Same(RecipeConfig a, RecipeConfig b) =>
        JsonSerializer.Serialize(a, Options) == JsonSerializer.Serialize(b, Options);
}
