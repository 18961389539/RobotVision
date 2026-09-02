using Microsoft.Extensions.Configuration;
using RobotVision.Hosting;
using Xunit;

namespace RobotVision.Tests;

/// <summary>改进程环境变量，须串行（与 CameraConfigStore/FailureImageStore 等同一队列）。</summary>
[Collection("Serial")]
public class ApplicationPathsTests : IDisposable
{
    private readonly string? _settingsEnv;
    private readonly string? _userDataEnv;
    private readonly string? _dataRootEnv;

    public ApplicationPathsTests()
    {
        _settingsEnv = Environment.GetEnvironmentVariable("ROBOTVISION_SETTINGS");
        _userDataEnv = Environment.GetEnvironmentVariable("ROBOTVISION_USER_DATA");
        _dataRootEnv = Environment.GetEnvironmentVariable("ROBOTVISION_DATA_ROOT");
        Environment.SetEnvironmentVariable("ROBOTVISION_SETTINGS", null);
        Environment.SetEnvironmentVariable("ROBOTVISION_USER_DATA", null);
        Environment.SetEnvironmentVariable("ROBOTVISION_DATA_ROOT", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ROBOTVISION_SETTINGS", _settingsEnv);
        Environment.SetEnvironmentVariable("ROBOTVISION_USER_DATA", _userDataEnv);
        Environment.SetEnvironmentVariable("ROBOTVISION_DATA_ROOT", _dataRootEnv);
    }

    [Fact]
    public void UserSettingsPath_DefaultsUnderProgramData()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            ApplicationPaths.ProductFolderName,
            "appsettings.json");
        Assert.Equal(expected, ApplicationPaths.UserSettingsPath);
    }

    [Fact]
    public void EnsureUserSettings_CreatesUserDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "rv_paths_" + Guid.NewGuid().ToString("N"));
        var userSettings = Path.Combine(root, "appsettings.json");
        Environment.SetEnvironmentVariable("ROBOTVISION_USER_DATA", root);
        Environment.SetEnvironmentVariable("ROBOTVISION_SETTINGS", userSettings);

        ApplicationPaths.EnsureUserSettings();
        Assert.True(Directory.Exists(root));

        Directory.Delete(root, true);
    }

    [Fact]
    public void NormalizeAppConfig_EmptyDataRoot_UsesDefaultDataRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "rv_paths_" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("ROBOTVISION_USER_DATA", root);
        var cfg = new AppConfig();
        ApplicationPaths.NormalizeAppConfig(cfg);
        Assert.Equal(Path.Combine(root, "Data"), cfg.DataRoot);
        // NormalizeAppConfig 只写配置不建目录：无目录可清理，删 Delete 避免 DirectoryNotFound
    }

    [Fact]
    public void NormalizeAppConfig_RespectsDataRootEnvironmentVariable()
    {
        var custom = Path.Combine(Path.GetTempPath(), "rv_custom_" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("ROBOTVISION_DATA_ROOT", custom);
        var cfg = new AppConfig { DataRoot = "ignored-when-env-set" };
        ApplicationPaths.NormalizeAppConfig(cfg);
        Assert.Equal(custom, cfg.DataRoot);
    }

    [Fact]
    public void AddRobotVisionAppSettings_UserFileOverridesInstall()
    {
        var root = Path.Combine(Path.GetTempPath(), "rv_cfg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var install = Path.Combine(root, "install.json");
        var user = Path.Combine(root, "user.json");
        File.WriteAllText(install, """{"TcpPort":1111,"TimeoutMs":1000}""");
        File.WriteAllText(user, """{"TcpPort":2222}""");

        Environment.SetEnvironmentVariable("ROBOTVISION_SETTINGS", user);

        var cfg = new ConfigurationBuilder()
            .AddJsonFile(install, optional: false)
            .AddJsonFile(user, optional: false)
            .Build()
            .Get<AppConfig>();

        Assert.NotNull(cfg);
        Assert.Equal(2222, cfg.TcpPort);
        Assert.Equal(1000, cfg.TimeoutMs);

        Directory.Delete(root, true);
    }
}
