using System.Text.Json;
using System.Text.Json.Nodes;

namespace RobotVision.Hosting;

/// <summary>
/// appsettings.json 的原子读-改-写：整个"读取 → 变更 → 临时文件替换落盘"
/// 都在同一把进程内静态锁（<see cref="Gate"/>）下执行。
/// 解决两个问题：1) 并发 Save 的读-改-写互相覆盖（AppSettingsStore / CameraConfigStore /
/// LightingConfigStore 都会写同一文件，读取与写入同锁才能保证原子）；2) 写一半崩溃
/// 不会留下损坏的半写文件。
/// </summary>
internal static class JsonAtomicWrite
{
    private static readonly object Gate = new();

    /// <summary>整段字符串原子写（临时文件 + 替换）。仅落盘加锁，读取在调用方。</summary>
    public static void WriteAllText(string path, string content)
    {
        lock (Gate)
            WriteAllTextCore(path, content);
    }

    /// <summary>
    /// 原子读-改-写：读取与落盘在同一把锁下，杜绝"只锁写、不锁读"时并发 Save 的后写覆盖前写。
    /// 变更回调中抛异常不会落盘（不产生部分写）。读取支持 JSONC 注释/尾逗号（appsettings.json 含中文注释）。
    /// </summary>
    public static void Update(string path, JsonSerializerOptions options, Action<JsonObject> mutate)
    {
        lock (Gate)
        {
            JsonNode? root = null;
            if (File.Exists(path))
            {
                // CommentHandling.Skip + AllowTrailingCommas 与宿主配置绑定（AddJsonFile）行为一致
                root = JsonNode.Parse(File.ReadAllText(path), documentOptions: new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                });
                if (root is not JsonObject)
                    throw new InvalidOperationException($"appsettings.json 根节点不是对象: {path}");
            }
            root ??= new JsonObject();

            var obj = (JsonObject)root;
            mutate(obj);

            WriteAllTextCore(path, obj.ToJsonString(options));
        }
    }

    private static void WriteAllTextCore(string path, string content)
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
            catch (IOException) { }
        }
    }
}
