using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Recipe;

/// <summary>配方列表协作对象访问宿主状态（由 <see cref="RecipeViewModel"/> 实现）。</summary>
internal interface IRecipeListHost
{
    RecipeConfig Editor { get; set; }
    bool IsNew { get; set; }
    bool IsBusy { get; set; }
    /// <summary>最近一次从磁盘加载配方失败：此时 Editor 为空壳，保存会覆盖磁盘真实文件，必须禁用。</summary>
    bool EditorLoadFailed { get; set; }
    bool HasUnsavedChanges { get; }
    string Message { get; set; }
    string OriginalName { get; set; }
    string RecipeHealthHint { get; set; }
    RecipeConfig? Baseline { get; set; }
    void ResetDirtyCache();
    void NotifyEditorBindings();
    void RefreshTestTriggerGate();
    void ClearTestAdvice();
    void NotifyTestCanExecute();
    void NotifyDeleteCanExecute();
    void RefreshRecipeHealth();
    void RaiseListFilterBindings();
    void RaiseDirtyBindings();
    RecipeLightingEditor Lighting { get; }
    RecipeTestSession Test { get; }
    IReadOnlyList<string> CameraIds { get; }
    IReadOnlyList<string> ModelFiles { get; }
    IReadOnlyList<string> StationIds { get; }
    string PrimaryModel { get; set; }
    string SecondaryModel { get; set; }
    bool ConfirmGrabOriginIfNeeded(string action);
    void CommitEdits();
}

/// <summary>配方列表：刷新、搜索过滤、新建/复制/保存/删除。</summary>
public sealed partial class RecipeListCatalog : ObservableObject
{
    private readonly IRecipeListHost _host;
    private readonly RecipeLoader _loader;
    private readonly IDialogService _dialogs;
    private readonly SqliteResultStore? _sqlite;
    private readonly ILogger _log;

    private bool _switching;
    private RecipeListItem? _lastConfirmed;
    private CancellationTokenSource? _refreshCts;
    private CancellationTokenSource? _loadCts;

    internal RecipeListCatalog(
        IRecipeListHost host,
        RecipeLoader loader,
        IDialogService dialogs,
        SqliteResultStore? sqlite,
        ILogger log)
    {
        _host = host;
        _loader = loader;
        _dialogs = dialogs;
        _sqlite = sqlite;
        _log = log;
    }

    public ObservableCollection<RecipeListItem> Recipes { get; } = [];

    [ObservableProperty]
    private RecipeListItem? _selected;

    [ObservableProperty]
    private string _searchText = "";

    public string RecipesFolderHint => _loader.Folder;

    public IEnumerable<RecipeListItem> VisibleRecipes =>
        string.IsNullOrWhiteSpace(SearchText)
            ? Recipes
            : Recipes.Where(r =>
                r.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                (r.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false));

    public bool IsSelectedVisibleInFilter =>
        Selected is not null && IsRecipeVisibleInFilter(Selected);

    public string SelectedFilterHint =>
        Selected is not null && !string.IsNullOrWhiteSpace(SearchText) && !IsSelectedVisibleInFilter
            ? $"当前编辑「{Selected.Name}」不在过滤结果中，列表未高亮；清空搜索或匹配到该项后可删除"
            : "";

    internal RecipeListItem? LastConfirmed => _lastConfirmed;

    internal void SetSwitching(bool value) => _switching = value;

    internal void SetLastConfirmed(RecipeListItem? item) => _lastConfirmed = item;

