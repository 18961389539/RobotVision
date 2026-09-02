using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OxyPlot;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Analysis;

public sealed record AnalysisRow(
    string TimeText, string Recipe, string Station, string Camera,
    string XText, string YText, string AngleText, string ConfText,
    string ResultText, string ElapsedText, bool Ok, int Code);

public sealed record HistogramBar(string Label, int Count, double Ratio);

public sealed record CodeShareRow(string Label, long Count, double Ratio, string CountText);

public sealed record AnalysisTrendBar(string Label, long Total, long Ok, double Ratio, string Detail);

public sealed record AnalysisRecipeYield(
    string Recipe, long Total, long Ok, double Yield, double Ratio, string YieldText);

/// <summary>
/// 结果分析：只读查询本机 SQLite（不挡检测节拍）。
/// 筛选时间/配方/工位/相机/成败/关键字 → 合格率、离散度、角度分布、结果码、
/// 时间趋势、按配方合格率、明细表；可导出 CSV。
/// </summary>
public partial class AnalysisViewModel : ObservableObject, IDisposable
{
    public const string AllRecipes = "全部配方";
    public const string AllStations = "全部工位";
    public const string AllCameras = "全部相机";
    public const string RangeToday = "今天";
    public const string Range7Days = "近7天";
    public const string Range30Days = "近30天";
    public const string RangeAll = "全部";
    public const string OutcomeAll = "全部";
    public const string OutcomeOk = "合格";
    public const string OutcomeFail = "失败";

    private const int TableLimit = 500;
    private const int HistogramBins = ResultAnalysis.DefaultHistogramBins;

    private readonly SqliteResultStore _db;
    private readonly ResultLogStore? _results;
    private readonly RecipeLoader? _recipes;
    private readonly ILogger<AnalysisViewModel> _log;
    private readonly DispatcherTimer _timer;
    private CancellationTokenSource? _refreshCts;
    private bool _ready;
    private bool _suppressFilter;

    /// <summary>测试可替换时钟，保证「今天」边界可断言。</summary>
    internal Func<DateTimeOffset> Clock { get; set; } = () => DateTimeOffset.Now;

    public ObservableCollection<string> RangeOptions { get; } = [RangeToday, Range7Days, Range30Days, RangeAll];

    public ObservableCollection<string> RecipeOptions { get; } = [];

    public ObservableCollection<string> StationOptions { get; } = [];

    public ObservableCollection<string> CameraOptions { get; } = [];

    public ObservableCollection<string> OutcomeOptions { get; } = [OutcomeAll, OutcomeOk, OutcomeFail];

    public ObservableCollection<AnalysisRow> Rows { get; } = [];

    public ObservableCollection<HistogramBar> AngleBars { get; } = [];

    public ObservableCollection<CodeShareRow> CodeRows { get; } = [];

    public ObservableCollection<AnalysisTrendBar> TrendBars { get; } = [];

    public ObservableCollection<AnalysisRecipeYield> RecipeYields { get; } = [];

    [ObservableProperty]
    private string _range = RangeToday;

    [ObservableProperty]
    private string _recipeFilter = AllRecipes;

    [ObservableProperty]
    private string _stationFilter = AllStations;

    [ObservableProperty]
    private string _cameraFilter = AllCameras;

    [ObservableProperty]
    private string _outcome = OutcomeAll;

    [ObservableProperty]
    private string _keyword = "";

    [ObservableProperty]
    private string _message = "尚未加载";

    [ObservableProperty]
    private string _totalText = "—";

    [ObservableProperty]
    private string _okText = "—";

    [ObservableProperty]
    private string _failText = "—";

    [ObservableProperty]
    private string _yieldText = "—";

    [ObservableProperty]
    private string _avgXText = "—";

    [ObservableProperty]
    private string _avgYText = "—";

    [ObservableProperty]
    private string _avgAngleText = "—";

    [ObservableProperty]
    private string _angleStdText = "—";

    [ObservableProperty]
    private string _avgMsText = "—";

