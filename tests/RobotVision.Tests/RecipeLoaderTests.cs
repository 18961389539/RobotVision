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
}