    private bool IsRecipeVisibleInFilter(RecipeListItem item)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
            return true;
        foreach (var visible in VisibleRecipes)
        {
            if (ReferenceEquals(visible, item))
                return true;
            if (string.Equals(visible.Name, item.Name, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private bool CanRunWhenIdle => !_host.IsBusy && !_host.EditorLoadFailed;

    /// <summary>CanExecute 依赖 _host 状态（IsBusy/EditorLoadFailed）的命令需由宿主显式刷新。</summary>
    internal void NotifyIdleCommands()
    {
        SaveCommand.NotifyCanExecuteChanged();
        NewCommand.NotifyCanExecuteChanged();
        CopyCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanRunWhenIdle))]
    public void Refresh() => ScheduleRefresh();

    public void ScheduleRefresh(string? preferName = null, bool reloadEditor = true, bool ignoreUnsaved = false) =>
        UiFireAndForget.Run(() => RefreshAsync(preferName, reloadEditor, ignoreUnsaved), _log);

    public void Refresh(string? preferName, bool reloadEditor, bool ignoreUnsaved = false) =>
        ScheduleRefresh(preferName, reloadEditor, ignoreUnsaved);

    public async Task RefreshAsync(string? preferName = null, bool reloadEditor = true, bool ignoreUnsaved = false)
    {
        _host.CommitEdits();
        if (!ignoreUnsaved && _host.HasUnsavedChanges && !ConfirmDiscard("刷新列表"))
            return;

        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        var cts = _refreshCts = new CancellationTokenSource();
        var token = cts.Token;
        var keepName = preferName ?? Selected?.Name ?? _host.Editor.Name;

        List<RecipeListItem> items;
        try
        {
            items = await Task.Run(() => BuildRecipeListItems(token), token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested)
            return;

        Recipes.Clear();
        foreach (var item in items)
            Recipes.Add(item);

        _host.RaiseListFilterBindings();

        _switching = true;
        Selected = string.IsNullOrWhiteSpace(keepName)
            ? Recipes.FirstOrDefault()
            : Recipes.FirstOrDefault(r => string.Equals(r.Name, keepName, StringComparison.OrdinalIgnoreCase));
        _switching = false;
        _lastConfirmed = Selected;
        if (reloadEditor)
        {
            if (Selected is not null)
                await LoadIntoEditorAsync(Selected.Name, token).ConfigureAwait(true);
            else if (Recipes.Count == 0)
                ResetEditorForEmptyList();
        }

        _host.Message = $"共 {Recipes.Count} 个配方";
        RaiseFilterBindings();
        _host.RefreshTestTriggerGate();
        _host.NotifyDeleteCanExecute();
    }

    private List<RecipeListItem> BuildRecipeListItems(CancellationToken token)
    {
        var items = new List<RecipeListItem>();
        foreach (var name in _loader.ListNames())
        {
            token.ThrowIfCancellationRequested();
            items.Add(RecipeListItemDescriber.Describe(_loader, name));
        }

        return items;
    }

    [RelayCommand(CanExecute = nameof(CanRunWhenIdle))]
    private void New()
    {
        _host.CommitEdits();
        if (_host.HasUnsavedChanges && !ConfirmDiscard("新建配方"))
            return;
        ClearListSelectionForDraft();
        _host.IsNew = true;
        _host.EditorLoadFailed = false;
        _host.OriginalName = "";
        _host.Editor = new RecipeConfig
        {
            Name = "",
            CameraId = _host.CameraIds.Count > 0 ? _host.CameraIds[0] : "",
            Models = [""],
        };
        _host.Baseline = _host.Editor.Clone();
        _host.ResetDirtyCache();
        _host.NotifyEditorBindings();
        _host.RaiseDirtyBindings();
        _host.RefreshTestTriggerGate();
        _host.ClearTestAdvice();
        _host.NotifyTestCanExecute();
        _host.NotifyDeleteCanExecute();
        _host.RecipeHealthHint = "";
        _host.Message = "新建配方：填写名称与参数后保存";
    }

    [RelayCommand(CanExecute = nameof(CanRunWhenIdle))]
    private void Copy()
    {
        _host.CommitEdits();
        if (_host.HasUnsavedChanges && !ConfirmCopyCurrentEditor())
            return;
        var source = _host.Editor;
        var copy = source.Clone();
        copy.Name = source.Name.Length > 0 ? source.Name + "_copy" : "";
        copy.SerialNumber = 0;
        copy.OutputOffset = new();
        ClearListSelectionForDraft();
        _host.IsNew = true;
        _host.EditorLoadFailed = false;
        _host.OriginalName = "";
        _host.Editor = copy;
        _host.Baseline = copy.Clone();
        _host.ResetDirtyCache();
        _host.NotifyEditorBindings();
        _host.RaiseDirtyBindings();
        _host.RefreshTestTriggerGate();
        _host.ClearTestAdvice();
        _host.NotifyTestCanExecute();
        _host.NotifyDeleteCanExecute();
        _host.RecipeHealthHint = "";
        _host.Message = source.Name.Length > 0
            ? $"已复制 {source.Name}：已清序列号与输出补偿，改名后保存即新配方"
            : "已复制为新配方：已清序列号与输出补偿，填写名称后保存";
    }

    [RelayCommand(CanExecute = nameof(CanRunWhenIdle))]
    private void Save()
    {
        try
        {
            if (_host.EditorLoadFailed)
            {
                ShowSaveBlocked("配方读取失败，保存已禁用（防止覆盖磁盘上的真实配方）。请先刷新列表或重新选择配方。");
                return;
            }

            _host.CommitEdits();

            var saveError = RecipeEditorValidator.TryValidateForSave(_host.Editor, _loader);
            if (saveError is not null)
            {
                ShowSaveBlocked(saveError);
                return;
            }

            if (string.IsNullOrWhiteSpace(_host.Editor.Name))
            {
                ShowSaveBlocked("请先填写配方名称");
                return;
            }
            if (!RecipeLoader.IsValidRecipeName(_host.Editor.Name))
            {
                ShowSaveBlocked("名称只允许字母、数字、下划线、中划线（长度 ≤ 64）");
                return;
            }

            var previousName = ResolvePreviousDiskName();

            var isRename = _host.IsNew ||
                !string.Equals(_host.Editor.Name, previousName, StringComparison.OrdinalIgnoreCase);
            if (isRename && _loader.FileExists(_host.Editor.Name) &&
                !_dialogs.ConfirmYesNo($"配方 {_host.Editor.Name} 已存在，保存将覆盖现有内容。继续？",
                    "覆盖确认"))
                return;

            var modelError = RecipeModelSlots.TryCommitUiModels(_host.Editor, _host.PrimaryModel, _host.SecondaryModel);
            if (modelError is not null)
            {
                ShowSaveBlocked(modelError);
                return;
            }

            if (_host.Editor.Lighting is not null && string.IsNullOrWhiteSpace(_host.Editor.LightControllerId))
            {
                ShowSaveBlocked("已启用光源但未选择光源控制器（appsettings LightControllers 未配置时先添加 None 类型）");
                return;
            }

            if (!_host.ConfirmGrabOriginIfNeeded("保存"))
                return;

            _loader.Save(_host.Editor, _host.IsNew ? null : previousName);

            var savedMessage = isRename && !string.IsNullOrEmpty(previousName) &&
                !string.Equals(_host.Editor.Name, previousName, StringComparison.OrdinalIgnoreCase)
                ? $"已保存 {_host.Editor.Name}（原 {previousName} 已重命名）"
                : $"已保存 {_host.Editor.Name}";

            _host.IsNew = false;
            _host.OriginalName = _host.Editor.Name;
            _host.Baseline = _host.Editor.Clone();
            _host.ResetDirtyCache();
            _host.RaiseDirtyBindings();
            Refresh(_host.Editor.Name, reloadEditor: false, ignoreUnsaved: true);
            _host.NotifyDeleteCanExecute();
            _host.RefreshRecipeHealth();
            _host.Message = savedMessage;
        }
        catch (Exception ex)
        {
            ShowSaveBlocked(ex.Message);
        }
    }

    private bool CanDelete =>
        Selected is not null && !_host.IsNew && !_host.IsBusy && IsSelectedVisibleInFilter;

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private void Delete()
    {
        if (Selected is null || _host.IsNew)
            return;

        var prompt = _host.HasUnsavedChanges && string.Equals(Selected.Name, _host.OriginalName, StringComparison.OrdinalIgnoreCase)
            ? $"配方 {Selected.Name} 有未保存的修改，删除将一并丢弃。确定删除？（不可恢复）"
            : $"确定删除配方 {Selected.Name}？（不可恢复）";

        if (!_dialogs.ConfirmYesNo(prompt, "删除配方"))
            return;

        var deletedName = Selected.Name;
        try
        {
            _loader.Delete(deletedName);
            _host.Message = $"已删除 {deletedName}";
            _lastConfirmed = null;
            Refresh(preferName: string.Empty, reloadEditor: true, ignoreUnsaved: true);
            _host.NotifyDeleteCanExecute();
        }
        catch (Exception ex)
        {
            _host.Message = $"删除失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenFolder() => Explorer.OpenFolder(_loader.Folder);

    internal void HandleSelectedChanged(RecipeListItem? value)
    {
        if (_switching || value is null)
            return;

        if (_host.IsBusy)
        {
            _switching = true;
            Selected = _lastConfirmed;
            _switching = false;
            return;
        }

        _host.CommitEdits();

        if (_lastConfirmed is not null && _host.HasUnsavedChanges &&
            !_dialogs.ConfirmDiscard($"配方 {_host.OriginalName} 有未保存的修改，切换将丢弃这些修改。继续？"))
        {
            _switching = true;
            Selected = _lastConfirmed;
            _switching = false;
            return;
        }

        ScheduleLoadIntoEditor(value.Name);
        _lastConfirmed = value;
        _host.ClearTestAdvice();
        RaiseFilterBindings();
        _host.NotifyDeleteCanExecute();
    }

    partial void OnSearchTextChanged(string value) => RaiseFilterBindings();

    internal void RaiseFilterBindings()
    {
        OnPropertyChanged(nameof(VisibleRecipes));
        OnPropertyChanged(nameof(IsSelectedVisibleInFilter));
        OnPropertyChanged(nameof(SelectedFilterHint));
        _host.NotifyDeleteCanExecute();
    }

    private void ScheduleLoadIntoEditor(string name) =>
        UiFireAndForget.Run(() => LoadIntoEditorAsync(name, CancellationToken.None), _log);

    private async Task LoadIntoEditorAsync(string name, CancellationToken token)
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        var loadCts = _loadCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        var loadToken = loadCts.Token;

        _host.IsBusy = true;
        NotifyIdleCommands();
        try
        {
            LoadResult result;
            try
            {
                result = await Task.Run(() => LoadRecipeFromDisk(name), loadToken).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (loadToken.IsCancellationRequested)
                return;

            ApplyLoadResult(name, result);
        }
        finally
        {
            if (ReferenceEquals(_loadCts, loadCts))
            {
                _host.IsBusy = false;
                NotifyIdleCommands();
            }
        }
    }

    private readonly record struct LoadResult(bool Failed, RecipeConfig? Loaded, string? ErrorMessage);

    private LoadResult LoadRecipeFromDisk(string name)
    {
        try
        {
            return new LoadResult(false, _loader.Get(name), null);
        }
        catch (Exception ex)
        {
            return new LoadResult(true, null, ex.Message);
        }
    }

    private void ApplyLoadResult(string name, LoadResult result)
    {
        _host.IsNew = false;
        _host.OriginalName = name;
        if (!result.Failed && result.Loaded is not null)
        {
            var loaded = result.Loaded;
            _host.EditorLoadFailed = false;
            _host.Editor = loaded.Clone();
            _host.Baseline = _host.Editor.Clone();
            _host.ResetDirtyCache();
            _host.NotifyEditorBindings();
            _host.Message = loaded.Enabled ? "" : $"配方 {name} 已停用（Enabled=false），触发将返回 1015";
            return;
        }

        _host.EditorLoadFailed = true;
        _host.Editor = new RecipeConfig { Name = name };
        _host.Baseline = _host.Editor.Clone();
        _host.ResetDirtyCache();
        _host.NotifyEditorBindings();
        _host.Message = $"读取失败：{result.ErrorMessage}（保存已禁用，防止覆盖磁盘配方）";
    }

    private void LoadIntoEditor(string name) =>
        ScheduleLoadIntoEditor(name);

    private void ResetEditorForEmptyList()
    {
        ClearListSelectionForDraft();
        _host.IsNew = true;
        _host.EditorLoadFailed = false;
        _host.OriginalName = "";
        _host.Editor = new RecipeConfig
        {
            Name = "",
            CameraId = _host.CameraIds.Count > 0 ? _host.CameraIds[0] : "",
            Models = [""],
        };
        _host.Baseline = _host.Editor.Clone();
        _host.ResetDirtyCache();
        _host.NotifyEditorBindings();
        _host.RaiseDirtyBindings();
        _host.RefreshTestTriggerGate();
        _host.ClearTestAdvice();
        _host.NotifyTestCanExecute();
        _host.RecipeHealthHint = "";
    }

    private bool ConfirmDiscard(string action) =>
        _dialogs.ConfirmDiscard($"配方 {_host.OriginalName} 有未保存的修改，{action}将丢弃这些修改。继续？");

    private bool ConfirmCopyCurrentEditor() =>
        _dialogs.ConfirmYesNo(
            "将把当前编辑器（含未保存修改）复制为新配方，原配方磁盘文件不变。新配方会清掉序列号和输出补偿。继续？",
            "复制为新配方",
            questionIcon: true);

    private void ClearListSelectionForDraft()
    {
        _switching = true;
        Selected = null;
        _lastConfirmed = null;
        _switching = false;
    }

    private void ShowSaveBlocked(string reason)
    {
        _host.Message = $"保存失败：{reason}";
        _dialogs.ShowWarning(reason, "无法保存");
    }

    private string ResolvePreviousDiskName()
    {
        if (_host.IsNew)
            return "";

        if (!string.IsNullOrEmpty(_host.OriginalName) && _loader.FileExists(_host.OriginalName))
            return _host.OriginalName;

        if (Selected is not null && _loader.FileExists(Selected.Name))
            return Selected.Name;

        return _host.OriginalName;
    }
}
