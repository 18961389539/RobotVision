using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using Xunit;

namespace RobotVision.Tests;

public sealed class RecipeLoaderTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "rv_recipes_" + Guid.NewGuid().ToString("N"));

    public RecipeLoaderTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void Get_ParsesRotationCompensationEnum()
    {
        File.WriteAllText(Path.Combine(_folder, "R01.json"), """
            {
              "cameraId": "cam1",
              "stationId": "st1",
              "angleMode": "KeyPointLine",
              "models": [ "m.onnx" ],
              "rotationCompensation": "EccentricTool"
            }
            """);

        var recipe = new RecipeLoader(_folder).Get("R01");

        Assert.Equal(RotationCompensationMode.EccentricTool, recipe.RotationCompensation);
        Assert.Equal(AngleMode.KeyPointLine, recipe.AngleMode);
        Assert.Equal("st1", recipe.StationId);
        Assert.Equal("R01", recipe.Name);
    }

    /// <summary>旧版平铺字段 JSON（子对象化前的格式）应仍能加载，setter-only 兼容属性迁移到子对象。</summary>
    [Fact]
    public void Get_LegacyFlatKeypointFields_MigrateToSubobjects()
    {
        File.WriteAllText(Path.Combine(_folder, "LEGACY.json"), """
            {
              "cameraId": "cam1",
              "angleMode": "KeyPointLine",
              "models": [ "m.onnx" ],
              "keypointIndexA": 2,
              "keypointIndexB": 5,
              "keypointMinConfidence": 0.4
            }
            """);

        var recipe = new RecipeLoader(_folder).Get("LEGACY");

        Assert.Equal(2, recipe.Keypoint.IndexA);
        Assert.Equal(5, recipe.Keypoint.IndexB);
        Assert.Equal(0.4, recipe.Keypoint.MinConfidence);
    }

    /// <summary>旧版平铺字段 JSON：pixelConfidence / pairingMaxDistancePx 同样迁移。</summary>
    [Fact]
    public void Get_LegacyFlatStrategyFields_MigrateToSubobjects()
    {
        File.WriteAllText(Path.Combine(_folder, "LEGACY2.json"), """
            {
              "cameraId": "cam1",
              "angleMode": "MaskMinAreaRect",
              "models": [ "m.onnx" ],
              "pixelConfidence": 0.8
            }
            """);
        File.WriteAllText(Path.Combine(_folder, "LEGACY3.json"), """
            {
              "cameraId": "cam1",
              "angleMode": "DualCenterLine",
              "models": [ "m1.onnx", "m2.onnx" ],
              "pairingMaxDistancePx": 123
            }
            """);

        var loader = new RecipeLoader(_folder);
        Assert.Equal(0.8, loader.Get("LEGACY2").Segmentation.PixelConfidence);
        Assert.Equal(123.0, loader.Get("LEGACY3").DualModel.PairingMaxDistancePx);
    }

    /// <summary>新格式子对象 JSON：保存后再次读取值一致（序列化往返）。</summary>
    [Fact]
    public void Save_RoundTripsSubobjects()
    {
        var loader = new RecipeLoader(_folder);
        var recipe = new RecipeConfig
        {
            Name = "RT",
            CameraId = "cam1",
            AngleMode = AngleMode.KeyPointLine,
            Models = ["m.onnx"],
            Keypoint = new KeypointOptions { IndexA = 1, IndexB = 3, MinConfidence = 0.55 },
            DualModel = new DualModelOptions { PairingMaxDistancePx = 222 },
            Segmentation = new SegmentationOptions { PixelConfidence = 0.77 },
        };
        loader.Save(recipe);

        var loaded = loader.Get("RT");
        Assert.Equal(1, loaded.Keypoint.IndexA);
        Assert.Equal(3, loaded.Keypoint.IndexB);
        Assert.Equal(0.55, loaded.Keypoint.MinConfidence);
        Assert.Equal(222.0, loaded.DualModel.PairingMaxDistancePx);
        Assert.Equal(0.77, loaded.Segmentation.PixelConfidence);
    }

    [Fact]
    public void Save_PersistsSerialNumber()
    {
        var loader = new RecipeLoader(_folder);
        var recipe = new RecipeConfig
        {
            Name = "SN1",
            CameraId = "cam1",
            AngleMode = AngleMode.KeyPointLine,
            Models = ["m.onnx"],
            SerialNumber = 1,
        };
        loader.Save(recipe);

        var json = File.ReadAllText(Path.Combine(_folder, "SN1.json"));
        Assert.Contains("\"SerialNumber\": 1", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, loader.Get("SN1", forceReload: true).SerialNumber);
    }

    [Fact]
    public void Get_MissingRotationCompensation_DefaultsToNone()
    {
        File.WriteAllText(Path.Combine(_folder, "R02.json"), """
            {
              "cameraId": "cam1",
              "angleMode": "MaskMinAreaRect",
              "models": [ "m.onnx" ]
            }
            """);

        var recipe = new RecipeLoader(_folder).Get("R02");

        Assert.Equal(RotationCompensationMode.None, recipe.RotationCompensation);
    }

    [Fact]
    public void Get_UnknownRecipe_Throws()
    {
        Assert.Throws<RecipeNotFoundException>(() => new RecipeLoader(_folder).Get("nope"));
    }

    /// <summary>文件被独占锁定（IO 故障）不得伪装成"配方不存在"（1001），应报 1099 内部错误——
    /// 否则文件锁/磁盘满/网络盘瞬断会把排障导向"配方名错了"的错误方向。</summary>
    [Fact]
    public void Get_FileLockedIoFault_ThrowsInternalError_NotNotFound()
    {
        var path = Path.Combine(_folder, "LOCKED.json");
        File.WriteAllText(path, """{ "cameraId": "cam", "models": ["m.onnx"] }""");

        using var lockStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        var ex = Assert.Throws<InvalidRecipeException>(() => new RecipeLoader(_folder).Get("LOCKED"));
        Assert.Equal(VisionErrorCode.InternalError, ex.ErrorCode);
    }

    /// <summary>EccentricTool 补偿缺 stationId 时补偿会被静默跳过（声称补偿实际没补偿），
    /// 必须在加载期拦截而不是留到运行期。</summary>
    [Fact]
    public void Validate_EccentricToolWithoutStationId_Throws()
    {
        var recipe = new RecipeConfig
        {
            Name = "ECC",
            CameraId = "cam",
            Models = ["m.onnx"],
            RotationCompensation = RotationCompensationMode.EccentricTool,
            StationId = "",
        };

        var ex = Assert.Throws<InvalidRecipeException>(() => RecipeLoader.Validate(recipe));
        Assert.Contains("stationId", ex.Message);
    }

    // ---- 改进项 3：缓存按 LastWriteTime 失效（外部改动可感知） ----

    [Fact]
    public void Get_ExternalFileChange_ReloadsFromDisk()
    {
        var path = Path.Combine(_folder, "A01.json");
        File.WriteAllText(path, """{ "cameraId": "cam", "models": ["m.onnx"] }""");
        var loader = new RecipeLoader(_folder);
        Assert.Equal("cam", loader.Get("A01").CameraId);

        // 模拟外部工具改动文件：内容与修改时间都变化。
        // 必须先写内容再前移时间戳：WriteAllText 会重置 LastWriteTime，
        // 顺序颠倒时两次写入可能落在同一时间戳粒度内，缓存判定未变化导致偶发失败
        File.WriteAllText(path, """{ "cameraId": "cam2", "models": ["m.onnx"] }""");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(5));

        Assert.Equal("cam2", loader.Get("A01").CameraId);
    }

    [Fact]
    public void Get_UnchangedFile_HitsCache()
    {
        var path = Path.Combine(_folder, "C01.json");
        File.WriteAllText(path, """{ "cameraId": "cam", "models": ["m.onnx"] }""");
        var loader = new RecipeLoader(_folder);
        var first = loader.Get("C01");

        // 缓存命中返回的是克隆副本（防调用方修改污染缓存），内容与缓存一致
        var second = loader.Get("C01");
        Assert.NotSame(first, second);
        Assert.Equal(first.CameraId, second.CameraId);
        Assert.Equal(first.Models, second.Models);
    }

    [Fact]
    public void Get_ForceReload_IgnoresCache()
    {
        var path = Path.Combine(_folder, "F01.json");
        File.WriteAllText(path, """{ "cameraId": "cam", "models": ["m.onnx"] }""");
        var loader = new RecipeLoader(_folder);
        loader.Get("F01");

        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(3));
        File.WriteAllText(path, """{ "cameraId": "cam3", "models": ["m.onnx"] }""");

        Assert.Equal("cam3", loader.Get("F01", forceReload: true).CameraId);
    }

    // ---- 改进项 1：重名覆盖确认的基础能力 FileExists ----

    [Fact]
    public void FileExists_ReflectsDiskState()
    {
        var loader = new RecipeLoader(_folder);
        Assert.False(loader.FileExists("nope"));

        File.WriteAllText(Path.Combine(_folder, "E01.json"), """{ "cameraId": "cam", "models": ["m.onnx"] }""");
        Assert.True(loader.FileExists("E01"));
    }

    // ---- 改进项 9：SchemaVersion / Description / Enabled 新字段 ----

    [Fact]
    public void Get_ParsesNewFields()
    {
        File.WriteAllText(Path.Combine(_folder, "N01.json"), """
            {
              "cameraId": "cam",
              "models": ["m.onnx"],
              "schemaVersion": 2,
              "description": "产线 3 号工位",
              "enabled": false
            }
            """);

        var recipe = new RecipeLoader(_folder).Get("N01");

        Assert.Equal(2, recipe.SchemaVersion);
        Assert.Equal("产线 3 号工位", recipe.Description);
        Assert.False(recipe.Enabled);
    }

    [Fact]
    public void Get_NewFields_DefaultValues()
    {
        File.WriteAllText(Path.Combine(_folder, "N02.json"), """{ "cameraId": "cam", "models": ["m.onnx"] }""");

        var recipe = new RecipeLoader(_folder).Get("N02");

        Assert.Equal(1, recipe.SchemaVersion);
        Assert.Equal("", recipe.Description);
        Assert.True(recipe.Enabled);
    }

    // ---- 改进项 2：引用完整性校验器（由组装层注入） ----

    [Fact]
    public void ReferenceValidator_RejectsMissingReference()
    {
        File.WriteAllText(Path.Combine(_folder, "R03.json"), """{ "cameraId": "cam", "models": ["m.onnx"] }""");
        var loader = new RecipeLoader(_folder)
        {
            ReferenceValidator = r => r.CameraId == "cam"
                ? new RecipeReferenceError("相机未注册: cam", VisionErrorCode.CameraNotRegistered)
                : null,
        };

        var ex = Assert.Throws<InvalidRecipeException>(() => loader.Get("R03"));
        Assert.Contains("相机未注册", ex.Message);
        Assert.Equal(VisionErrorCode.CameraNotRegistered, ex.ErrorCode);
    }

    [Fact]
    public void ReferenceValidator_NotInjected_AllowsAny()
    {
        File.WriteAllText(Path.Combine(_folder, "R04.json"), """{ "cameraId": "cam", "models": ["m.onnx"] }""");

        var recipe = new RecipeLoader(_folder).Get("R04");

        Assert.NotNull(recipe);
    }

    [Fact]
    public void Get_CacheHit_RevalidatesReferences()
    {
        File.WriteAllText(Path.Combine(_folder, "R05.json"), """{ "cameraId": "cam", "models": ["m.onnx"] }""");
        var allow = true;
        var loader = new RecipeLoader(_folder)
        {
            ReferenceValidator = _ => allow
                ? null
                : new RecipeReferenceError("工位未做外参/多项式标定: st1", VisionErrorCode.NotCalibrated),
        };

        Assert.Equal("cam", loader.Get("R05").CameraId);
        allow = false;
        var ex = Assert.Throws<InvalidRecipeException>(() => loader.Get("R05"));
        Assert.Equal(VisionErrorCode.NotCalibrated, ex.ErrorCode);
    }

    [Fact]
    public void ResolveTriggerKey_ByName()
    {
        File.WriteAllText(Path.Combine(_folder, "A01.json"), """
            { "serialNumber": 3, "cameraId": "cam", "models": ["m.onnx"] }
            """);
        var loader = new RecipeLoader(_folder);

        var (name, error) = loader.ResolveTriggerKey("A01");
        Assert.Null(error);
        Assert.Equal("A01", name);
    }

    [Fact]
    public void ResolveTriggerKey_BySerial()
    {
        File.WriteAllText(Path.Combine(_folder, "A01.json"), """
            { "serialNumber": 3, "cameraId": "cam", "models": ["m.onnx"] }
            """);
        var loader = new RecipeLoader(_folder);

        Assert.Equal("A01", loader.ResolveTriggerKey("3").RecipeName);
        Assert.Equal("A01", loader.ResolveTriggerKey("#3").RecipeName);
    }

    [Fact]
    public void ResolveTriggerKey_UnknownSerial_ReturnsError()
    {
        File.WriteAllText(Path.Combine(_folder, "A01.json"), """
            { "serialNumber": 3, "cameraId": "cam", "models": ["m.onnx"] }
            """);
        var loader = new RecipeLoader(_folder);

        var (_, error) = loader.ResolveTriggerKey("9");
        Assert.Equal("UNKNOWN_SERIAL", error);
    }

    [Fact]
    public void ResolveTriggerKey_BySerial_SkipsUnloadableSibling()
    {
        File.WriteAllText(Path.Combine(_folder, "BAD.json"), "{ not-json");
        File.WriteAllText(Path.Combine(_folder, "GOOD.json"), """
            { "serialNumber": 2, "cameraId": "cam", "angleMode": "KeyPointLine", "models": ["m.onnx"] }
            """);
        var loader = new RecipeLoader(_folder);

        Assert.Equal("GOOD", loader.ResolveTriggerKey("#2").RecipeName);
        Assert.Equal("GOOD", loader.ResolveTriggerKey("2").RecipeName);
    }

    [Fact]
    public void Save_RejectsDuplicateSerialNumber()
    {
        File.WriteAllText(Path.Combine(_folder, "A01.json"), """
            { "serialNumber": 1, "cameraId": "cam", "models": ["m.onnx"] }
            """);
        File.WriteAllText(Path.Combine(_folder, "B02.json"), """
            { "serialNumber": 2, "cameraId": "cam", "models": ["m.onnx"] }
            """);
        var loader = new RecipeLoader(_folder);
        var recipe = loader.Get("B02");
        recipe.SerialNumber = 1;

        var ex = Assert.Throws<InvalidRecipeException>(() => loader.Save(recipe));
        Assert.Contains("serialNumber 1", ex.Message);
    }

    [Fact]
    public void Save_SerialNumber_IgnoresUnloadableSibling()
    {
        File.WriteAllText(Path.Combine(_folder, "GOOD.json"), """
            { "cameraId": "cam", "models": ["m.onnx"], "angleMode": "KeyPointLine", "keypointIndexA": 0, "keypointIndexB": 1 }
            """);
        File.WriteAllText(Path.Combine(_folder, "BROKEN.json"), """
            { "cameraId": "cam", "angleMode": "UnknownMode" }
            """);
        var loader = new RecipeLoader(_folder);
        var recipe = loader.Get("GOOD");
        recipe.SerialNumber = 1;

        loader.Save(recipe);
        Assert.Equal(1, loader.Get("GOOD", forceReload: true).SerialNumber);
    }

    [Fact]
    public void Save_Rename_RemovesPreviousFile()
    {
        File.WriteAllText(Path.Combine(_folder, "A01.json"), """
            { "cameraId": "cam", "models": ["m.onnx"], "angleMode": "KeyPointLine", "keypointIndexA": 0, "keypointIndexB": 1 }
            """);
        var loader = new RecipeLoader(_folder);
        var recipe = loader.Get("A01");
        recipe.Name = "A01b";

        loader.Save(recipe, previousName: "A01");

        Assert.False(File.Exists(Path.Combine(_folder, "A01.json")));
        Assert.True(File.Exists(Path.Combine(_folder, "A01b.json")));
        Assert.Equal(new[] { "A01b" }, loader.ListNames());
        Assert.Equal("cam", loader.Get("A01b", forceReload: true).CameraId);
    }

    [Fact]
    public void Save_Rename_KeepsSameSerialNumber()
    {
        File.WriteAllText(Path.Combine(_folder, "A01.json"), """
            { "serialNumber": 1, "cameraId": "cam", "models": ["m.onnx"], "angleMode": "KeyPointLine", "keypointIndexA": 0, "keypointIndexB": 1 }
            """);
        var loader = new RecipeLoader(_folder);
        var recipe = loader.Get("A01");
        recipe.Name = "A01b";

        loader.Save(recipe, previousName: "A01");

        Assert.False(File.Exists(Path.Combine(_folder, "A01.json")));
        Assert.Equal(1, loader.Get("A01b", forceReload: true).SerialNumber);
    }

    [Fact]
    public void Save_Copy_WithoutPreviousName_LeavesSource()
    {
        File.WriteAllText(Path.Combine(_folder, "A01.json"), """
            { "cameraId": "cam", "models": ["m.onnx"], "angleMode": "KeyPointLine", "keypointIndexA": 0, "keypointIndexB": 1 }
            """);
        var loader = new RecipeLoader(_folder);
        var copy = loader.Get("A01");
        copy.Name = "A01_copy";

        loader.Save(copy);

        Assert.True(File.Exists(Path.Combine(_folder, "A01.json")));
        Assert.True(File.Exists(Path.Combine(_folder, "A01_copy.json")));
    }

    [Fact]
    public void Delete_RemovesFileAndListEntry()
    {
        File.WriteAllText(Path.Combine(_folder, "A02.json"), """
            { "cameraId": "cam", "models": ["m.onnx"], "angleMode": "KeyPointLine", "keypointIndexA": 0, "keypointIndexB": 1 }
            """);
        var loader = new RecipeLoader(_folder);
        Assert.Contains("A02", loader.ListNames());

        Assert.True(loader.Delete("A02"));
        Assert.False(File.Exists(Path.Combine(_folder, "A02.json")));
        Assert.DoesNotContain("A02", loader.ListNames());
        Assert.False(loader.Delete("A02"));
    }
}
