using RobotVision.Hosting;
using Xunit;

namespace RobotVision.Tests;

public sealed class RecipeSampleSeederTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "rv_seed_" + Guid.NewGuid().ToString("N"));

    public RecipeSampleSeederTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, true); }
        catch (IOException) { }
    }

    [Fact]
    public void SeedIfNeeded_CopiesSamplesOnce_ThenHonorsDelete()
    {
        var samples = Path.Combine(_root, "samples");
        var recipes = Path.Combine(_root, "recipes");
        Directory.CreateDirectory(samples);
        File.WriteAllText(Path.Combine(samples, "A01.json"), """{"cameraId":"cam"}""");
        File.WriteAllText(Path.Combine(samples, "A02.json"), """{"cameraId":"cam"}""");

        RecipeSampleSeeder.SeedIfNeeded(recipes, samples);
        Assert.True(File.Exists(Path.Combine(recipes, "A02.json")));
        Assert.True(File.Exists(Path.Combine(recipes, RecipeSampleSeeder.SeededMarkerFileName)));

        File.Delete(Path.Combine(recipes, "A02.json"));
        RecipeSampleSeeder.SeedIfNeeded(recipes, samples);

        Assert.False(File.Exists(Path.Combine(recipes, "A02.json")));
        Assert.True(File.Exists(Path.Combine(recipes, "A01.json")));
    }

    [Fact]
    public void SeedIfNeeded_ExistingRecipes_DoesNotFillMissingSample()
    {
        var samples = Path.Combine(_root, "samples");
        var recipes = Path.Combine(_root, "recipes");
        Directory.CreateDirectory(samples);
        Directory.CreateDirectory(recipes);
        File.WriteAllText(Path.Combine(samples, "A02.json"), """{"cameraId":"cam"}""");
        File.WriteAllText(Path.Combine(recipes, "A01.json"), """{"cameraId":"cam"}""");

        RecipeSampleSeeder.SeedIfNeeded(recipes, samples);

        Assert.False(File.Exists(Path.Combine(recipes, "A02.json")));
        Assert.True(File.Exists(Path.Combine(recipes, RecipeSampleSeeder.SeededMarkerFileName)));
    }

    [Fact]
    public void ResolveAndPrepareRecipesFolder_DoesNotUseCwdSourceTree()
    {
        var live = Path.Combine(_root, "live");
        var cfg = new AppConfig { RecipesFolder = live };
        var resolved = cfg.ResolveAndPrepareRecipesFolder();
        Assert.Equal(Path.GetFullPath(live), resolved);
        Assert.True(File.Exists(Path.Combine(live, RecipeSampleSeeder.SeededMarkerFileName)));
    }
}
