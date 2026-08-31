using System.Text.Json;
using System.Text.Json.Nodes;
using RobotVision.Core.IO;

namespace RobotVision.Hosting;

/// <summary>
/// appsettings.json 的原子读-改-写：整个"读取 → 变更 → 临时文件替换落盘"
/// 都在同一把进程内锁（<see cref="AtomicFile"/> 内部 Gate）下执行。
/// 解决两个问题：1) 并发 Save 的读-改-写互相覆盖（AppSettingsStore / CameraConfigStore /
/// LightingConfigStore 都会写同一文件，读取与写入同锁才能保证原子）；2) 写一半崩溃
/// 不会留下损坏的半写文件。
/// <para>落盘实现已统一到 <see cref="AtomicFile"/>，本类只保留 appsettings 的
/// JSON 读-改-写编排（JSONC 注释/尾逗号兼容）。</para>
/// </summary>
internal static class JsonAtomicWrite
{
    /// <summary>整段字符串原子写（临时文件 + 替换）。仅落盘加锁，读取在调用方。</summary>
    public static void WriteAllText(string path, string content) =>
        AtomicFile.WriteAllText(path, content);

    /// <summary>
    /// 原子读-改-写：读取与落盘在同一把锁下，杜绝"只锁写、不锁读"时并发 Save 的后写覆盖前写。
    /// 变更回调中抛异常不会落盘（不产生部分写）。读取支持 JSONC 注释/尾逗号（appsettings.json 含中文注释）。
    /// </summary>
    public static void Update(string path, JsonSerializerOptions options, Action<JsonObject> mutate)
    {
        AtomicFile.InWriteLock(() =>
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

            WriteAllText(path, obj.ToJsonString(options));
        });
    }
}
