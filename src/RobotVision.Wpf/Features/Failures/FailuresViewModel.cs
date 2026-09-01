using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RobotVision.Hosting;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Failures;

public sealed record FailureItem(
    string PngPath, string JsonPath, string DisplayName, string Recipe, string ErrorCode,
    string MetaText, BitmapImage Thumb);

/// <summary>
/// 失败现场画廊：浏览留存 PNG 与元数据，支持删除/清空（文件 OnLoad 解码，不锁定磁盘）。
/// 支持按配方/错误码筛选（选项从当前留存的元数据中动态收集）。
/// </summary>
public partial class FailuresViewModel : ObservableObject, IDisposable
{
    private const string AllFilter = "全部";

    private readonly FailureImageStore _store;
    private readonly IDialogService _dialogs;
    private readonly ILogger<FailuresViewModel> _log;
    private readonly List<FailureItem> _allItems = [];

    public ObservableCollection<FailureItem> Items { get; } = [];

    public ObservableCollection<string> RecipeFilters { get; } = [];

    public ObservableCollection<string> ErrorCodeFilters { get; } = [];

    [ObservableProperty]
    private FailureItem? _selected;

    [ObservableProperty]
    private BitmapImage? _previewImage;

    [ObservableProperty]
    private string _message = "";

    [ObservableProperty]
    private string _recipeFilter = AllFilter;

    [ObservableProperty]
    private string _errorCodeFilter = AllFilter;

    [ObservableProperty]
    private string _filterSummary = "";

    /// <summary>失败列表浮动面板是否可见（与相机/配方/模型页同构，图像主导布局）。</summary>
    [ObservableProperty]
    private bool _isListPanelVisible = true;

    [RelayCommand]
    private void ToggleListPanel() => IsListPanelVisible = !IsListPanelVisible;

    /// <summary>清空按钮文案：筛选状态下仅删除筛选结果，文案与行为一致。</summary>
    public string DeleteAllButtonText =>
        RecipeFilter != AllFilter || ErrorCodeFilter != AllFilter ? "清空筛选结果" : "清空全部";

    public FailuresViewModel(FailureImageStore store, IDialogService dialogs, ILogger<FailuresViewModel> log)
    {
        _store = store;
        _dialogs = dialogs;
        _log = log;
        ScheduleRefresh();
    }

    public void ScheduleRefresh() => UiFireAndForget.Run(RefreshAsync, _log);

