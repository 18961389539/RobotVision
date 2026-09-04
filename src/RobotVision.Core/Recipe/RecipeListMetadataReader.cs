using System.Text.Json;

namespace RobotVision.Core.Recipe;

/// <summary>流式读取配方 JSON 列表元数据，跳过 <c>templateImageBase64</c> 大字段。</summary>
internal static class RecipeListMetadataReader
{
    public static RecipeListMetadata Read(string path, string name)
    {
        var bytes = File.ReadAllBytes(path);
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });

        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            return RecipeListMetadata.ParseFailed(name, "配方 JSON 根节点不是对象");

        var meta = new Builder(name);
        ReadObject(ref reader, meta, depth: 0);
        return meta.Build();
    }

    private static void ReadObject(ref Utf8JsonReader reader, Builder meta, int depth)
    {
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return;

            if (reader.TokenType != JsonTokenType.PropertyName)
                continue;

            if (depth == 0)
            {
                if (NameIs(ref reader, "enabled"u8))
                {
                    reader.Read();
                    meta.Enabled = reader.TokenType == JsonTokenType.True;
                    continue;
                }

                if (NameIs(ref reader, "description"u8))
                {
                    reader.Read();
                    meta.Description = ReadString(ref reader);
                    continue;
                }

                if (NameIs(ref reader, "serialNumber"u8))
                {
                    reader.Read();
                    meta.SerialNumber = reader.TokenType == JsonTokenType.Number ? reader.GetInt32() : 0;
                    continue;
                }

                if (NameIs(ref reader, "cameraId"u8))
                {
                    reader.Read();
                    meta.CameraId = ReadString(ref reader) ?? "";
                    continue;
                }

                if (NameIs(ref reader, "stationId"u8))
                {
                    reader.Read();
                    meta.StationId = ReadString(ref reader);
                    continue;
                }

                if (NameIs(ref reader, "angleMode"u8))
                {
                    reader.Read();
                    meta.AngleMode = ReadEnum(ref reader, AngleMode.MaskMinAreaRect);
                    continue;
                }

                if (NameIs(ref reader, "models"u8))
                {
                    ReadModels(ref reader, meta);
                    continue;
                }

                if (NameIs(ref reader, "roi"u8))
                {
                    reader.Read();
                    meta.HasDetectionRoi = reader.TokenType == JsonTokenType.StartObject;
                    if (meta.HasDetectionRoi)
                        SkipValue(ref reader);
                    continue;
                }

                if (NameIs(ref reader, "template"u8))
                {
                    ReadTemplate(ref reader, meta);
                    continue;
                }

                if (NameIs(ref reader, "dualTemplate"u8))
                {
                    ReadDualTemplate(ref reader, meta);
                    continue;
                }

                if (NameIs(ref reader, "lightControllerId"u8))
                {
                    reader.Read();
                    meta.LightControllerId = ReadString(ref reader);
                    continue;
                }

                if (NameIs(ref reader, "lighting"u8))
                {
                    reader.Read();
                    meta.HasLighting = reader.TokenType == JsonTokenType.StartObject;
                    if (meta.HasLighting)
                        SkipValue(ref reader);
                    continue;
                }

                if (NameIs(ref reader, "outputOffset"u8))
                {
                    ReadOutputOffset(ref reader, meta);
                    continue;
                }

                if (NameIs(ref reader, "modelSha256"u8))
                {
                    ReadModelPins(ref reader, meta);
                    continue;
                }

                if (NameIs(ref reader, "stationSha256"u8))
                {
                    reader.Read();
                    var pin = ReadString(ref reader);
                    meta.HasStationPin = !string.IsNullOrWhiteSpace(pin);
                    continue;
                }
            }

            reader.Read();
            SkipValue(ref reader);
        }
    }

    private static void ReadTemplate(ref Utf8JsonReader reader, Builder meta)
    {
        reader.Read();
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            SkipValue(ref reader);
            return;
        }

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return;

            if (reader.TokenType != JsonTokenType.PropertyName)
                continue;

            if (NameIs(ref reader, "refineMethod"u8))
            {
                reader.Read();
                meta.RefineMethod = ReadEnum(ref reader, SegmentRefineMethod.Template);
                continue;
            }

            if (NameIs(ref reader, "templateImageBase64"u8))
            {
                reader.Read();
                meta.HasTemplateImage = reader.TokenType == JsonTokenType.String && reader.ValueSpan.Length > 0;
                continue;
            }

            if (NameIs(ref reader, "roi"u8))
            {
                reader.Read();
                meta.HasFeatureRoi = reader.TokenType == JsonTokenType.StartObject;
                if (meta.HasFeatureRoi)
                    SkipValue(ref reader);
                continue;
            }

            reader.Read();
            SkipValue(ref reader);
        }
    }

    private static void ReadDualTemplate(ref Utf8JsonReader reader, Builder meta)
    {
        reader.Read();
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            SkipValue(ref reader);
            return;
        }

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return;

            if (reader.TokenType != JsonTokenType.PropertyName)
                continue;

            if (NameIs(ref reader, "templateABase64"u8))
            {
                reader.Read();
                meta.HasDualTemplateA = reader.TokenType == JsonTokenType.String && reader.ValueSpan.Length > 0;
                continue;
            }

            if (NameIs(ref reader, "templateBBase64"u8))
            {
                reader.Read();
                meta.HasDualTemplateB = reader.TokenType == JsonTokenType.String && reader.ValueSpan.Length > 0;
                continue;
            }

            reader.Read();
            SkipValue(ref reader);
        }
    }

    private static void ReadModels(ref Utf8JsonReader reader, Builder meta)
    {
        reader.Read();
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            SkipValue(ref reader);
            return;
        }

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                return;
            if (reader.TokenType == JsonTokenType.String && meta.PrimaryModel.Length == 0)
                meta.PrimaryModel = reader.GetString() ?? "";
            else
                SkipValue(ref reader);
        }
    }

    private static void ReadOutputOffset(ref Utf8JsonReader reader, Builder meta)
    {
        reader.Read();
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            SkipValue(ref reader);
            return;
        }

        double x = 0, y = 0, rz = 0;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType != JsonTokenType.PropertyName)
                continue;

            if (NameIs(ref reader, "x"u8))
            {
                reader.Read();
                x = ReadDouble(ref reader);
                continue;
            }

            if (NameIs(ref reader, "y"u8))
            {
                reader.Read();
                y = ReadDouble(ref reader);
                continue;
            }

            if (NameIs(ref reader, "rzDeg"u8))
            {
                reader.Read();
                rz = ReadDouble(ref reader);
                continue;
            }

            reader.Read();
            SkipValue(ref reader);
        }

        meta.HasOutputOffset = x != 0 || y != 0 || rz != 0;
    }

    private static void ReadModelPins(ref Utf8JsonReader reader, Builder meta)
    {
        reader.Read();
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            SkipValue(ref reader);
            return;
        }

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                return;
            if (reader.TokenType == JsonTokenType.String && reader.ValueSpan.Length > 0)
                meta.HasModelPin = true;
        }
    }

    /// <summary>
    /// 手工/测试 JSON 常用 camelCase；<see cref="RecipeLoader.Save"/> 默认按 CLR 名写 PascalCase。
    /// <see cref="Utf8JsonReader.ValueTextEquals(ReadOnlySpan{byte})"/> 区分大小写，两种都认。
    /// </summary>
    private static bool NameIs(ref Utf8JsonReader reader, ReadOnlySpan<byte> camel)
    {
        if (reader.ValueTextEquals(camel))
            return true;
        if (camel.Length == 0)
            return false;

        Span<byte> pascal = stackalloc byte[camel.Length];
        camel.CopyTo(pascal);
        if (pascal[0] is >= (byte)'a' and <= (byte)'z')
            pascal[0] -= (byte)'a' - (byte)'A';
        return reader.ValueTextEquals(pascal);
    }

    private static TEnum ReadEnum<TEnum>(ref Utf8JsonReader reader, TEnum fallback)
        where TEnum : struct, Enum
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            SkipValue(ref reader);
            return fallback;
        }

        var text = reader.GetString();
        return Enum.TryParse<TEnum>(text, ignoreCase: true, out var value) ? value : fallback;
    }

    private static string? ReadString(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.String ? reader.GetString() : null;

    private static double ReadDouble(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.Number ? reader.GetDouble() : 0;

    private static void SkipValue(ref Utf8JsonReader reader)
    {
        if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
        {
            var depth = 0;
            do
            {
                if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                    depth++;
                else if (reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
                    depth--;
            }
            while (depth > 0 && reader.Read());
        }
    }

    private sealed class Builder(string name)
    {
        public string Name { get; } = name;
        public bool Enabled = true;
        public string? Description;
        public AngleMode AngleMode;
        public SegmentRefineMethod RefineMethod;
        public string CameraId = "";
        public string PrimaryModel = "";
        public int SerialNumber;
        public string? StationId;
        public bool HasDetectionRoi;
        public bool HasFeatureRoi;
        public bool HasTemplateImage;
        public bool HasDualTemplateA;
        public bool HasDualTemplateB;
        public bool HasLighting;
        public string? LightControllerId;
        public bool HasOutputOffset;
        public bool HasModelPin;
        public bool HasStationPin;

        public RecipeListMetadata Build() => new()
        {
            Name = Name,
            ParseSucceeded = true,
            Enabled = Enabled,
            Description = Description,
            AngleMode = AngleMode,
            RefineMethod = RefineMethod,
            CameraId = CameraId,
            PrimaryModel = PrimaryModel,
            SerialNumber = SerialNumber,
            StationId = StationId,
            HasDetectionRoi = HasDetectionRoi,
            HasFeatureRoi = HasFeatureRoi,
            HasTemplateImage = HasTemplateImage,
            HasDualTemplateA = HasDualTemplateA,
            HasDualTemplateB = HasDualTemplateB,
            HasLighting = HasLighting,
            LightControllerId = LightControllerId,
            HasOutputOffset = HasOutputOffset,
            HasModelPin = HasModelPin,
            HasStationPin = HasStationPin,
        };
    }
}
