using Microsoft.Extensions.Logging.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Hosting;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// 本机 SQLite 结果库：插入/查询/多目标位姿/筛选/超期清理。
/// 不经管线线程；Insert 同步调用。ResultLogStore 双写通过轮询 Count 等待后台任务。
/// </summary>
public class SqliteResultStoreTests : IDisposable
{
    private readonly string _folder =
        Path.Combine(Path.GetTempPath(), "rv_sqlite_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_folder, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private ResultLogConfig Config(bool sqlite = true, bool jsonl = false, int retainedDays = 30) => new()
    {
        Enabled = true,
        Folder = Path.Combine(_folder, Guid.NewGuid().ToString("N")),
        RetainedDays = retainedDays,
        Jsonl = jsonl,
        Sqlite = sqlite,
    };

    private static ResultLogEntry Entry(
        string recipe = "A01",
        double? x = 10, double? y = 20, double? angle = 1.5, double? confidence = 0.9,
        int count = 1, int code = 0, string message = "",
        IReadOnlyList<ResultPoseLog>? poses = null)
    {
        poses ??= x is null
            ? []
            : [new ResultPoseLog(x.Value, y ?? 0, angle ?? 0, confidence)];
        return new ResultLogEntry(
            T: DateTimeOffset.Now.ToString("O"),
            Recipe: recipe,
            Station: "st1",
            Camera: "cam",
            X: x, Y: y, Angle: angle, Confidence: confidence,
            Count: count, ElapsedMs: 12.5, Code: code, Message: message,
            Poses: poses);
    }

    [Fact]
    public void Insert_Success_RoundTripsCoordinates()
    {
        using var db = new SqliteResultStore(Config(), NullLogger<SqliteResultStore>.Instance);
        db.Insert(Entry(), DateTimeOffset.Now.ToUnixTimeMilliseconds());

        var row = Assert.Single(db.Query());
        Assert.Equal("A01", row.Recipe);
        Assert.Equal("st1", row.Station);
        Assert.Equal("cam", row.Camera);
        Assert.Equal(10, row.X!.Value, 3);
        Assert.Equal(20, row.Y!.Value, 3);
        Assert.Equal(1.5, row.Angle!.Value, 3);
        Assert.Equal(0.9, row.Confidence!.Value, 3);
        Assert.Equal(0, row.Code);
        Assert.Single(row.Poses);
        Assert.Equal(1, db.Count());
    }

    [Fact]
    public void Insert_Failure_NullCoordinates()
    {
        using var db = new SqliteResultStore(Config(), NullLogger<SqliteResultStore>.Instance);
        db.Insert(Entry(x: null, y: null, angle: null, confidence: null, count: 0, code: 1007,
                message: "未检出目标", poses: []),
            DateTimeOffset.Now.ToUnixTimeMilliseconds());

        var row = Assert.Single(db.Query());
        Assert.Equal(1007, row.Code);
        Assert.Null(row.X);
        Assert.Null(row.Y);
        Assert.Empty(row.Poses);
        Assert.Contains("未检出", row.Message);
    }

    [Fact]
    public void Insert_MultiplePoses_StoresAllInPosesJson()
    {
        using var db = new SqliteResultStore(Config(), NullLogger<SqliteResultStore>.Instance);
        var poses = new ResultPoseLog[]
        {
            new(1, 2, 3, 0.9),
            new(4, 5, 6, 0.8),
        };
        db.Insert(Entry(x: 1, y: 2, angle: 3, count: 2, poses: poses),
            DateTimeOffset.Now.ToUnixTimeMilliseconds());

        var row = Assert.Single(db.Query());
        Assert.Equal(2, row.Count);
        Assert.Equal(2, row.Poses.Count);
        Assert.Equal(4, row.Poses[1].X, 3);
        Assert.Equal(6, row.Poses[1].Angle, 3);
        Assert.Equal(0.8, row.Poses[1].Confidence!.Value, 3);
    }

    [Fact]
    public void Query_FiltersByRecipeAndCode()
    {
        using var db = new SqliteResultStore(Config(), NullLogger<SqliteResultStore>.Instance);
        var t = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        db.Insert(Entry("A01"), t);
        db.Insert(Entry("A02"), t + 1);
        db.Insert(Entry("A01", x: null, y: null, angle: null, count: 0, code: 1007, poses: []), t + 2);

        Assert.Equal(2, db.Count(new ResultDbQuery { Recipe = "a01" }));
        Assert.Equal(1, db.Count(new ResultDbQuery { Recipe = "A01", Code = 0 }));
        Assert.Equal(1, db.Count(new ResultDbQuery { Code = 1007 }));
        Assert.Equal("A02", Assert.Single(db.Query(new ResultDbQuery { Recipe = "A02" })).Recipe);
    }

    [Fact]
    public void Summarize_AveragesSuccessfulPoses()
    {
        using var db = new SqliteResultStore(Config(), NullLogger<SqliteResultStore>.Instance);
        var t = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        db.Insert(Entry(x: 10, y: 0, angle: 0), t);
        db.Insert(Entry(x: 20, y: 0, angle: 0), t + 1);
        db.Insert(Entry(x: null, y: null, angle: null, count: 0, code: 1007, poses: []), t + 2);

        var summary = db.Summarize();
        Assert.Equal(3, summary.Total);
        Assert.Equal(2, summary.Ok);
        Assert.Equal(1, summary.Failed);
        Assert.Equal(15, summary.AvgX!.Value, 3);
    }

    [Fact]
    public void Query_OkOnly_FiltersPassAndFail()
    {
        using var db = new SqliteResultStore(Config(), NullLogger<SqliteResultStore>.Instance);
        var t = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        db.Insert(Entry("A01"), t);
        db.Insert(Entry("A01", x: null, y: null, angle: null, count: 0, code: 1007, poses: []), t + 1);

        Assert.Equal(1, db.Count(new ResultDbQuery { OkOnly = true }));
        Assert.Equal(1, db.Count(new ResultDbQuery { OkOnly = false }));
        Assert.Equal(1007, Assert.Single(db.Query(new ResultDbQuery { OkOnly = false })).Code);
        Assert.Equal(0, Assert.Single(db.Query(new ResultDbQuery { OkOnly = true })).Code);
    }

    [Fact]
    public void ListRecipes_QueryAngles_CountByCode()
    {
        using var db = new SqliteResultStore(Config(), NullLogger<SqliteResultStore>.Instance);
        var t = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        db.Insert(Entry("A01", x: 1, y: 0, angle: 10), t);
        db.Insert(Entry("B02", x: 2, y: 0, angle: 20), t + 1);
        db.Insert(Entry("A01", x: null, y: null, angle: null, count: 0, code: 1007, poses: []), t + 2);

        Assert.Equal(new[] { "A01", "B02" }, db.ListRecipes());
        var angles = db.QueryAngles(new ResultDbQuery { OkOnly = true });
        Assert.Equal(2, angles.Count);
        Assert.Contains(10d, angles);
        var codes = db.CountByCode();
        Assert.Equal(2, codes.Count);
        Assert.Equal(2, codes.Single(c => c.Code == 0).Count);
        Assert.Equal(1, codes.Single(c => c.Code == 1007).Count);
    }

    [Fact]
    public void DeleteOlderThan_RemovesExpiredRows()
    {
        using var db = new SqliteResultStore(Config(), NullLogger<SqliteResultStore>.Instance);
        var old = DateTimeOffset.Now.AddDays(-10).ToUnixTimeMilliseconds();
        var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        db.Insert(Entry("OLD"), old);
        db.Insert(Entry("NEW"), now);

        var deleted = db.DeleteOlderThan(DateTimeOffset.Now.AddDays(-7));
        Assert.Equal(1, deleted);
        Assert.Equal("NEW", Assert.Single(db.Query()).Recipe);
    }

    [Fact]
    public void Insert_Disabled_DoesNotCreateDatabase()
    {
        using var db = new SqliteResultStore(Config(sqlite: false), NullLogger<SqliteResultStore>.Instance);
        db.Insert(Entry(), DateTimeOffset.Now.ToUnixTimeMilliseconds());
        Assert.False(File.Exists(db.DatabasePath));
        Assert.Equal(0, db.Count());
    }

    [Fact]
    public void ResolveDbPath_RejectsPathTraversal()
    {
        var folder = Path.Combine(_folder, "safe");
        Assert.Equal(Path.Combine(folder, "results.db"),
            SqliteResultStore.ResolveDbPath(folder, "..\\evil.db"));
        Assert.Equal(Path.Combine(folder, "mine.db"),
            SqliteResultStore.ResolveDbPath(folder, "mine.db"));
    }

    [Fact]
    public void ResultLogStore_DualWrite_JsonlAndSqlite()
    {
        using var store = new ResultLogStore(Config(sqlite: true, jsonl: true),
            NullLogger<ResultLogStore>.Instance);
        store.Record(VisionResult.Success("A03",
            [new RobotPose(15.023, 20.117, 0.12), new RobotPose(1, 2, 3)], 87.5, [0.92, 0.8]),
            ("cam_file", "S1"));

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline && store.Sqlite.Count() < 1)
            Thread.Sleep(50);

        Assert.Equal(1, store.Sqlite.Count());
        var row = Assert.Single(store.Sqlite.Query());
        Assert.Equal("A03", row.Recipe);
        Assert.Equal(15.023, row.X!.Value, 3);
        Assert.Equal(2, row.Count);
        Assert.Equal(2, row.Poses.Count);
        Assert.Equal(1, row.Poses[1].X, 3);

        var jsonl = Directory.GetFiles(store.Sqlite.Folder, "results-*.jsonl");
        Assert.Single(jsonl);
        Assert.Contains("A03", File.ReadAllText(jsonl[0]));
    }

    [Fact]
    public void ResultLogStore_SqliteOnly_NoJsonlFile()
    {
        using var store = new ResultLogStore(Config(sqlite: true, jsonl: false),
            NullLogger<ResultLogStore>.Instance);
        store.Record(VisionResult.Success("A", [new RobotPose(1, 2, 0)], 10));

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline && store.Sqlite.Count() < 1)
            Thread.Sleep(50);

        Assert.Equal(1, store.Sqlite.Count());
        Assert.Empty(Directory.Exists(store.Sqlite.Folder)
            ? Directory.GetFiles(store.Sqlite.Folder, "results-*.jsonl")
            : []);
    }
}