    [ObservableProperty]
    private string _tableSummary = "";

    [ObservableProperty]
    private string _angleSummary = "暂无角度样本";

    [ObservableProperty]
    private string _codeSummary = "暂无错误码统计";

    [ObservableProperty]
    private string _trendSummary = "暂无趋势";

    [ObservableProperty]
    private string _recipeYieldSummary = "暂无配方统计";

    [ObservableProperty]
    private bool _hasRows;

    [ObservableProperty]
    private bool _hasAngleBars;

    [ObservableProperty]
    private bool _hasCodeRows;

    [ObservableProperty]
    private bool _hasTrendBars;

    [ObservableProperty]
    private bool _hasRecipeYields;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private PlotModel _anglePlot = AnalysisPlots.Empty("暂无角度样本");

    [ObservableProperty]
    private PlotModel _codePlot = AnalysisPlots.Empty("暂无结果码");

    [ObservableProperty]
    private PlotModel _trendPlot = AnalysisPlots.Empty("暂无趋势");

    [ObservableProperty]
    private PlotModel _recipePlot = AnalysisPlots.Empty("暂无配方统计");

    [ObservableProperty]
    private PlotModel _xyPlot = AnalysisPlots.Empty("暂无 XY 坐标");

    [ObservableProperty]
    private PlotModel _elapsedPlot = AnalysisPlots.Empty("暂无耗时样本");

    [ObservableProperty]
    private string _xySummary = "暂无 XY 坐标";

    [ObservableProperty]
    private string _elapsedSummary = "暂无耗时样本";

    [ObservableProperty]
    private string _healthHint = "";

    [ObservableProperty]
    private bool _hasHealthHint;

    [ObservableProperty]
    private bool _hasXyPlot;

    [ObservableProperty]
    private bool _hasElapsedBars;

    public void InvalidatePlots()
    {
        AnglePlot.InvalidatePlot(true);
        CodePlot.InvalidatePlot(true);
        TrendPlot.InvalidatePlot(true);
        RecipePlot.InvalidatePlot(true);
        XyPlot.InvalidatePlot(true);
        ElapsedPlot.InvalidatePlot(true);
    }

    public AnalysisViewModel(
        SqliteResultStore db,
        ILogger<AnalysisViewModel> log,
        ResultLogStore? results = null,
        RecipeLoader? recipes = null)
    {
        _db = db;
        _log = log;
        _results = results;
        _recipes = recipes;
        RecipeOptions.Add(AllRecipes);
        StationOptions.Add(AllStations);
        CameraOptions.Add(AllCameras);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _timer.Tick += (_, _) =>
        {
            if (_ready)
                ScheduleRefresh();
        };
    }

    public void ScheduleRefresh() => UiFireAndForget.Run(RefreshAsync, _log);

    public void StartTimer() => _timer.Start();

    public void StopTimer() => _timer.Stop();

    partial void OnRangeChanged(string value)
    {
        if (_ready && !_suppressFilter)
            ScheduleRefresh();
    }

    partial void OnRecipeFilterChanged(string value)
    {
        if (_ready && !_suppressFilter)
            ScheduleRefresh();
    }

    partial void OnStationFilterChanged(string value)
    {
        if (_ready && !_suppressFilter)
            ScheduleRefresh();
    }

    partial void OnCameraFilterChanged(string value)
    {
        if (_ready && !_suppressFilter)
            ScheduleRefresh();
    }

    partial void OnOutcomeChanged(string value)
    {
        if (_ready && !_suppressFilter)
            ScheduleRefresh();
    }

