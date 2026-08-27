using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobotVision.Hosting;

namespace RobotVision.WpfHost.Features.Analysis;

public sealed record AnalysisRow(
    string TimeText, string Recipe, string Station, string Camera,
    string XText, string YText, string AngleText, string ConfText,
    string ResultText, string ElapsedText, bool Ok, int Code);

public sealed record HistogramBar(string Label, int Count, double Ratio);

public sealed record CodeShareRow(string Label, long Count, double Ratio, string CountText);

/// <summary>
/// 结果分析：只读查询本机 SQLite（不挡检测节拍）。
/// 筛选时间/配方/成败 → 合格率与位姿均值、角度分布、错误码分布、明细表。
/// </summary>
public partial class AnalysisViewModel : ObservableObject
{
    public const string AllRecipes = "全部配方";
    public const string RangeToday = "今天";
    public const string Range7Days = "近7天";
    public const string Range30Days = "近30天";
    public const string RangeAll = "全部";
    public const string OutcomeAll = "全部";
    public const string OutcomeOk = "合格";
    public const string OutcomeFail = "失败";

    private const int TableLimit = 500;
    private const int HistogramBins = 12;

    private readonly SqliteResultStore _db;
    private readonly ResultLogStore? _results;
    private readonly DispatcherTimer _timer;
    private CancellationTokenSource? _refreshCts;
    private bool _ready;
    private bool _suppressFilter;

    /// <summary>测试可替换时钟，保证「今天」边界可断言。</summary>
    internal Func<DateTimeOffset> Clock { get; set; } = () => DateTimeOffset.Now;

    public ObservableCollection<string> RangeOptions { get; } = [RangeToday, Range7Days, Range30Days, RangeAll];

    public ObservableCollection<string> RecipeOptions { get; } = [];

    public ObservableCollection<string> OutcomeOptions { get; } = [OutcomeAll, OutcomeOk, OutcomeFail];

    public ObservableCollection<AnalysisRow> Rows { get; } = [];

    public ObservableCollection<HistogramBar> AngleBars { get; } = [];

    public ObservableCollection<CodeShareRow> CodeRows { get; } = [];

    [ObservableProperty]
    private string _range = RangeToday;

    [ObservableProperty]
    private string _recipeFilter = AllRecipes;

    [ObservableProperty]
    private string _outcome = OutcomeAll;

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
    private string _avgMsText = "—";

    [ObservableProperty]
    private string _tableSummary = "";

    [ObservableProperty]
    private string _angleSummary = "暂无角度样本";

    [ObservableProperty]
    private string _codeSummary = "暂无错误码统计";

    [ObservableProperty]
    private bool _hasRows;

    [ObservableProperty]
    private bool _hasAngleBars;

    [ObservableProperty]
    private bool _hasCodeRows;

    [ObservableProperty]
    private bool _isBusy;

    public AnalysisViewModel(SqliteResultStore db, ResultLogStore? results = null)
    {
        _db = db;
        _results = results;
        RecipeOptions.Add(AllRecipes);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _timer.Tick += (_, _) =>
        {
            if (_ready)
                _ = RefreshAsync();
        };
    }

    public void StartTimer() => _timer.Start();

    public void StopTimer() => _timer.Stop();

    partial void OnRangeChanged(string value)
    {
        if (_ready && !_suppressFilter)
            _ = RefreshAsync();
    }

    partial void OnRecipeFilterChanged(string value)
    {
        if (_ready && !_suppressFilter)
            _ = RefreshAsync();
    }

