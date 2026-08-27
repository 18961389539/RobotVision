using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RobotVision.Hosting;
using RobotVision.WpfHost.Features.Analysis;

namespace RobotVision.Wpf.Tests;

/// <summary>
/// 结果分析页：空库提示、KPI/明细、时间与成败筛选、角度直方图分箱。
/// </summary>
public class AnalysisViewModelTests
{
    [Fact]
    public async Task Refresh_EmptyDatabase_ShowsHint_NoRows()
    {
        using var dir = new TestInfra.TempDir("rv_analysis_empty");
        using var db = OpenDb(dir);
        var vm = new AnalysisViewModel(db);
        vm.Range = AnalysisViewModel.RangeAll;
        await vm.RefreshAsync();

        vm.HasRows.Should().BeFalse();
        vm.TotalText.Should().Be("0");
        vm.YieldText.Should().Be("—");
        vm.Message.Should().Contain("暂无结果记录");
        vm.RecipeOptions.Should().Equal(AnalysisViewModel.AllRecipes);
    }

    [Fact]
    public async Task Refresh_WithResults_FillsKpisAndTable()
    {
        using var dir = new TestInfra.TempDir("rv_analysis_kpi");
        using var db = OpenDb(dir);
        var now = DateTimeOffset.Now;
        Insert(db, "A01", now, 10, 20, 1.5, 0);
        Insert(db, "A01", now.AddSeconds(1), 30, 40, 2.5, 0);
        Insert(db, "B02", now.AddSeconds(2), null, null, null, 1007);

        var vm = new AnalysisViewModel(db) { Clock = () => now.AddMinutes(1) };
        vm.Range = AnalysisViewModel.RangeAll;
        await vm.RefreshAsync();

        vm.TotalText.Should().Be("3");
        vm.OkText.Should().Be("2");
        vm.FailText.Should().Be("1");
        vm.YieldText.Should().Be("66.7%");
        vm.HasRows.Should().BeTrue();
        vm.Rows.Should().HaveCount(3);
        vm.RecipeOptions.Should().Equal(AnalysisViewModel.AllRecipes, "A01", "B02");
        vm.HasAngleBars.Should().BeTrue();
        vm.HasCodeRows.Should().BeTrue();
        vm.CodeRows.Select(r => r.Label).Should().Contain(new[] { "合格", "1007" });
        vm.AvgAngleText.Should().Contain("°");
    }

    [Fact]
    public async Task RecipeFilter_LimitsRowsAndYield()
    {
        using var dir = new TestInfra.TempDir("rv_analysis_recipe");
        using var db = OpenDb(dir);
        var now = DateTimeOffset.Now;
        Insert(db, "A01", now, 1, 1, 0, 0);
        Insert(db, "B02", now.AddSeconds(1), null, null, null, 1007);

        var vm = new AnalysisViewModel(db) { Clock = () => now.AddMinutes(1) };
        vm.Range = AnalysisViewModel.RangeAll;
        await vm.RefreshAsync();
        vm.RecipeFilter = "B02";
        await vm.RefreshAsync();

        vm.TotalText.Should().Be("1");
        vm.FailText.Should().Be("1");
        vm.Rows.Should().ContainSingle().Which.Recipe.Should().Be("B02");
    }

    [Fact]
    public async Task OutcomeFail_HidesOkRows()
    {
        using var dir = new TestInfra.TempDir("rv_analysis_fail");
        using var db = OpenDb(dir);
        var now = DateTimeOffset.Now;
        Insert(db, "A01", now, 1, 1, 0, 0);
        Insert(db, "A01", now.AddSeconds(1), null, null, null, 1007);

        var vm = new AnalysisViewModel(db) { Clock = () => now.AddMinutes(1) };
        vm.Range = AnalysisViewModel.RangeAll;
        vm.Outcome = AnalysisViewModel.OutcomeFail;
        await vm.RefreshAsync();

        vm.TotalText.Should().Be("1");
        vm.Rows.Should().ContainSingle().Which.Ok.Should().BeFalse();
        vm.BuildQuery().OkOnly.Should().BeFalse();
    }

    [Fact]
    public async Task RangeToday_ExcludesYesterday()
    {
        using var dir = new TestInfra.TempDir("rv_analysis_today");
        using var db = OpenDb(dir);
        var today = new DateTimeOffset(2026, 8, 27, 11, 0, 0, TimeSpan.FromHours(8));
        Insert(db, "OLD", today.AddDays(-1), 1, 1, 0, 0);
        Insert(db, "NEW", today, 2, 2, 1, 0);

        var vm = new AnalysisViewModel(db) { Clock = () => today.AddHours(1) };
        vm.Range = AnalysisViewModel.RangeToday;
        await vm.RefreshAsync();

        vm.TotalText.Should().Be("1");
        vm.Rows.Should().ContainSingle().Which.Recipe.Should().Be("NEW");
    }

    [Fact]
    public void BuildHistogram_Empty_And_SingleValue_And_Spread()
    {
        AnalysisViewModel.BuildHistogram([]).Should().BeEmpty();

        var same = AnalysisViewModel.BuildHistogram([1.5, 1.5, 1.5]);
        same.Should().ContainSingle();
        same[0].Count.Should().Be(3);
        same[0].Ratio.Should().Be(1);

        var spread = AnalysisViewModel.BuildHistogram([-6, 0, 6], binCount: 3);
        spread.Should().HaveCount(3);
        spread.Sum(b => b.Count).Should().Be(3);
        spread.Max(b => b.Ratio).Should().Be(1);
    }

    [Fact]
    public async Task Refresh_SqliteDisabled_StillReadsExistingRows_ShowsHint()
    {
        using var dir = new TestInfra.TempDir("rv_analysis_disabled");
        using var db = OpenDb(dir);
        var now = DateTimeOffset.Now;
        Insert(db, "A01", now, 1, 1, 0, 0);
        db.Enabled = false;

        using var results = new ResultLogStore(
            new ResultLogConfig { Folder = db.Folder, Sqlite = false, Jsonl = false },
            NullLogger<ResultLogStore>.Instance,
            db);
        var vm = new AnalysisViewModel(db, results) { Clock = () => now.AddMinutes(1) };
        vm.Range = AnalysisViewModel.RangeAll;
        await vm.RefreshAsync();

        vm.HasRows.Should().BeTrue();
        vm.Message.Should().Contain("当前未写入");
    }

    private static SqliteResultStore OpenDb(TestInfra.TempDir dir)
    {
        var folder = dir.CreateSub("results");
        return new SqliteResultStore(
            new ResultLogConfig { Folder = folder, Sqlite = true, Jsonl = false },
            NullLogger<SqliteResultStore>.Instance);
    }

    private static void Insert(
        SqliteResultStore db, string recipe, DateTimeOffset at,
        double? x, double? y, double? angle, int code)
    {
        var poses = code == 0 && x is not null
            ? new ResultPoseLog[] { new(x.Value, y ?? 0, angle ?? 0, 0.9) }
            : Array.Empty<ResultPoseLog>();
        db.Insert(new ResultLogEntry(
            at.ToString("O"), recipe, "st", "cam",
            x, y, angle, code == 0 ? 0.9 : null,
            poses.Length, 12, code, code == 0 ? "" : "fail", poses),
            at.ToUnixTimeMilliseconds());
    }
}