    partial void OnKeywordChanged(string value)
    {
        if (_ready && !_suppressFilter)
            ScheduleRefresh();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        _ready = true;
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        var cts = _refreshCts = new CancellationTokenSource();
        var token = cts.Token;
        IsBusy = true;
        Message = "加载中…";

        var query = BuildQuery();
        var grain = Range == RangeToday ? "hour" : "day";
        try
        {
            var snapshot = await Task.Run(() => LoadSnapshot(query, grain), token);
            if (token.IsCancellationRequested)
                return;
            ApplySnapshot(snapshot);
        }
        catch (OperationCanceledException)
        {
            // 新的刷新已开始
        }
        catch (Exception ex)
        {
            WpfUiLog.AnalysisLoadFailed(_log, ex);
            Message = "读取结果库失败: " + ex.Message;
            HasRows = false;
        }
        finally
        {
            if (!token.IsCancellationRequested)
                IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenFolder() => Explorer.OpenFolder(_db.Folder);

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        IsBusy = true;
        Message = "导出中…";
        try
        {
            var result = await Task.Run(ExportCsvCore).ConfigureAwait(true);
            if (result is null)
            {
                Message = "没有可导出的记录";
                return;
            }

            Message = $"已导出 {result.Value.RowCount} 条 → {Path.GetFileName(result.Value.Path)}";
            Explorer.OpenFolder(_db.Folder);
        }
        catch (Exception ex)
        {
            WpfUiLog.AnalysisLoadFailed(_log, ex);
            Message = "导出失败: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private (string Path, int RowCount)? ExportCsvCore()
    {
        var query = BuildQuery() with { Limit = 10_000, Offset = 0 };
        var rows = _db.Query(query);
        if (rows.Count == 0)
            return null;

        Directory.CreateDirectory(_db.Folder);
        var path = Path.Combine(_db.Folder, $"analysis_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        var sb = new StringBuilder();
        sb.AppendLine("time,recipe,station,camera,x,y,angle,confidence,count,elapsed_ms,code,message");
        foreach (var row in rows)
        {
            sb.Append(Csv(row.T)).Append(',')
                .Append(Csv(row.Recipe)).Append(',')
                .Append(Csv(row.Station)).Append(',')
                .Append(Csv(row.Camera)).Append(',')
                .Append(Num(row.X)).Append(',')
                .Append(Num(row.Y)).Append(',')
                .Append(Num(row.Angle)).Append(',')
                .Append(Num(row.Confidence)).Append(',')
                .Append(row.Count.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(row.ElapsedMs.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(row.Code.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(Csv(row.Message)).AppendLine();
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return (path, rows.Count);
    }

    internal ResultDbQuery BuildQuery()
    {
        var now = Clock();
        DateTimeOffset? from = Range switch
        {
            RangeToday => new DateTimeOffset(now.Date, now.Offset),
            Range7Days => now.AddDays(-7),
            Range30Days => now.AddDays(-30),
            _ => null,
        };
        bool? okOnly = Outcome switch
        {
            OutcomeOk => true,
            OutcomeFail => false,
            _ => null,
        };
        var recipe = string.IsNullOrWhiteSpace(RecipeFilter) || RecipeFilter == AllRecipes
            ? null
            : RecipeFilter;
        var station = string.IsNullOrWhiteSpace(StationFilter) || StationFilter == AllStations
            ? null
            : StationFilter;
        var camera = string.IsNullOrWhiteSpace(CameraFilter) || CameraFilter == AllCameras
            ? null
            : CameraFilter;
        return new ResultDbQuery
        {
            From = from,
            To = now,
            Recipe = recipe,
            Station = station,
            Camera = camera,
            OkOnly = okOnly,
            MessageContains = string.IsNullOrWhiteSpace(Keyword) ? null : Keyword.Trim(),
            Limit = TableLimit,
        };
    }

    internal static List<HistogramBar> BuildHistogram(IReadOnlyList<double> values, int binCount = HistogramBins) =>
        ResultAnalysis.BuildHistogram(values, binCount)
            .Select(b => new HistogramBar(b.Label, b.Count, b.Ratio))
            .ToList();

    private Snapshot LoadSnapshot(ResultDbQuery query, string grain)
    {
        var okQuery = query with { OkOnly = true, Code = null };
        return new Snapshot(
            _db.Summarize(query),
            _db.QuerySpread(query),
            _db.Query(query),
            _db.ListRecipes(),
            _db.ListStations(),
            _db.ListCameras(),
            _db.QueryAngles(query),
            _db.CountByCode(query),
            _db.QueryTrend(query, grain),
            _db.SummarizeByRecipe(query),
            _db.QueryXy(query),
            _db.QueryElapsedMs(query),
            _db.QueryAngles(okQuery),
            _db.QuerySpread(okQuery),
            File.Exists(_db.DatabasePath));
    }

    private void ApplySnapshot(Snapshot snapshot)
    {
        _suppressFilter = true;
        try
        {
            ReplaceOptions(RecipeOptions, AllRecipes, snapshot.Recipes, () => RecipeFilter, v => RecipeFilter = v);
            ReplaceOptions(StationOptions, AllStations, snapshot.Stations, () => StationFilter, v => StationFilter = v);
            ReplaceOptions(CameraOptions, AllCameras, snapshot.Cameras, () => CameraFilter, v => CameraFilter = v);
        }
        finally
        {
            _suppressFilter = false;
        }

        var summary = snapshot.Summary;
        TotalText = summary.Total.ToString("N0", CultureInfo.InvariantCulture);
        OkText = summary.Ok.ToString("N0", CultureInfo.InvariantCulture);
        FailText = summary.Failed.ToString("N0", CultureInfo.InvariantCulture);
        YieldText = summary.Total == 0
            ? "—"
            : (100.0 * summary.Ok / summary.Total).ToString("0.0", CultureInfo.InvariantCulture) + "%";
        AvgXText = FormatNum(summary.AvgX);
        AvgYText = FormatNum(summary.AvgY);
        AvgAngleText = summary.AvgAngle is { } ang ? ang.ToString("0.###", CultureInfo.InvariantCulture) + "°" : "—";
        AngleStdText = snapshot.Spread.StdAngle is { } std
            ? "σ " + std.ToString("0.###", CultureInfo.InvariantCulture) + "°"
            : "σ —";
        AvgMsText = summary.AvgMs is { } ms ? ms.ToString("0", CultureInfo.InvariantCulture) + " ms" : "—";

        Rows.Clear();
        foreach (var row in snapshot.Rows)
            Rows.Add(ToRow(row));
        HasRows = Rows.Count > 0;
        TableSummary = summary.Total == 0
            ? ""
            : summary.Total > TableLimit
                ? $"显示最近 {Rows.Count} 条 · 共 {summary.Total:N0} 条"
                : $"共 {summary.Total:N0} 条";

        AngleBars.Clear();
        foreach (var bar in BuildHistogram(snapshot.Angles))
            AngleBars.Add(bar);
        HasAngleBars = AngleBars.Count > 0;
        AngleSummary = snapshot.Angles.Count == 0
            ? "暂无角度样本"
            : SpreadLine(snapshot.Spread);

        var codePeak = snapshot.Codes.Count == 0 ? 0 : snapshot.Codes.Max(c => c.Count);
        CodeRows.Clear();
        foreach (var item in snapshot.Codes)
        {
            var ratio = codePeak == 0 ? 0 : (double)item.Count / codePeak;
            CodeRows.Add(new CodeShareRow(
                ResultAnalysis.DescribeCode(item.Code), item.Count, ratio,
                item.Count.ToString("N0", CultureInfo.InvariantCulture)));
        }
        HasCodeRows = CodeRows.Count > 0;
        CodeSummary = snapshot.Codes.Count == 0 ? "暂无分布" : $"{snapshot.Codes.Count} 种结果码";

        var trendPeak = snapshot.Trend.Count == 0 ? 0 : snapshot.Trend.Max(t => t.Total);
        TrendBars.Clear();
        foreach (var bucket in snapshot.Trend)
        {
            var ratio = trendPeak == 0 ? 0 : (double)bucket.Total / trendPeak;
            var yield = bucket.Total == 0 ? 0 : 100.0 * bucket.Ok / bucket.Total;
            TrendBars.Add(new AnalysisTrendBar(
                ShortTrendLabel(bucket.Label),
                bucket.Total,
                bucket.Ok,
                ratio,
                $"{bucket.Total} · {yield.ToString("0.0", CultureInfo.InvariantCulture)}%"));
        }
        HasTrendBars = TrendBars.Count > 0;
        TrendSummary = snapshot.Trend.Count == 0
            ? "暂无趋势"
            : Range == RangeToday
                ? $"按小时 · {snapshot.Trend.Count} 桶"
                : $"按日 · {snapshot.Trend.Count} 桶";

        var yieldPeak = snapshot.ByRecipe.Count == 0 ? 0 : snapshot.ByRecipe.Max(r => r.Total);
        RecipeYields.Clear();
        foreach (var item in snapshot.ByRecipe)
        {
            var yld = item.Total == 0 ? 0 : 100.0 * item.Ok / item.Total;
            var ratio = yieldPeak == 0 ? 0 : (double)item.Total / yieldPeak;
            RecipeYields.Add(new AnalysisRecipeYield(
                item.Recipe, item.Total, item.Ok, yld, ratio,
                yld.ToString("0.0", CultureInfo.InvariantCulture) + "%"));
        }
        HasRecipeYields = RecipeYields.Count > 0;
        RecipeYieldSummary = snapshot.ByRecipe.Count == 0
            ? "暂无配方统计"
            : $"{snapshot.ByRecipe.Count} 个配方";

        HasXyPlot = snapshot.Xy.Count > 0;
        XySummary = snapshot.Xy.Count == 0
            ? "暂无 XY 坐标"
            : $"n={snapshot.Xy.Count} · 绿=合格 红=失败";
        HasElapsedBars = snapshot.Elapsed.Count > 0;
        ElapsedSummary = snapshot.Elapsed.Count == 0
            ? "暂无耗时样本"
            : snapshot.Spread.StdMs is { } stdMs
                ? $"n={snapshot.Elapsed.Count} · σ {stdMs.ToString("0", CultureInfo.InvariantCulture)} ms"
                : $"n={snapshot.Elapsed.Count}";

        AnglePlot = AnalysisPlots.Histogram(
            ResultAnalysis.BuildHistogram(snapshot.Angles),
            mean: summary.AvgAngle,
            std: snapshot.Spread.StdAngle);
        CodePlot = AnalysisPlots.CodeShare(snapshot.Codes);
        TrendPlot = AnalysisPlots.Trend(snapshot.Trend);
        RecipePlot = AnalysisPlots.RecipeYield(snapshot.ByRecipe);
        XyPlot = AnalysisPlots.Scatter(
            snapshot.Xy, summary.AvgX, summary.AvgY, snapshot.Spread.StdX, snapshot.Spread.StdY);
        ElapsedPlot = AnalysisPlots.Histogram(
            ResultAnalysis.BuildHistogram(snapshot.Elapsed, unit: " ms"),
            AnalysisPlots.Yield,
            summary.AvgMs,
            snapshot.Spread.StdMs,
            " ms");
        InvalidatePlots();

        var teachPeak = 0.0;
        if (_recipes is not null &&
            !string.IsNullOrWhiteSpace(RecipeFilter) &&
            RecipeFilter != AllRecipes &&
            RecipeLoader.IsValidRecipeName(RecipeFilter))
        {
            try
            {
                teachPeak = _recipes.Get(RecipeFilter).Template.TeachPeakScore;
            }
            catch (Exception)
            {
                teachPeak = 0;
            }
        }

        var hints = RecipeHealthAdvisor.Analyze(
            summary.Total, snapshot.Codes, snapshot.OkAngles, snapshot.OkSpread, teachPeak);
        HealthHint = string.Join(Environment.NewLine, hints.Select(h => h.Message));
        HasHealthHint = hints.Count > 0;

        if (!snapshot.DatabaseExists)
        {
            Message = _results is { SqliteEnabled: false }
                ? "尚未生成结果库。请在「服务设置」开启结果留档并勾选 SQLite 后触发检测。"
                : "暂无结果记录。触发检测后将写入 data/results/results.db。";
        }
        else if (summary.Total == 0)
        {
            Message = "该筛选条件下没有记录";
        }
        else
        {
            var writeHint = _results is { SqliteEnabled: false } ? "（当前未写入新结果）" : "";
            Message = $"{Range} · {RecipeFilter} · {StationFilter} · {Outcome}{writeHint}";
        }
    }

    private static void ReplaceOptions(
        ObservableCollection<string> target, string allLabel, IReadOnlyList<string> names,
        Func<string> get, Action<string> set)
    {
        var keep = get();
        target.Clear();
        target.Add(allLabel);
        foreach (var name in names)
            target.Add(name);
        set(target.Contains(keep) ? keep : allLabel);
    }

    private static string SpreadLine(ResultPoseSpread spread)
    {
        if (spread.StdAngle is null && spread.MinAngle is null)
            return "暂无角度样本";
        var std = spread.StdAngle is { } s ? $"σ {s.ToString("0.###", CultureInfo.InvariantCulture)}°" : "σ —";
        if (spread.MinAngle is { } min && spread.MaxAngle is { } max)
            return $"{std} · {min.ToString("0.0", CultureInfo.InvariantCulture)}~{max.ToString("0.0", CultureInfo.InvariantCulture)}°";
        return std;
    }

    private static string ShortTrendLabel(string label)
    {
        if (label.Length >= 16 && label[10] == ' ')
            return label[11..]; // HH:00
        if (label.Length >= 10)
            return label[5..]; // MM-DD
        return label;
    }

    private static AnalysisRow ToRow(ResultDbRow row)
    {
        var ok = row.Code == 0;
        var time = DateTimeOffset.TryParse(row.T, out var parsed)
            ? parsed.ToString("MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            : row.T;
        return new AnalysisRow(
            time, row.Recipe, row.Station, row.Camera,
            FormatNum(row.X), FormatNum(row.Y),
            row.Angle is { } a ? a.ToString("0.###", CultureInfo.InvariantCulture) : "—",
            row.Confidence is { } c ? c.ToString("0.###", CultureInfo.InvariantCulture) : "—",
            ok ? "OK" : row.Code.ToString(CultureInfo.InvariantCulture),
            row.ElapsedMs.ToString("0", CultureInfo.InvariantCulture),
            ok, row.Code);
    }

    private static string FormatNum(double? value) =>
        value is { } n ? n.ToString("0.###", CultureInfo.InvariantCulture) : "—";

    private static string Num(double? value) =>
        value is { } n ? n.ToString("0.###", CultureInfo.InvariantCulture) : "";

    private static string Csv(string? value)
    {
        var s = value ?? "";
        if (s.Contains(',', StringComparison.Ordinal) || s.Contains('"', StringComparison.Ordinal) || s.Contains('\n', StringComparison.Ordinal) || s.Contains('\r', StringComparison.Ordinal))
            return "\"" + s.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        return s;
    }

    public void Dispose()
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = null;
        _timer.Stop();
    }

    private sealed record Snapshot(
        ResultDbSummary Summary,
        ResultPoseSpread Spread,
        IReadOnlyList<ResultDbRow> Rows,
        IReadOnlyList<string> Recipes,
        IReadOnlyList<string> Stations,
        IReadOnlyList<string> Cameras,
        IReadOnlyList<double> Angles,
        IReadOnlyList<ResultCodeCount> Codes,
        IReadOnlyList<ResultTrendBucket> Trend,
        IReadOnlyList<ResultRecipeStat> ByRecipe,
        IReadOnlyList<ResultXyPoint> Xy,
        IReadOnlyList<double> Elapsed,
        IReadOnlyList<double> OkAngles,
        ResultPoseSpread OkSpread,
        bool DatabaseExists);
}
