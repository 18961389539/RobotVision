using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobotVision.Hosting;

namespace RobotVision.WpfHost;

public sealed record LogFileItem(string Name, string DateText, string SizeText, string Path);

public sealed partial record LogRow
{
    public string Time { get; init; } = "";
    public string Level { get; init; } = "";
    public string Source { get; init; } = "";
    public string Message { get; init; } = "";

    [GeneratedRegex(@"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}) \[(\w{1,3})\] (.+?): (.*)$")]
    private static partial Regex LinePattern();

    /// <summary>
    /// 解析 Serilog 文件行：时间 [级别] 来源: 消息。
    /// 不匹配的行（异常堆栈等续行）并入上一条的 Message。
    /// </summary>
    public static List<LogRow> ParseAll(IEnumerable<string> lines)
    {
        var rows = new List<LogRow>();
        LogRow? last = null;
        foreach (var raw in lines)
        {
            var match = LinePattern().Match(raw);
            if (match.Success)
            {
                last = new LogRow
                {
                    Time = match.Groups[1].Value[11..], // 只保留时分秒.毫秒，日期在文件名上
                    Level = match.Groups[2].Value,
                    Source = match.Groups[3].Value,
                    Message = match.Groups[4].Value,
                };
                rows.Add(last);
            }
            else if (last is not null)
            {
                last = last with { Message = last.Message + Environment.NewLine + raw };
                rows[^1] = last;
            }
        }
        return rows;
    }

    /// <summary>该行是否为独立日志头（非异常堆栈等续行）。增量追加时用于续行归并判断。</summary>
    public static bool IsHeaderLine(string raw) => LinePattern().IsMatch(raw);
}

/// <summary>
/// 日志文件浏览：按天列文件、级别/关键词过滤、异常堆栈并入所属条目、
/// 可选跟随文件尾部（2s 轮询，写日志的进程是本程序自身，读文件无锁冲突）。
/// </summary>
public partial class LogsViewModel : ObservableObject
{
    /// <summary>超长文件只保留尾部，避免 UI 卡死。</summary>
    private const int MaxRows = 20000;

    private readonly string _folder;
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _filterDebounce;
    private List<LogRow> _allRows = [];
    private DateTime _lastWriteUtc;
    private string _loadedPath = "";
    private CancellationTokenSource? _reloadCts;

    /// <summary>已解析到文件的行数（跟随模式只增量解析新增行，避免每 2s 全量解析大文件）。</summary>
    private int _parsedLineCount;

    public ObservableCollection<LogFileItem> Files { get; } = [];

    public ObservableCollection<LogRow> Rows { get; } = [];

    [ObservableProperty]
    private LogFileItem? _selectedFile;

    [ObservableProperty]
    private bool _includeDebug;

    [ObservableProperty]
    private bool _includeInfo = true;

    [ObservableProperty]
    private bool _includeWarning = true;

    [ObservableProperty]
    private bool _includeError = true;

    [ObservableProperty]
    private string _keyword = "";

    [ObservableProperty]
    private bool _followTail;

    [ObservableProperty]
    private string _status = "";

    public LogsViewModel(AppConfig cfg)
    {
        _folder = AppConfigExtensions.ResolveFolder(cfg.FileLogging.Folder);
        RefreshFiles();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => PollFileChange();

        // 关键词过滤防抖：连续敲字不逐字符重建列表（大日志文件下避免 UI 卡顿）
        _filterDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _filterDebounce.Tick += (_, _) =>
        {
            _filterDebounce.Stop();
            ApplyFilter();
        };
    }

    public void StartTimer() => _timer.Start();

    public void StopTimer() => _timer.Stop();

    [RelayCommand]
    public void RefreshFiles()
    {
        var keepPath = SelectedFile?.Path;
        Files.Clear();
        if (!Directory.Exists(_folder))
        {
            SelectedFile = null;
            Status = $"日志目录不存在: {_folder}";
            return;
        }

        foreach (var file in new DirectoryInfo(_folder)
                     .EnumerateFiles("robotvision-*.log")
                     .OrderByDescending(f => f.Name))
        {
            Files.Add(new LogFileItem(
                file.Name, file.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                $"{file.Length / 1024.0:0.0} KB", file.FullName));
        }

        SelectedFile = string.IsNullOrWhiteSpace(keepPath)
            ? Files.FirstOrDefault()
            : Files.FirstOrDefault(f => f.Path == keepPath);
        Status = $"{Files.Count} 个日志文件";
    }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        if (SelectedFile is null)
        {
            _allRows = [];
            ApplyFilter();
            Status = "没有日志文件（启用文件日志后生成）";
            return;
        }

        _reloadCts?.Cancel();
        _reloadCts?.Dispose();
        var cts = _reloadCts = new CancellationTokenSource();