    [RelayCommand]
    public async Task RefreshAsync()
    {
        // 取消上一轮未完成的加载（快速连续刷新/切筛选时避免旧任务污染）
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        var cts = _loadCts = new CancellationTokenSource();
        var token = cts.Token;
        var keepPath = Selected?.PngPath;

        PreviewImage = null;

        if (!Directory.Exists(_store.Folder))
        {
            _allItems.Clear();
            Items.Clear();
            RecipeFilters.Clear();
            ErrorCodeFilters.Clear();
            Selected = null;
            Message = "暂无失败留存（目录未创建）";
            FilterSummary = "";
            return;
        }

        // 目录枚举是同步 IO，放在 UI 线程会随失败图数量线性卡顿，挪到后台一次完成。
        var files = await Task.Run(
            () => Directory.GetFiles(_store.Folder, "*.png")
                .OrderByDescending(p => p, StringComparer.OrdinalIgnoreCase)
                .Select(p => (Png: p, Json: Path.ChangeExtension(p, ".json")))
                .ToList(),
            token);

        Message = $"加载中：{files.Count} 张…";
        var loaded = new List<FailureItem>(files.Count);
        foreach (var (png, json) in files)
        {
            if (token.IsCancellationRequested)
                return;

            // 元数据读取（同步文件 IO）与缩略图解码（CPU 密集）合并为一次后台调用：
            // 原先 ReadMeta 留在 UI 线程，每张图一次同步读盘，大量失败图时明显卡顿。
            var (recipe, code, meta, thumb) = await Task.Run(
                () =>
                {
                    var (r, c, t) = ReadMeta(json);
                    return (r, c, t, LoadBitmap(png, 220) ?? PlaceholderThumb);
                },
                token);
            if (token.IsCancellationRequested)
                return;
            loaded.Add(new FailureItem(
                png, json, Path.GetFileName(png), recipe, code, meta, thumb));
        }

        _allItems.Clear();
        _allItems.AddRange(loaded);

        // 重建筛选选项（保留用户当前选择，若该选项仍存在）
        RecipeFilters.Clear();
        RecipeFilters.Add(AllFilter);
        foreach (var recipe in _allItems.Select(i => i.Recipe)
                     .Where(r => r.Length > 0).Distinct().Order())
            RecipeFilters.Add(recipe);
        RecipeFilter = RecipeFilters.Contains(RecipeFilter) ? RecipeFilter : AllFilter;

        ErrorCodeFilters.Clear();
        ErrorCodeFilters.Add(AllFilter);
        foreach (var code in _allItems.Select(i => i.ErrorCode)
                     .Where(c => c.Length > 0).Distinct().Order())
            ErrorCodeFilters.Add(code);
        ErrorCodeFilter = ErrorCodeFilters.Contains(ErrorCodeFilter) ? ErrorCodeFilter : AllFilter;

        ApplyFilter(keepPath);

        Message = _allItems.Count > 0
            ? $"共 {_allItems.Count} 条失败现场（时间倒序）"
            : "暂无失败留存";
    }

    private CancellationTokenSource? _loadCts;
    private CancellationTokenSource? _previewCts;

    partial void OnRecipeFilterChanged(string value) => ApplyFilter();

    partial void OnErrorCodeFilterChanged(string value) => ApplyFilter();

    private void ApplyFilter() => ApplyFilter(Selected?.PngPath);

    private void ApplyFilter(string? preferPath)
    {
        var keepPath = preferPath;
        var byRecipe = _allItems
            .Where(i => RecipeFilter == AllFilter || i.Recipe == RecipeFilter);
        var byCode = ErrorCodeFilter == AllFilter
            ? byRecipe
            : byRecipe.Where(i => i.ErrorCode == ErrorCodeFilter);
        var filtered = byCode.ToList();

        Items.Clear();
        foreach (var item in filtered)
            Items.Add(item);
        Selected = string.IsNullOrWhiteSpace(keepPath)
            ? Items.FirstOrDefault()
            : Items.FirstOrDefault(i => string.Equals(i.PngPath, keepPath, StringComparison.OrdinalIgnoreCase));

        var hasFilter = RecipeFilter != AllFilter || ErrorCodeFilter != AllFilter;
        FilterSummary = hasFilter
            ? $"筛选: 配方 {RecipeFilter} · 错误码 {ErrorCodeFilter} → {filtered.Count}/{_allItems.Count} 条"
            : "";
        OnPropertyChanged(nameof(DeleteAllButtonText));
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        if (Selected is null)
            return;

        if (!_dialogs.ConfirmYesNo($"删除 {Selected.DisplayName}？（不可恢复）", "删除失败现场"))
            return;

        TryDelete(Selected.PngPath);
        TryDelete(Selected.JsonPath);
        ScheduleRefresh();
    }

    [RelayCommand]
    private void DeleteAll()
    {
        if (Items.Count == 0)
            return;

        // 筛选状态下只删除筛选结果，文案必须与之相符，避免"以为清空全部、实际只删了筛选部分"
        var hasFilter = RecipeFilter != AllFilter || ErrorCodeFilter != AllFilter;
        var prompt = hasFilter
            ? $"将清空当前筛选结果 {Items.Count} 条（其余 {_allItems.Count - Items.Count} 条保留）？（不可恢复）"
            : $"清空全部 {_allItems.Count} 条失败现场？（不可恢复）";

        if (!_dialogs.ConfirmYesNo(prompt, "清空失败现场"))
            return;

        foreach (var item in Items)
        {
            TryDelete(item.PngPath);
            TryDelete(item.JsonPath);
        }
        ScheduleRefresh();
    }

