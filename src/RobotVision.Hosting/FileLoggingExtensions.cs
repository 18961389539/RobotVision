using Microsoft.Extensions.Logging;
using Serilog;

namespace RobotVision.Hosting;

/// <summary>
/// 文件日志（Serilog 滚动文件）：工控机无头运行时唯一的现场留痕，
/// 控制台日志在服务化部署后会丢失。按天滚动、自动清理超期文件，
/// 条目含毫秒时间戳、级别、来源类别、消息与完整异常堆栈。
/// 控制台宿主与 WPF 宿主共用同一实现。
/// </summary>
public static class FileLoggingExtensions
{
    public static ILoggingBuilder AddRobotVisionFileLogging(this ILoggingBuilder builder, AppConfig cfg)
    {
        if (!cfg.FileLogging.Enabled)
            return builder;

        var folder = cfg.ResolveDataPath(cfg.FileLogging.Folder);
        return builder.AddRobotVisionFileLogging(folder, cfg.FileLogging.RetainedDays);
    }

    public static ILoggingBuilder AddRobotVisionFileLogging(
        this ILoggingBuilder builder, string folder, int retainedDays)
    {
        Directory.CreateDirectory(folder);
        var serilog = new LoggerConfiguration()
            .WriteTo.File(
                Path.Combine(folder, "robotvision-.log"),
                rollingInterval: RollingInterval.Day,
                // 日志页同时读当天文件；不共享会 ERROR_SHARING_VIOLATION
                shared: true,
                // retainedDays ≤ 0 = 不自动清理（null）；原 Math.Max(1,·) 把 0 改成 1，
                // 与"≤0 不清理"的语义不一致（RetainedDays=0 应保留全部日志）
                retainedFileCountLimit: retainedDays > 0 ? retainedDays : null,
                outputTemplate:
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
        return builder.AddSerilog(serilog, dispose: true);
    }
}
