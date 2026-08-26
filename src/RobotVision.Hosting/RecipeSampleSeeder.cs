namespace RobotVision.Hosting;

/// <summary>
/// 把仓库里的示例配方复制到运行时配方目录，且只在该目录尚未初始化时做一次。
/// 不能在每次启动/编译时按「缺文件就补」同步示例，否则界面删除的配方会在重启后被 MSBuild
/// CopyToOutputDirectory 或二次种子拷贝复活。
/// </summary>
public static class RecipeSampleSeeder
{
    public const string SamplesFolderName = "recipes.samples";
    public const string SeededMarkerFileName = ".seeded";

    /// <summary>
    /// 若 <paramref name="recipesFolder"/> 尚未种子化：目录里没有任何 json 时，从示例目录拷入；
    /// 然后写入 <see cref="SeededMarkerFileName"/>。之后删除配方不会被再次拷回。
    /// </summary>
    public static void SeedIfNeeded(string recipesFolder, string? samplesFolder = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipesFolder);
        Directory.CreateDirectory(recipesFolder);

        var marker = Path.Combine(recipesFolder, SeededMarkerFileName);
        if (File.Exists(marker))
            return;

        var hasRecipes = Directory.EnumerateFiles(recipesFolder, "*.json").Any();
        if (!hasRecipes)
        {
            samplesFolder ??= Path.Combine(AppContext.BaseDirectory, SamplesFolderName);
            if (Directory.Exists(samplesFolder))
            {
                foreach (var src in Directory.EnumerateFiles(samplesFolder, "*.json"))
                {
                    var dest = Path.Combine(recipesFolder, Path.GetFileName(src));
                    if (!File.Exists(dest))
                        File.Copy(src, dest);
                }
            }
        }

        File.WriteAllText(marker, "");
    }
}
