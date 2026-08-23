using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;
using RobotVision.Core.Models;
using RobotVision.Hosting;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// 失败现场图像留存测试：PNG+JSON 成对落盘、元数据内容、同毫秒冲突、
/// 数量滚动清理（含孤儿元数据）、开关与空图守卫。时钟注入保证确定性。
/// </summary>
public class FailureImageStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "rv_fail_" + Guid.NewGuid().ToString("N"));
    private readonly DateTime _base = new(2026, 8, 22, 10, 0, 0);

    public void Dispose()
    {
        try { Directory.Delete(_folder, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private FailureImageStore CreateStore(int retained = 3, bool enabled = true, Func<DateTime>? clock = null)
        => new(new FailureImageConfig { Enabled = enabled, Folder = _folder, RetainedCount = retained },
            NullLogger<FailureImageStore>.Instance, clock);

    private static Mat MakeImage(int w = 16, int h = 12)
        => new(h, w, MatType.CV_8UC3, Scalar.All(200));

    private static VisionResult MakeFailure(VisionErrorCode code = VisionErrorCode.NoTargetFound, string msg = "未检出目标")
        => VisionResult.Fail("A01", code, msg, 123.4);

    /// <summary>
    /// 等待后台落盘完成：Save 已异步化（fire-and-forget + 后台 WriteCore），
    /// 断言前轮询目录直到满足条件，避免与后台线程竞态。
    /// </summary>
    private static void WaitForCondition(Func<bool> condition, string description)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;
            Thread.Sleep(25);
        }
        Assert.Fail($"等待超时: {description}");
    }

    private static void WaitForPngs(string folder, int expected) =>
        WaitForCondition(
            () => Directory.Exists(folder) && Directory.GetFiles(folder, "*.png").Length == expected,
            $"目录中应有 {expected} 张 PNG");

    [Fact]
    public void Save_WritesPngAndJsonSidecar()
    {
        var store = CreateStore(clock: () => _base);
        using var image = MakeImage();
        store.Save("A01", image, MakeFailure());
        WaitForPngs(_folder, 1); // Save 已异步化，等待后台落盘完成

        var pngs = Directory.GetFiles(_folder, "*.png");
        var jsons = Directory.GetFiles(_folder, "*.json");
        Assert.Single(pngs);
        Assert.Single(jsons);
        Assert.Equal("20260822_100000000_A01_1007.png", Path.GetFileName(pngs[0]));

        using var roundTrip = Cv2.ImRead(pngs[0]);
        Assert.Equal(16, roundTrip.Width);
        Assert.Equal(12, roundTrip.Height);

        var json = File.ReadAllText(jsons[0]);
        Assert.Contains("\"Recipe\": \"A01\"", json);
        Assert.Contains("\"ErrorCode\": 1007", json);
        Assert.Contains("未检出目标", json); // 中文不转义，现场工程师可直接读
        Assert.Contains("\"ElapsedMs\": 123.4", json);
    }

    [Fact]
    public void Save_SameTimestamp_AppendsSuffixWithoutOverwrite()
    {
        using var image = MakeImage();
        var fixedClock = _base;
        var store2 = new FailureImageStore(
            new FailureImageConfig { Folder = _folder, RetainedCount = 10 },
            NullLogger<FailureImageStore>.Instance, () => fixedClock);

        store2.Save("A01", image, MakeFailure(VisionErrorCode.InternalError, "内部错误"));
        store2.Save("A01", image, MakeFailure(VisionErrorCode.InternalError, "内部错误"));
        store2.Save("A01", image, MakeFailure(VisionErrorCode.InternalError, "内部错误"));
        WaitForPngs(_folder, 3); // 等待 3 次后台落盘全部完成

        Assert.Equal(3, Directory.GetFiles(_folder, "*.png").Length);
        Assert.Contains("20260822_100000000_A01_1099_1.png",
            Directory.GetFiles(_folder, "*.png").Select(Path.GetFileName));
        Assert.Contains("20260822_100000000_A01_1099_2.png",
            Directory.GetFiles(_folder, "*.png").Select(Path.GetFileName));
    }

    [Fact]
    public void Save_RetentionKeepsNewestAndCleansSidecars()
    {
        var time = _base;
        var store = new FailureImageStore(
            new FailureImageConfig { Folder = _folder, RetainedCount = 3 },
            NullLogger<FailureImageStore>.Instance, () => time);
        using var image = MakeImage();

        for (var i = 0; i < 5; i++)
        {
            // 用 1099（非 1007）验证数量滚动：1007 有独立限流，不参与本用例
            store.Save("A01", image, MakeFailure(VisionErrorCode.InternalError, "内部错误"));
            time = time.AddSeconds(1);
        }

        // 等全部 5 次落盘完成（TotalSaved==5）且滚动清理后数量稳定为 3（含最后保存的秒 4 文件）
        WaitForCondition(
            () => store.TotalSaved == 5 &&
                  Directory.Exists(_folder) &&
                  Directory.GetFiles(_folder, "*.png").Length == 3 &&
                  Directory.GetFiles(_folder, "*.png").Any(p => p.Contains("100004000")),
            "滚动清理后保留最新 3 张");

        var pngs = Directory.GetFiles(_folder, "*.png").Select(Path.GetFileName).OrderBy(n => n).ToList();
        var jsons = Directory.GetFiles(_folder, "*.json").Select(Path.GetFileName).OrderBy(n => n).ToList();

        Assert.Equal(3, pngs.Count);
        Assert.Equal(3, jsons.Count);
        // 5 次保存（秒 0~4）保留最新 3 张（秒 2/3/4），最旧两张连同元数据被清理
        Assert.DoesNotContain("20260822_100000000_A01_1099.png", pngs);
        Assert.DoesNotContain("20260822_100001000_A01_1099.png", pngs);
        Assert.Contains("20260822_100002000_A01_1099.png", pngs);
        Assert.Contains("20260822_100004000_A01_1099.png", pngs);
        Assert.Equal(
            pngs.Select(n => Path.GetFileNameWithoutExtension(n) + ".json").OrderBy(n => n),
            jsons);
    }

    [Fact]
    public void Save_Disabled_IsNoOp()
    {
        var store = CreateStore(enabled: false);
        using var image = MakeImage();

        store.Save("A01", image, MakeFailure());

        Assert.False(Directory.Exists(_folder));
    }

    [Fact]
    public void Save_EmptyImage_Skipped()
    {
        var store = CreateStore();
        using var empty = new Mat();

        store.Save("A01", empty, MakeFailure());

        Assert.Empty(Directory.Exists(_folder)
            ? Directory.GetFiles(_folder)
            : Array.Empty<string>());
    }

    [Fact]
    public void Save_SuccessResult_Skipped()
    {
        var store = CreateStore();
        using var image = MakeImage();

        store.Save("A01", image, VisionResult.Success("A01", [], 1));

        Assert.Empty(Directory.Exists(_folder)
            ? Directory.GetFiles(_folder)
            : Array.Empty<string>());
    }

    [Fact]
    public void Save_ZeroRetention_KeepsEverything()
    {
        var time = _base;
        var store = new FailureImageStore(
            new FailureImageConfig { Folder = _folder, RetainedCount = 0 },
            NullLogger<FailureImageStore>.Instance, () => time);
        using var image = MakeImage();

        for (var i = 0; i < 6; i++)
        {
            store.Save("A01", image, MakeFailure(VisionErrorCode.InternalError, "内部错误"));
            time = time.AddSeconds(1);
        }
        WaitForPngs(_folder, 6); // 等待 6 次后台落盘全部完成

        Assert.Equal(6, Directory.GetFiles(_folder, "*.png").Length);
    }

    // ---- 分级留存：1007 限流 / 缩图 / 清理优先级 ----

    [Fact]
    public void Save_NoTarget_ThrottledWithinWindow()
    {
        // 同一 (配方, 1007) 在窗口内只存 1 张；不同配方不受限
        var store = CreateStore(clock: () => _base);
        using var image = MakeImage();

        store.Save("A01", image, MakeFailure());
        store.Save("A01", image, MakeFailure());
        store.Save("A02", image, MakeFailure());

        // 限流后应只落盘 2 张（A01 一次 + A02 一次），等待后台完成
        WaitForPngs(_folder, 2);
    }

    [Fact]
    public void Save_NoTarget_StoredAgainAfterWindow()
    {
        var time = _base;
        var store = CreateStore(clock: () => time);
        using var image = MakeImage();

        store.Save("A01", image, MakeFailure());
        time = time.AddMinutes(2); // 超过默认 1 分钟限流窗口
        store.Save("A01", image, MakeFailure());
        WaitForPngs(_folder, 2);

        Assert.Equal(2, Directory.GetFiles(_folder, "*.png").Length);
    }

    [Fact]
    public void Save_NoTarget_DownscaledToMaxWidth()
    {
        var store = CreateStore(clock: () => _base);
        store.MaxNoTargetWidth = 8;
        using var image = MakeImage(w: 64, h: 48);

        store.Save("A01", image, MakeFailure());
        WaitForPngs(_folder, 1); // 等待后台落盘完成

        var png = Directory.GetFiles(_folder, "*.png").Single();
        using var saved = Cv2.ImRead(png);
        Assert.Equal(8, saved.Width); // 高度按比例 48 * 8/64 = 6
        Assert.Equal(6, saved.Height);
    }

    [Fact]
    public void Cleanup_PrefersKeepingNonNoTarget()
    {
        var time = _base;
        var store = new FailureImageStore(
            new FailureImageConfig { Folder = _folder, RetainedCount = 2 },
            NullLogger<FailureImageStore>.Instance, () => time);
        using var image = MakeImage();

        store.Save("A01", image, MakeFailure(VisionErrorCode.InternalError, "内部错误"));
        time = time.AddSeconds(1);
        store.Save("A01", image, MakeFailure());             // 1007
        time = time.AddSeconds(1);
        store.Save("A02", image, MakeFailure());             // 1007

        // 等待最后一次落盘（含滚动清理）完成：数量 2 且包含最后保存的 A02（秒 2）
        WaitForCondition(
            () => Directory.Exists(_folder) &&
                  Directory.GetFiles(_folder, "*.png").Length == 2 &&
                  Directory.GetFiles(_folder, "*.png").Any(p => p.Contains("100002000")),
            "清理后保留 1099 与 A02_1007");

        var pngs = Directory.GetFiles(_folder, "*.png").Select(Path.GetFileName).ToList();
        Assert.Equal(2, pngs.Count);
        // 配额 2：删 1007 中最旧（A01_1007），保留 1099 与 A02_1007
        Assert.Contains(pngs, n => n!.Contains("_1099.png"));
        Assert.DoesNotContain(pngs, n => n!.Contains("A01_1007.png"));
        Assert.Contains(pngs, n => n!.Contains("A02_1007.png"));
    }

    [Fact]
    public void Cleanup_RemovesOrphanJson()
    {
        var store = CreateStore(retained: 10, clock: () => _base);
        using var image = MakeImage();

        // 手工制造孤儿 JSON（无对应 PNG）
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "orphan.json"), "{}");

        store.Save("A01", image, MakeFailure(VisionErrorCode.InternalError, "内部错误"));

        // 等待后台落盘完成（含孤儿 JSON 清理）
        WaitForCondition(
            () => !File.Exists(Path.Combine(_folder, "orphan.json")),
            "孤儿 JSON 被清理");

        Assert.False(File.Exists(Path.Combine(_folder, "orphan.json")));
    }

    [Fact]
    public void Cleanup_RetainedDays_RemovesExpired()
    {
        var time = _base;
        var store = new FailureImageStore(
            new FailureImageConfig { Folder = _folder, RetainedCount = 0, RetainedDays = 1 },
            NullLogger<FailureImageStore>.Instance, () => time);
        using var image = MakeImage();

        store.Save("A01", image, MakeFailure(VisionErrorCode.InternalError, "内部错误"));
        time = time.AddDays(2);
        store.Save("A01", image, MakeFailure(VisionErrorCode.InternalError, "内部错误"));
        WaitForPngs(_folder, 1); // 等待最后一次落盘（含天数清理）完成

        Assert.Single(Directory.GetFiles(_folder, "*.png"));
    }

    [Fact]
    public void Stats_Tracked()
    {
        var store = CreateStore(clock: () => _base);
        using var image = MakeImage();

        Assert.Equal(0, store.TotalSaved);
        store.Save("A01", image, MakeFailure(VisionErrorCode.InternalError, "内部错误"));
        WaitForCondition(() => store.TotalSaved == 1, "后台落盘完成并更新统计");

        Assert.Equal(1, store.TotalSaved);
        Assert.True(store.TotalBytes > 0);
        Assert.Equal(_base, store.LastSavedAt);
    }

    [Fact]
    public void Meta_IncludesDiagnosticContext()
    {
        var store = CreateStore(clock: () => _base);
        using var image = MakeImage();

        store.Save("A01", image, MakeFailure(VisionErrorCode.InternalError, "内部错误"),
            new FailureContext(CameraId: "cam1", StationId: "st1", Models: "a.onnx|b.onnx",
                AngleMode: "KeyPointLine", Confidence: 0.5, Iou: 0.7, Source: "pipeline"));
        WaitForCondition(
            () => Directory.Exists(_folder) && Directory.GetFiles(_folder, "*.json").Length == 1,
            "元数据 JSON 落盘完成");

        var json = Directory.GetFiles(_folder, "*.json").Single();
        var text = File.ReadAllText(json);
        Assert.Contains("\"CameraId\": \"cam1\"", text);
        Assert.Contains("\"StationId\": \"st1\"", text);
        Assert.Contains("\"Models\": \"a.onnx|b.onnx\"", text);
        Assert.Contains("\"Source\": \"pipeline\"", text);
    }
}