    partial void OnOutcomeChanged(string value)
    {
        if (_ready && !_suppressFilter)
            _ = RefreshAsync();
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
        try
        {
            var snapshot = await Task.Run(() => LoadSnapshot(query), token);
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
        return new ResultDbQuery
        {
            From = from,
            To = now,
            Recipe = recipe,
            OkOnly = okOnly,
            Limit = TableLimit,
        };
    }

    internal static List<HistogramBar> BuildHistogram(IReadOnlyList<double> values, int binCount = HistogramBins)
    {
        if (values.Count == 0 || binCount < 1)
            return [];
        var min = values.Min();
        var max = values.Max();
        if (!double.IsFinite(min) || !double.IsFinite(max))
            return [];
        if (Math.Abs(max - min) < 1e-9)
            return [new HistogramBar($"{min.ToString("0.###", CultureInfo.InvariantCulture)}°", values.Count, 1)];

        var span = max - min;
        var bins = new int[binCount];
        foreach (var value in values)
        {
            if (!double.IsFinite(value))
                continue;
            var index = (int)((value - min) / span * binCount);
            if (index >= binCount)
                index = binCount - 1;
            if (index < 0)
                index = 0;
            bins[index]++;
        }

        var peak = bins.Max();
        var bars = new List<HistogramBar>(binCount);
        for (var i = 0; i < binCount; i++)
        {
            var start = min + span * i / binCount;
            var end = min + span * (i + 1) / binCount;
            var ratio = peak == 0 ? 0 : (double)bins[i] / peak;
            bars.Add(new HistogramBar(
                $"{start.ToString("0.0", CultureInfo.InvariantCulture)}~{end.ToString("0.0", CultureInfo.InvariantCulture)}°",
                bins[i], ratio));
        }
        return bars;
    }

    private Snapshot LoadSnapshot(ResultDbQuery query)
    {
        return new Snapshot(
            _db.Summarize(query),
            _db.Query(query),
            _db.ListRecipes(),
            _db.QueryAngles(query),
            _db.CountByCode(query),
            File.Exists(_db.DatabasePath));
    }

    private void ApplySnapshot(Snapshot snapshot)
    {
        _suppressFilter = true;
        try
        {
            var keepRecipe = RecipeFilter;
            RecipeOptions.Clear();
            RecipeOptions.Add(AllRecipes);
            foreach (var name in snapshot.Recipes)
                RecipeOptions.Add(name);
            RecipeFilter = RecipeOptions.Contains(keepRecipe) ? keepRecipe : AllRecipes;
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
            : $"n={snapshot.Angles.Count} · 均角 {AvgAngleText}";

        var codePeak = snapshot.Codes.Count == 0 ? 0 : snapshot.Codes.Max(c => c.Count);
        CodeRows.Clear();
        foreach (var item in snapshot.Codes)
        {
            var label = item.Code == 0 ? "合格" : item.Code.ToString(CultureInfo.InvariantCulture);
            var ratio = codePeak == 0 ? 0 : (double)item.Count / codePeak;
            CodeRows.Add(new CodeShareRow(label, item.Count, ratio, item.Count.ToString("N0")));
        }
        HasCodeRows = CodeRows.Count > 0;
        CodeSummary = snapshot.Codes.Count == 0 ? "暂无分布" : $"{snapshot.Codes.Count} 种结果码";

        if (!snapshot.DatabaseExists)
        {
            Message = _results is { SqliteEnabled: false }
                ? "尚未生成结果库。请在 appsettings 打开 ResultLog.Sqlite 后触发检测。"
                : "暂无结果记录。触发检测后将写入 data/results/results.db。";
        }
        else if (summary.Total == 0)
        {
            Message = "该筛选条件下没有记录";
        }
        else
        {
            var writeHint = _results is { SqliteEnabled: false } ? "（当前未写入新结果）" : "";
            Message = $"{Range} · {RecipeFilter} · {Outcome}{writeHint}";
        }
    }

    private static AnalysisRow ToRow(ResultDbRow row)
    {
        var ok = row.Code == 0;
        var time = DateTimeOffset.TryParse(row.T, out var parsed)
            ? parsed.ToString("MM-dd HH:mm:ss")
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

    private sealed record Snapshot(
        ResultDbSummary Summary,
        IReadOnlyList<ResultDbRow> Rows,
        IReadOnlyList<string> Recipes,
        IReadOnlyList<double> Angles,
        IReadOnlyList<ResultCodeCount> Codes,
        bool DatabaseExists);
}