        try
        {
            var path = SelectedFile.Path;
            _loadedPath = path;
            _lastWriteUtc = File.GetLastWriteTimeUtc(path);
            Status = $"加载中：{Path.GetFileName(path)} …";

            // 文件读取 + 行解析在后台线程（大日志文件不冻结 UI）
            var (rows, total, trimmed) = await Task.Run(() =>
            {
                var lines = File.ReadAllLines(path);
                var totalCount = lines.Length;
                if (lines.Length > MaxRows)
                    lines = lines[(lines.Length - MaxRows)..];
                return (LogRow.ParseAll(lines), totalCount, lines.Length != totalCount);
            }, cts.Token);

            if (cts.IsCancellationRequested)
                return;

            _allRows = rows;
            _parsedLineCount = total; // 增量起点 = 文件全量行数
            ApplyFilter();
            Status = $"{SelectedFile.Name} · {total} 行" +
                     (trimmed ? $"（仅显示尾部 {MaxRows} 行）" : "");
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException ex)
        {
            Status = $"读取失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenFolder() => RecipeViewModel.ShellOpen(_folder);

    [RelayCommand]
    private void ClearFilter()
    {
        IncludeDebug = false;
        IncludeInfo = true;
        IncludeWarning = true;
        IncludeError = true;
        Keyword = "";
    }

    partial void OnSelectedFileChanged(LogFileItem? value) => _ = ReloadAsync();

    partial void OnIncludeDebugChanged(bool value) => ApplyFilter();
    partial void OnIncludeInfoChanged(bool value) => ApplyFilter();
    partial void OnIncludeWarningChanged(bool value) => ApplyFilter();
    partial void OnIncludeErrorChanged(bool value) => ApplyFilter();

    /// <summary>关键词过滤防抖触发（300ms 内停止输入才重建列表）。</summary>
    partial void OnKeywordChanged(string value)
    {
        _filterDebounce.Stop();
        _filterDebounce.Start();
    }

    private void ApplyFilter()
    {
        Rows.Clear();
        foreach (var row in _allRows)
        {
            if (MatchesFilter(row))
                Rows.Add(row);
        }
    }

    /// <summary>单条是否通过当前级别/关键词过滤（增量追加与全量重建共用）。</summary>
    private bool MatchesFilter(LogRow row)
    {
        if (!LevelEnabled(row.Level))
            return false;
        var keyword = Keyword.Trim();
        if (keyword.Length > 0 &&
            !row.Message.Contains(keyword, StringComparison.OrdinalIgnoreCase) &&
            !row.Source.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    /// <summary>增量追加：只对新增行做过滤并追加到显示列表。</summary>
    private void ApplyFilterAppend(IEnumerable<LogRow> added)
    {
        foreach (var row in added)
        {
            if (MatchesFilter(row))
                Rows.Add(row);
        }
    }

    private bool LevelEnabled(string level) => level switch
    {
        "DBG" or "VRB" => IncludeDebug,
        "INF" => IncludeInfo,
        "WRN" => IncludeWarning,
        "ERR" or "FTL" => IncludeError,
        _ => true,
    };

    private void PollFileChange()
    {
        if (!FollowTail || _loadedPath.Length == 0 || !File.Exists(_loadedPath))
            return;

        try
        {
            var write = File.GetLastWriteTimeUtc(_loadedPath);
            if (write != _lastWriteUtc)
            {
                _lastWriteUtc = write;
                _ = AppendTailAsync(); // 跟随刷新：增量解析新增行，不阻塞 UI
            }
        }
        catch (IOException)
        {
            // 文件正被滚动清理时忽略，下轮再试
        }
    }

    /// <summary>
    /// 跟随模式增量刷新：只解析自上次以来的新增行（文件读取在后台线程）。
    /// 文件被滚动重建/行数超上限时回退到全量重载。
    /// </summary>
    private async Task AppendTailAsync()
    {
        try
        {
            var (tail, total, needFullReload) = await Task.Run(() =>
            {
                var lines = File.ReadAllLines(_loadedPath);
                var totalCount = lines.Length;
                if (totalCount <= _parsedLineCount)
                    return (Array.Empty<string>(), totalCount, false);
                if (totalCount > MaxRows || totalCount < _parsedLineCount)
                    return (lines, totalCount, true); // 超上限/滚动重建 → 全量重载
                return (lines[_parsedLineCount..], totalCount, false);
            });

            if (needFullReload)
            {
                await ReloadAsync();
                return;
            }
            if (tail.Length == 0)
                return;

            _parsedLineCount = total;

            // 新块首行是续行（不匹配日志头）且上一条已解析 → 并入上一条（含显示列表同步）
            var start = 0;
            if (_allRows.Count > 0 && !LogRow.IsHeaderLine(tail[0]))
            {
                var last = _allRows[^1];
                var updated = last with { Message = last.Message + Environment.NewLine + tail[0] };
                _allRows[^1] = updated;

                // 显示列表末条若是同一行（值相等），按新内容重判定过滤并替换/移除
                var idx = Rows.IndexOf(last);
                if (idx >= 0)
                {
                    if (MatchesFilter(updated))
                        Rows[idx] = updated;
                    else
                        Rows.RemoveAt(idx);
                }
                start = 1;
            }

            var added = LogRow.ParseAll(tail[start..]);
            if (added.Count == 0)
                return;
            _allRows.AddRange(added);
            ApplyFilterAppend(added);
        }
        catch (IOException)
        {
            // 文件正被滚动清理时忽略，下轮再试
        }
        catch (OperationCanceledException)
        {
        }
    }
}
