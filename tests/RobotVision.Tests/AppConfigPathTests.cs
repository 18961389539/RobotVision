using RobotVision.Hosting;
using Xunit;

namespace RobotVision.Tests;

public class AppConfigPathTests
{
    [Theory]
    [InlineData("recipes")]
    [InlineData("data/calibration")]
    public void ResolveBase_RelativePath_AnchorsToBaseDirectory(string relative)
    {
        var resolved = AppConfigExtensions.ResolveBase(relative);
        var expected = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, relative));
        Assert.Equal(expected, resolved);
    }

    [Theory]
    [InlineData(@"D:\absolute\path")]
    [InlineData(@"\\server\share\folder")]
    public void ResolveBase_AbsolutePath_ReturnedUnchanged(string absolute)
        => Assert.Equal(absolute, AppConfigExtensions.ResolveBase(absolute));

    [Fact]
    public void ResolveBase_EmptyPath_ReturnedAsIs()
    {
        Assert.Equal("", AppConfigExtensions.ResolveBase(""));
        Assert.Equal(" ", AppConfigExtensions.ResolveBase(" "));
    }

    [Fact]
    public void ResolveFolder_DeploymentLayout_WinsOverCwd()
    {
        // 测试宿主默认 CWD == BaseDirectory，先切到独立目录让两个锚点分开
        var original = Environment.CurrentDirectory;
        var isolated = Path.Combine(Path.GetTempPath(), "rv_iso_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(isolated);
        try
        {
            Environment.CurrentDirectory = isolated;

            // exe 目录下存在 → 用 exe 目录，即使 CWD 下也有同名目录
            var name = "rv_shared_" + Guid.NewGuid().ToString("N");
            var exeDir = Path.Combine(AppContext.BaseDirectory, name);
            var cwdDir = Path.Combine(isolated, name);
            Directory.CreateDirectory(exeDir);
            Directory.CreateDirectory(cwdDir);
            try
            {
                Assert.Equal(exeDir, AppConfigExtensions.ResolveFolder(name));
            }
            finally
            {
                Directory.Delete(exeDir);
                Directory.Delete(cwdDir);
            }
        }
        finally
        {
            Environment.CurrentDirectory = original;
            Directory.Delete(isolated);
        }
    }

    [Fact]
    public void ResolveFolder_DevLayout_FallsBackToCwdAndNotifies()
    {
        var original = Environment.CurrentDirectory;
        var isolated = Path.Combine(Path.GetTempPath(), "rv_iso_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(isolated);
        try
        {
            Environment.CurrentDirectory = isolated;

            // exe 目录没有、CWD 有（dotnet run 从仓库根启动）→ 用 CWD 并回调
            var name = "rv_cwd_" + Guid.NewGuid().ToString("N");
            var cwdDir = Path.Combine(isolated, name);
            Directory.CreateDirectory(cwdDir);
            try
            {
                var notified = false;
                var resolved = AppConfigExtensions.ResolveFolder(name, p => notified = true);
                Assert.Equal(cwdDir, resolved);
                Assert.True(notified);
            }
            finally
            {
                Directory.Delete(cwdDir);
            }
        }
        finally
        {
            Environment.CurrentDirectory = original;
            Directory.Delete(isolated);
        }
    }

    [Fact]
    public void ResolveFolder_NeitherExists_AnchorsToExeDirectory()
    {
        var name = "rv_missing_" + Guid.NewGuid().ToString("N");
        var resolved = AppConfigExtensions.ResolveFolder(name);
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, name), resolved);
    }

    [Fact]
    public void ResolveFolder_AbsolutePath_ReturnedUnchanged()
    {
        Assert.Equal(@"D:\abs", AppConfigExtensions.ResolveFolder(@"D:\abs"));
        Assert.Equal("", AppConfigExtensions.ResolveFolder(""));
    }

    [Fact]
    public void ResolveFolder_IndependentOfSystem32LikeCwd()
    {
        // CWD 切到任意目录（模拟服务启动）后：exe 目录存在仍优先
        var original = Environment.CurrentDirectory;
        var name = "rv_svc_" + Guid.NewGuid().ToString("N");
        var exeDir = Path.Combine(AppContext.BaseDirectory, name);
        Directory.CreateDirectory(exeDir);
        try
        {
            Environment.CurrentDirectory = Path.GetTempPath();
            Assert.Equal(exeDir, AppConfigExtensions.ResolveFolder(name));
        }
        finally
        {
            Environment.CurrentDirectory = original;
            Directory.Delete(exeDir);
        }
    }

    [Fact]
    public void ConvenienceMethods_UseFolderResolution()
    {
        var cfg = new AppConfig
        {
            RecipesFolder = "recipes",
            ModelsFolder = "models",
            CalibrationFolder = "data/calibration",
        };

        Assert.Equal(AppConfigExtensions.ResolveFolder("recipes"), cfg.ResolveRecipesFolder());
        Assert.Equal(AppConfigExtensions.ResolveFolder("models"), cfg.ResolveModelsFolder());
        Assert.Equal(AppConfigExtensions.ResolveFolder("data/calibration"), cfg.ResolveCalibrationFolder());
    }

    [Fact]
    public void ResolveDataPath_WithDataRoot_CombinesRelative()
    {
        var root = Path.Combine(Path.GetTempPath(), "rv_dr_" + Guid.NewGuid().ToString("N"));
        var cfg = new AppConfig { DataRoot = root, RecipesFolder = "recipes" };
        Assert.Equal(Path.GetFullPath(Path.Combine(root, "recipes")), cfg.ResolveDataPath("recipes"));
        Assert.Equal(Path.GetFullPath(root), cfg.ResolveDataRoot());
    }

    [Fact]
    public void ResolveModelsFolder_DoesNotUseDataRoot()
    {
        var cfg = new AppConfig { DataRoot = @"E:\RobotVisionData", ModelsFolder = "models" };
        Assert.Equal(AppConfigExtensions.ResolveFolder("models"), cfg.ResolveModelsFolder());
    }

    [Fact]
    public void DataRootBinder_Apply_RelocatesRelativeAndKeepsAbsolute()
    {
        var root = Path.Combine(Path.GetTempPath(), "rv_dr_" + Guid.NewGuid().ToString("N"));
        var absCalib = Path.Combine(Path.GetTempPath(), "rv_cal_" + Guid.NewGuid().ToString("N"));
        var cfg = new AppConfig
        {
            DataRoot = root,
            RecipesFolder = "recipes",
            CalibrationFolder = absCalib,
            FailureImage = { Folder = "failures" },
            Cameras = [new CameraConfig { Id = "cam_file", Type = "File", Folder = "replay" }],
        };
        DataRootBinder.Apply(cfg);
        Assert.Equal(Path.GetFullPath(Path.Combine(root, "recipes")), cfg.RecipesFolder);
        Assert.Equal(Path.GetFullPath(absCalib), cfg.CalibrationFolder);
        Assert.Equal(Path.GetFullPath(Path.Combine(root, "failures")), cfg.FailureImage.Folder);
        Assert.Equal(Path.GetFullPath(Path.Combine(root, "replay")), cfg.Cameras[0].Folder);
        Directory.Delete(root, true);
    }

    [Fact]
    public void DataRootBinder_CopyLegacyIfEmpty_CopiesJsonOnce()
    {
        var exe = Path.Combine(Path.GetTempPath(), "rv_exe_" + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(Path.GetTempPath(), "rv_dr_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(exe, "recipes"));
        File.WriteAllText(Path.Combine(exe, "recipes", "Product.json"), """{"name":"Product"}""");
        var cfg = new AppConfig { DataRoot = root, RecipesFolder = "recipes" };
        DataRootBinder.Apply(cfg);
        DataRootBinder.CopyLegacyIfEmpty(cfg, exe);
        Assert.True(File.Exists(Path.Combine(root, "recipes", "Product.json")));
        File.WriteAllText(Path.Combine(exe, "recipes", "Other.json"), "{}");
        DataRootBinder.CopyLegacyIfEmpty(cfg, exe);
        Assert.False(File.Exists(Path.Combine(root, "recipes", "Other.json")));
        Directory.Delete(exe, true);
        Directory.Delete(root, true);
    }

    [Fact]
    public void ResolveAndPrepareRecipesFolder_UsesDataRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "rv_dr_" + Guid.NewGuid().ToString("N"));
        var cfg = new AppConfig { DataRoot = root, RecipesFolder = "recipes" };
        var resolved = cfg.ResolveAndPrepareRecipesFolder();
        Assert.Equal(Path.GetFullPath(Path.Combine(root, "recipes")), resolved);
        Assert.True(File.Exists(Path.Combine(resolved, RecipeSampleSeeder.SeededMarkerFileName)));
        Directory.Delete(root, true);
    }

    [Fact]
    public void ResolveCameraFolder_WithApp_UsesDataRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "rv_dr_" + Guid.NewGuid().ToString("N"));
        var app = new AppConfig { DataRoot = root };
        var camera = new CameraConfig { Id = "cam_file", Folder = "replay" };
        Assert.Equal(Path.GetFullPath(Path.Combine(root, "replay")), camera.ResolveCameraFolder(app));
    }

    [Fact]
    public void ResolveCameraFolder_RelativePath_UsesFolderResolution()
    {
        var camera = new CameraConfig { Id = "cam_file", Folder = "data/replay" };
        Assert.Equal(AppConfigExtensions.ResolveFolder("data/replay"), camera.ResolveCameraFolder());
    }
}

