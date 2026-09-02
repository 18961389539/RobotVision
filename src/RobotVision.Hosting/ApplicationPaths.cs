using Microsoft.Extensions.Configuration;

namespace RobotVision.Hosting;

/// <summary>
/// 安装目录（只读随包配置）与用户数据目录（可写配置/工位数据）的路径约定。
/// 避免把 appsettings 写到 Program Files 导致保存失败，也避免仓库默认配置绑死某台开发机。
/// </summary>
public static class ApplicationPaths
{
    public const string ProductFolderName = "RobotVision";

    /// <summary>exe 所在目录（随包 appsettings.json）。</summary>
    public static string InstallDirectory => AppContext.BaseDirectory;

    /// <summary>随包默认配置（只读模板，运行时 UI 不写回此文件）。</summary>
    public static string InstallSettingsPath => Path.Combine(InstallDirectory, "appsettings.json");

    /// <summary>本机可选开发覆盖（optional，不入库）。</summary>
    public static string DevelopmentSettingsPath =>
        Path.Combine(InstallDirectory, "appsettings.Development.json");

    /// <summary>
    /// 用户/机器数据根。默认 %ProgramData%\RobotVision（服务与多用户工控机共享）；
    /// 可用环境变量 ROBOTVISION_USER_DATA 覆盖。
    /// </summary>
    public static string UserDataDirectory
    {
        get
        {
            var env = Environment.GetEnvironmentVariable("ROBOTVISION_USER_DATA");
            if (!string.IsNullOrWhiteSpace(env))
                return Path.GetFullPath(env.Trim());

            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            return Path.Combine(programData, ProductFolderName);
        }
    }

    /// <summary>未配置 DataRoot 时的默认工位数据目录（%ProgramData%\RobotVision\Data）。</summary>
    public static string DefaultDataRoot => Path.Combine(UserDataDirectory, "Data");

    /// <summary>
    /// 可写 appsettings 路径。默认 %ProgramData%\RobotVision\appsettings.json；
    /// 可用 ROBOTVISION_SETTINGS 覆盖（测试/容器部署）。
    /// </summary>
    public static string UserSettingsPath
    {
        get
        {
            var env = Environment.GetEnvironmentVariable("ROBOTVISION_SETTINGS");
            if (!string.IsNullOrWhiteSpace(env))
                return Path.GetFullPath(env.Trim());
            return Path.Combine(UserDataDirectory, "appsettings.json");
        }
    }

    /// <summary>首次启动：确保用户目录存在，并从随包配置复制一份可写副本。</summary>
    public static void EnsureUserSettings()
    {
        Directory.CreateDirectory(UserDataDirectory);
        if (File.Exists(UserSettingsPath))
            return;
        if (File.Exists(InstallSettingsPath))
            File.Copy(InstallSettingsPath, UserSettingsPath);
    }

    /// <summary>
    /// 启动前归一化：ROBOTVISION_DATA_ROOT 优先；DataRoot 仍为空则落到 <see cref="DefaultDataRoot"/>。
    /// 便携部署可在用户 appsettings 里把 DataRoot 设为相对路径（如 "."）以锚定 exe 旁目录。
    /// </summary>
    public static void NormalizeAppConfig(AppConfig cfg)
    {
        var dataRootEnv = Environment.GetEnvironmentVariable("ROBOTVISION_DATA_ROOT");
        if (!string.IsNullOrWhiteSpace(dataRootEnv))
            cfg.DataRoot = dataRootEnv.Trim();
        else if (string.IsNullOrWhiteSpace(cfg.DataRoot))
            cfg.DataRoot = DefaultDataRoot;
    }

    /// <summary>叠加随包、用户、Development 三层 JSON 配置（后加载的覆盖先加载的）。</summary>
    public static IConfigurationBuilder AddRobotVisionAppSettings(this IConfigurationBuilder builder)
    {
        builder.AddJsonFile(InstallSettingsPath, optional: true, reloadOnChange: false);
        builder.AddJsonFile(UserSettingsPath, optional: true, reloadOnChange: false);
        builder.AddJsonFile(DevelopmentSettingsPath, optional: true, reloadOnChange: false);
        return builder;
    }
}
