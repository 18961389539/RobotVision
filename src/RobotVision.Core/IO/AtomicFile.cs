namespace RobotVision.Core.IO;

/// <summary>
/// 产线关键资产（配方 / 标定档案 / appsettings / 健康状态）的原子落盘：
/// 临时文件 + <see cref="File.Replace"/>。写一半崩溃不留截断 JSON，
/// 读者要么看到旧文件、要么看到完整新文件（替换对读者近似原子）。
/// <para>
/// 全仓此前有 4 份各自手写的同语义实现（RecipeLoader / JsonAtomicWrite /
/// ProcessHealthStore / CalibrationManager），临时文件命名与异常清理各不相同，
/// 改一漏三，已统一到本类。
/// </para>
/// <para>
/// 进程内全局锁：所有原子写串行化，杜绝并发 Save 互相覆盖。
/// <c>lock(Monitor)</c> 可重入——"读-改-写"场景在 <see cref="InWriteLock"/> 内
/// 读取并调用 <see cref="WriteAllText"/>（读与写同锁才保证整体原子），嵌套安全。
/// </para>
/// </summary>
public static class AtomicFile
{
    private static readonly object Gate = new();

    /// <summary>
    /// 在同一把进程内锁下执行回调。读取与落盘需整体原子时使用
    /// （只锁写、不锁读时，并发"读-改-写"仍会互相覆盖）；
    /// 回调内可再调 <see cref="WriteAllText"/>（锁可重入）。
    /// </summary>
    public static void InWriteLock(Action action)
    {
        lock (Gate)
            action();
    }

    /// <summary>原子写文本：临时文件 + 替换；异常时清理临时文件（不残留 .tmp）。</summary>
    public static void WriteAllText(string path, string content)
    {
        lock (Gate)
        {
            var full = Path.GetFullPath(path);
            var dir = Path.GetDirectoryName(full)!;
            Directory.CreateDirectory(dir);
            var tmp = Path.Combine(dir, $".{Path.GetFileName(full)}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(tmp, content);
                if (File.Exists(full))
                    File.Replace(tmp, full, null);
                else
                    File.Move(tmp, full);
            }
            finally
            {
                try { File.Delete(tmp); }
                catch (IOException ex)
                {
                    // 临时文件残留（被占用/权限）：不影响主路径（已 Replace/Move 成功），留痕便于排查
                    System.Diagnostics.Trace.TraceWarning(
                        $"AtomicFile: 临时文件清理失败 {tmp}: {ex.Message}");
                }
            }
        }
    }
}