    [RelayCommand]
    private void OpenFolder() => Explorer.OpenFolder(_store.Folder);

    partial void OnSelectedChanged(FailureItem? value)
    {
        if (value is null)
        {
            _previewCts?.Cancel();
            PreviewImage = null;
            return;
        }
        // 大图预览解码放后台线程；切换过快时取消上一张
        var path = value.PngPath;
        _previewCts?.Cancel();
        var cts = _previewCts = new CancellationTokenSource();
        UiFireAndForget.Run(() => LoadPreviewAsync(path, cts), _log);
    }

    private async Task LoadPreviewAsync(string path, CancellationTokenSource cts)
    {
        try
        {
            var img = await Task.Run(() => LoadBitmap(path, 1000), cts.Token);
            if (cts.IsCancellationRequested ||
                !string.Equals(Selected?.PngPath, path, StringComparison.OrdinalIgnoreCase))
                return;
            PreviewImage = img;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            WpfUiLog.FailurePreviewFailed(_log, ex, path);
            if (!cts.IsCancellationRequested)
                PreviewImage = null;
        }
    }

    private static BitmapImage? LoadBitmap(string path, int decodeWidth)
    {
        try
        {
            // 必须 BeginInit/EndInit：对象初始化器方式不触发解码，渲染为空白
            var img = new BitmapImage();
            img.BeginInit();
            img.DecodePixelWidth = decodeWidth;
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            img.UriSource = new Uri(path);
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>缩略图加载失败时的占位图（1×1 透明像素）。</summary>
    private static readonly BitmapImage PlaceholderThumb = CreatePlaceholder();

    private static BitmapImage CreatePlaceholder()
    {
        var img = new BitmapImage();
        using var ms = new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D,
            0x49, 0x48, 0x44, 0x52, 0, 0, 0, 1, 0, 0, 0, 1, 8, 6, 0, 0, 0, 0x1F, 0x15, 0xC4, 0x89,
            0, 0, 0, 0x0A, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0, 1, 0, 0, 5, 0, 1, 0x0D, 0x0A,
            0x2D, 0xB4, 0, 0, 0, 0, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82]);
        img.BeginInit();
        img.StreamSource = ms;
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.EndInit();
        img.Freeze();
        return img;
    }

    /// <summary>解析留存元数据：返回 (配方, 错误码, 摘要文本)。缺文件/解析失败返回占位值。</summary>
    private static (string Recipe, string ErrorCode, string MetaText) ReadMeta(string jsonPath)
    {
        if (!File.Exists(jsonPath))
            return ("", "", "（元数据缺失）");
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
            var root = doc.RootElement;
            var recipe = GetString(root, "Recipe");
            var code = GetString(root, "ErrorCode");
            var meta = string.Join("  ",
                $"配方 {recipe}",
                $"错误码 {code}",
                $"{GetString(root, "Message")}",
                $"耗时 {GetString(root, "ElapsedMs")}ms");
            return (recipe, code, meta);
        }
        catch
        {
            return ("", "", "（元数据解析失败）");
        }
    }

    private static string GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var v) ? v.ToString() : "-";

    /// <summary>
    /// 尽力删除；失败不阻断批量清理，但必须留痕。
    /// 否则用户点删除后静默失败，会误以为留存已清掉。
    /// </summary>
    private void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (IOException ex) { WpfUiLog.FailureDeleteFailed(_log, ex, path); }
        catch (UnauthorizedAccessException ex) { WpfUiLog.FailureDeleteFailed(_log, ex, path); }
    }

    public void Dispose()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;
        _previewCts?.Cancel();
        _previewCts?.Dispose();
        _previewCts = null;
    }
}
