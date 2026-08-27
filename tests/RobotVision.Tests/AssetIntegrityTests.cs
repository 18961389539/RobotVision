using Microsoft.Extensions.Logging.Abstractions;
using RobotVision.Core.Assets;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Inference;
using Xunit;

namespace RobotVision.Tests;

public class AssetIntegrityTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rv_asset_" + Guid.NewGuid().ToString("N"));

    public AssetIntegrityTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    [Fact]
    public void FileSha256_RoundTripAndNormalize()
    {
        var path = Path.Combine(_dir, "a.bin");
        File.WriteAllBytes(path, [1, 2, 3, 4]);
        var hash = FileSha256.ComputeFile(path);
        Assert.True(FileSha256.IsHex(hash));
        Assert.True(FileSha256.EqualsHex(hash, hash.ToUpperInvariant()));
        Assert.False(FileSha256.EqualsHex(hash, "00" + hash[2..]));
    }

    [Fact]
    public void Checker_DetectsModelHashMismatch()
    {
        var model = Path.Combine(_dir, "m.onnx");
        File.WriteAllBytes(model, [9, 9, 9]);
        var models = new ModelManager(_dir);
        var actual = models.ComputeSha256("m.onnx");
        var wrong = actual[0] == 'a' ? "b" + actual[1..] : "a" + actual[1..];

        var checker = new AssetIntegrityChecker(
            new AppConfig { AssetIntegrity = { Enabled = true } },
            models,
            new CalibrationManager(),
            NullLogger<AssetIntegrityChecker>.Instance);

        var recipe = new RecipeConfig
        {
            Name = "A01",
            CameraId = "cam",
            AngleMode = AngleMode.KeyPointLine,
            Models = ["m.onnx"],
            ModelSha256 = [wrong],
        };

        Assert.Equal("MODEL_HASH_MISMATCH", checker.Check(recipe));

        recipe.ModelSha256 = [actual];
        Assert.Null(checker.Check(recipe));
    }

    [Fact]
    public void ComputeSha256_CachesUntilFileMetadataChanges()
    {
        var model = Path.Combine(_dir, "cache.onnx");
        File.WriteAllBytes(model, [1, 2, 3, 4, 5]);
        var models = new ModelManager(_dir);

        var first = models.ComputeSha256("cache.onnx");
        Assert.Equal(1, models.Sha256FileReads);
        Assert.Equal(first, models.ComputeSha256("cache.onnx"));
        Assert.Equal(1, models.Sha256FileReads);

        File.WriteAllBytes(model, [9, 9, 9]);
        var second = models.ComputeSha256("cache.onnx");
        Assert.NotEqual(first, second);
        Assert.Equal(2, models.Sha256FileReads);
        Assert.Equal(second, models.ComputeSha256("cache.onnx"));
        Assert.Equal(2, models.Sha256FileReads);
    }

    [Fact]
    public void Checker_Disabled_Skips()
    {
        var checker = new AssetIntegrityChecker(
            new AppConfig { AssetIntegrity = { Enabled = false } },
            new ModelManager(_dir),
            new CalibrationManager(),
            NullLogger<AssetIntegrityChecker>.Instance);
        var recipe = new RecipeConfig
        {
            Models = ["missing.onnx"],
            ModelSha256 = ["aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"],
        };
        Assert.Null(checker.Check(recipe));
    }

    [Fact]
    public void StationFingerprint_ChangesAfterProfileEdit()
    {
        var cal = new CalibrationManager();
        cal.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st1",
            CameraId = "cam",
            Affine = [1, 0, 0, 0, 1, 0],
        });
        var a = cal.ComputeStationSha256("st1");
        Assert.False(string.IsNullOrEmpty(a));

        cal.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st1",
            CameraId = "cam",
            Affine = [1, 0, 1, 0, 1, 0],
        });
        var b = cal.ComputeStationSha256("st1");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void StationFingerprint_IgnoresQualityMetadata()
    {
        var cal = new CalibrationManager();
        cal.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st1",
            CameraId = "cam",
            Affine = [1, 0, 0, 0, 1, 0],
            Rms = 0.01,
            MaxResidual = 0.02,
            PointResiduals = [0.01, 0.02],
            CalibratedAt = new DateTime(2020, 1, 1),
        });
        var a = cal.ComputeStationSha256("st1");

        cal.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st1",
            CameraId = "cam",
            Affine = [1, 0, 0, 0, 1, 0],
            Rms = 9,
            MaxResidual = 9,
            PointResiduals = [9, 9, 9],
            CalibratedAt = new DateTime(2026, 8, 26),
        });
        Assert.Equal(a, cal.ComputeStationSha256("st1"));
    }

    [Fact]
    public void StationFingerprint_ExtrinsicIncludesIntrinsic()
    {
        var cal = new CalibrationManager();
        cal.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st1",
            CameraId = "cam",
            Affine = [1, 0, 0, 0, 1, 0],
            Width = 64,
            Height = 64,
        });
        cal.LoadIntrinsic(DummyIntrinsic("cam", 800));
        var withA = cal.ComputeStationSha256("st1", undistortCameraId: "cam");

        cal.LoadIntrinsic(DummyIntrinsic("cam", 900));
        var withB = cal.ComputeStationSha256("st1", undistortCameraId: "cam");
        Assert.NotEqual(withA, withB);

        var checker = new AssetIntegrityChecker(
            new AppConfig { AssetIntegrity = { Enabled = true } },
            new ModelManager(_dir),
            cal,
            NullLogger<AssetIntegrityChecker>.Instance);
        var recipe = new RecipeConfig
        {
            Name = "A01",
            CameraId = "cam",
            StationId = "st1",
            AngleMode = AngleMode.DualBlobCenterLine,
            StationSha256 = withA,
        };
        Assert.Equal("STATION_HASH_MISMATCH", checker.Check(recipe));

        recipe.StationSha256 = withB;
        Assert.Null(checker.Check(recipe));
    }

    private static IntrinsicProfile DummyIntrinsic(string cameraId, double fx) => new()
    {
        CameraId = cameraId,
        Width = 64,
        Height = 64,
        CameraMatrix = [fx, 0, 32, 0, fx, 32, 0, 0, 1],
        DistCoeffs = [0, 0, 0, 0, 0],
    };
}
