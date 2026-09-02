using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Hosting.Cameras;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Cameras;
public partial class CamerasViewModel
{
    [RelayCommand]
    public void Refresh() => ScheduleRefresh();

    public void ScheduleRefresh(string? preferId = null, bool resetPreview = true, bool loadLiveParams = true) =>
        UiFireAndForget.Run(() => RefreshAsync(preferId, resetPreview, loadLiveParams), _log);

    /// <param name="preferId">刷新后优先选中的 Id；空则尽量保持当前选中。</param>
    /// <param name="resetPreview">
    /// true：同步编辑区时清掉预览图（F5 / 进页 / 删除后换机）。
    /// false：保存后刷新列表时保留当前预览——图仍是这台相机的，清掉只增加再取一次的成本。
    /// </param>
    /// <param name="loadLiveParams">
    /// true：后台读曝光/增益填滑块（选中/F5）。
    /// false：保存后不要连相机读参——读失败会把「已保存」盖成「读取相机参数失败」。
    /// </param>
    public async Task RefreshAsync(string? preferId = null, bool resetPreview = true, bool loadLiveParams = true)
    {
        if (HasUnsavedChanges && !ConfirmDiscard("刷新列表"))
            return;

        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        var cts = _refreshCts = new CancellationTokenSource();
        var token = cts.Token;
        var keepId = ListSelection.KeepKey(preferId, Selected?.Id ?? EditId.Trim());

        List<CameraListItem> items;
        try
        {
            items = await Task.Run(() => BuildCameraListItems(token), token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested)
            return;

        _switching = true;
        try
        {
            Items.Clear();
            foreach (var item in items)
                Items.Add(item);

            Selected = ListSelection.Restore(Items, keepId, i => i.Id);
        }
        finally
        {
            _switching = false;
        }

        if (Selected is not null)
            ApplySelectedItem(Selected, resetPreview, loadLiveParams);
        Message = $"共 {Items.Count} 台相机";
    }

    private List<CameraListItem> BuildCameraListItems(CancellationToken ct)
    {
        var items = new List<CameraListItem>(_cfg.Cameras.Count);
        foreach (var camera in _cfg.Cameras)
        {
            ct.ThrowIfCancellationRequested();
            var registered = _cameras.IsRegistered(camera.Id);
            var title = CameraLabels.ListTitle(camera);
            var subtitle = string.IsNullOrWhiteSpace(camera.Name) ? null : camera.Id;
            items.Add(new CameraListItem(
                camera.Id, title, subtitle, camera.Type, Summarize(camera),
                registered ? "已注册" : "未注册", registered,
                registered ? null : UnregisterReason(camera)));
        }

        return items;
    }

    /// <summary>保存/删除后刷新列表（保留预览与不调相机读参）。</summary>
    private void RefreshAfterMutation(string? preferId = null) =>
        ScheduleRefresh(preferId, resetPreview: false, loadLiveParams: false);

    /// <summary>未保存修改确认框；用户拒绝时返回 false 并中止当前操作。</summary>
    private bool ConfirmDiscard(string action) =>
        _dialogs.ConfirmDiscard($"相机 {_baseline?.Id} 有未保存的修改，{action}将丢弃这些修改。继续？");

    private static string Summarize(CameraConfig c) => c.Type switch
    {
        "Basler" or "GigEVision" => string.IsNullOrWhiteSpace(c.DeviceId)
            ? "未指定设备（仅单台时可开）"
            : $"SN/IP {c.DeviceId}",
        "Virtual" => $"{c.Width}×{c.Height} · {c.Pattern}",
        _ => c.Folder,
    };

    /// <summary>未注册原因（列表展示，让"未注册"在列表层面即可区分原因）。</summary>
    private string? UnregisterReason(CameraConfig c)
    {
        if (string.Equals(c.Type, "File", StringComparison.OrdinalIgnoreCase))
        {
            var folder = c.ResolveCameraFolder(_cfg);
            if (!Directory.Exists(folder))
                return "回放目录不存在";
            return Directory.EnumerateFiles(folder)
                .Any(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                ? "初始化失败"
                : "目录中没有图片";
        }
        if (string.Equals(c.Type, "Basler", StringComparison.OrdinalIgnoreCase))
            return "pylon 未安装或相机未连接";
        if (string.Equals(c.Type, "GigEVision", StringComparison.OrdinalIgnoreCase))
            return "网口未发现 GigE Vision 相机";
        return "初始化失败";
    }

    private static readonly string[] ImageExtensions = [".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff"];

    [RelayCommand]
    private void New()
    {
        if (HasUnsavedChanges && !ConfirmDiscard("新建相机"))
            return;
        StopPreview();
        IsNew = true;
        _baseline = null;
        EditId = "";
        EditName = "";
        EditType = "File";
        EditFolder = "";
        EditDeviceId = "";
        EditExposureUs = "";
        EditGain = "";
        EditGrabTimeoutMs = AppConfig.DefaultGrabTimeoutMs;
        EditWidth = 1280;
        EditHeight = 960;
        EditPattern = "Chessboard";
        EditCellPx = 40;
        EditIntervalMs = 0;
        EditNoiseSigma = "0";
        PreviewImage = null;
        PreviewCaption = "";
        PreviewToolTip = "";
        Message = "新建相机：选择类型并填写参数后保存";
    }

    [RelayCommand]
    private void Copy()
    {
        if (HasUnsavedChanges && !ConfirmDiscard("复制相机"))
            return;
        StopPreview();
        var id = EditId.Trim();
        IsNew = true;
        _baseline = null;
        EditId = id.Length > 0 ? id + "_copy" : "";
        PreviewImage = null;
        PreviewCaption = "";
        PreviewToolTip = "";
        Message = "已复制：改 Id 后保存即新相机";
    }

    [RelayCommand]
    private void Save()
    {
        this.Commit();
        var id = EditId.Trim();
        if (id.Length == 0)
        {
            Message = "保存失败：Id 不能为空";
            return;
        }
        if (IsNew && _cfg.Cameras.Any(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase)))
        {
            Message = $"保存失败：相机 {id} 已存在";
            return;
        }

        CameraConfig entry;
        try
        {
            entry = BuildConfig(id);
        }
        catch (FormatException ex)
        {
            Message = $"保存失败: {ex.Message}";
            return;
        }

        // 超时预算校验：硬件相机取图超时须小于服务总超时
        if (IsHardwareType(entry.Type))
        {
            if (entry.GrabTimeoutMs <= 0)
            {
                Message = "保存失败: 取图超时须为正数（ms）";
                return;
            }
            if (entry.GrabTimeoutMs >= _cfg.TimeoutMs)
            {
                Message = $"保存失败: 取图超时 {entry.GrabTimeoutMs}ms 不小于服务总超时 {_cfg.TimeoutMs}ms（将表现为 1008 而非 1003），请调小取图超时或在服务设置调大总超时";
                return;
            }
        }

        try
        {
            var list = _cfg.Cameras.ToList();
            var index = list.FindIndex(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
                list[index] = entry;
            else
                list.Add(entry);

            _store.Save(list);

            // 运行时注册失败不影响配置保存（如 Basler 未接相机，之后接上重新保存即可）
            var error = TryRegister(entry);
            IsNew = false;
            _baseline = entry; // 保存后的配置即新基线（Refresh 的脏检查不会误弹）
            RefreshAfterMutation(id);
            Message = error is null ? $"已保存 {id}（运行时已注册）" : $"已保存 {id}（运行时注册失败: {error}）";
            NotifyDirtyState();
        }
        catch (Exception ex)
        {
            Message = $"保存失败: {ex.Message}";
        }
    }

    /// <summary>按当前编辑字段构造配置；数值格式非法抛 FormatException。
    /// 未知类型（外部工厂注册、无内置编辑面板）：保留磁盘已有字段（克隆），仅应用 Id/Type。</summary>
    private CameraConfig BuildConfig(string id)
    {
        CameraConfig entry;
        if (!_registry.IsKnown(EditType))
        {
            var existing = _cfg.Cameras.FirstOrDefault(
                c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                var clone = JsonSerializer.Deserialize<CameraConfig>(
                    JsonSerializer.Serialize(existing))!;
                clone.Id = id;
                clone.Type = EditType;
                entry = clone;
            }
            else
                entry = new CameraConfig { Id = id, Type = EditType };
        }
        else
        {
            entry = new CameraConfig { Id = id, Type = EditType };
            switch (EditType)
            {
                case "Basler":
                case "GigEVision":
                    entry.DeviceId = EditDeviceId.Trim();
                    entry.ExposureTimeUs = ParseOptional(EditExposureUs, "曝光时间");
                    entry.Gain = ParseOptional(EditGain, "增益");
                    entry.GrabTimeoutMs = EditGrabTimeoutMs;
                    break;
                case "Virtual":
                    entry.Width = EditWidth;
                    entry.Height = EditHeight;
                    entry.Pattern = EditPattern;
                    entry.ChessCellPx = EditCellPx;
                    entry.IntervalMs = EditIntervalMs;
                    entry.NoiseSigma = ParseDouble(EditNoiseSigma, "噪声");
                    break;
                default:
                    entry.Folder = EditFolder.Trim();
                    entry.IntervalMs = EditIntervalMs;
                    break;
            }
        }

        entry.Name = EditName.Trim();
        return entry;
    }

    private static double? ParseOptional(string text, string label) =>
        string.IsNullOrWhiteSpace(text) ? null : ParseDouble(text, label);

    private static double ParseDouble(string text, string label)
    {
        if (!double.TryParse(text.Trim(), out var value))
            throw new FormatException($"{label} 不是有效数字: {text}");
        return value;
    }

    [RelayCommand]
    private void Delete()
    {
        if (IsNew || Selected is null)
        {
            Message = "请先选择要删除的相机";
            return;
        }
        var id = Selected.Id;

        var referenced = RecipeReferenceCheck.FindReferencing(_recipes, r => r.CameraId, id);
        var warn = referenced.Count > 0
            ? $"\n有 {referenced.Count} 个配方引用该相机（{string.Join("、", referenced)}），删除后触发将返回相机未注册错误。"
            : "";

        var prompt = HasUnsavedChanges
            ? $"相机 {_baseline?.Id} 有未保存的修改，删除将一并丢弃。确定删除相机 {id}？{warn}"
            : $"确定删除相机 {id}？{warn}";

        if (!_dialogs.ConfirmYesNo(prompt, "删除相机"))
            return;

        try
        {
            var list = _cfg.Cameras
                .Where(c => !string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase))
                .ToList();
            _store.Save(list);
            _cameras.Unregister(id);
            ScheduleRefresh();
            Message = $"已删除 {id}";
        }
        catch (Exception ex)
        {
            Message = $"删除失败: {ex.Message}";
        }
    }
    [RelayCommand]
    private async Task EnumerateBaslerAsync()
    {
        BaslerDevices.Clear();
        HasBaslerDevices = false;
        try
        {
            Message = "枚举设备中…";
            var type = EditType;
            var devices = await Task.Run(() => _registry.EnumerateDevices(type));
            foreach (var d in devices)
                BaslerDevices.Add(d);
            HasBaslerDevices = devices.Count > 0;
            SelectedBaslerDevice = BaslerDevices.FirstOrDefault();
            if (!string.IsNullOrEmpty(SelectedBaslerDevice))
                EditDeviceId = SelectedBaslerDevice.Split('|')[0].Trim();
            Message = devices.Count > 0
                ? $"发现 {devices.Count} 台设备，选择后自动填入序列号/IP"
                : string.Equals(type, "GigEVision", StringComparison.OrdinalIgnoreCase)
                    ? "未发现 GigE Vision 相机（检查网线、IP 网段与 UDP 防火墙）"
                    : "未发现 Basler 相机。若 pylon Viewer 能看到，请先完全退出 Viewer 再枚举，并填写序列号";
        }
        catch (Exception ex)
        {
            Message = $"枚举设备失败: {ex.Message}";
        }
    }

    partial void OnSelectedBaslerDeviceChanged(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return;
        // 条目格式 "SN | 名称 | 型号"
        EditDeviceId = value.Split('|')[0].Trim();
    }

    [RelayCommand]
    private void BrowseFolder()
    {
        var resolved = _cfg.ResolveDataPath(EditFolder.Trim());
        var initial = Directory.Exists(resolved) ? resolved : null;
        var picked = _dialogs.PickFolder("选择回放目录", initial);
        if (picked is not null)
            EditFolder = picked;
    }

    [RelayCommand]
    private void OpenFolder()
    {
        if (!IsFile)
        {
            Message = "仅 File 类型相机有回放目录";
            return;
        }
        var resolved = _cfg.ResolveDataPath(EditFolder.Trim());
        if (Directory.Exists(resolved))
            Explorer.OpenFolder(resolved);
        else
            Message = $"目录不存在: {resolved}";
    }
    /// <summary>按配置创建相机（临时实例，先试后存）：经工厂注册表，支持外部注册的类型。</summary>
    private ICamera CreateCamera(CameraConfig camera) =>
        _registry.Create(camera)
        ?? throw new InvalidOperationException($"相机类型 {camera.Type} 未注册工厂（CameraTypeRegistry.Register）");

    private string? TryRegister(CameraConfig camera)
    {
        try
        {
            _cameras.Register(CreateCamera(camera));
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    partial void OnSelectedChanged(CameraListItem? value)
    {
        if (_switching || value is null)
            return;

        // 未保存修改：确认后才切换（拒绝时恢复原选中）。预览停在确认之后，
        // 避免点了另一台又取消时把当前实时预览一并停掉。
        if (HasUnsavedChanges && !ConfirmDiscard("切换相机"))
        {
            _switching = true;
            Selected = Items.FirstOrDefault(i => string.Equals(i.Id, _baseline?.Id, StringComparison.OrdinalIgnoreCase));
            _switching = false;
            return;
        }

        ApplySelectedItem(value, resetPreview: true, loadLiveParams: true);
    }

    /// <summary>选中项 → 编辑区 + 运行时调光面板（Refresh 与 OnSelectedChanged 共用）。</summary>
    private void ApplySelectedItem(CameraListItem value, bool resetPreview, bool loadLiveParams)
    {
        IsNew = false;
        if (resetPreview)
        {
            StopPreview();
            ClearPreview();
        }

        var config = _cfg.Cameras.FirstOrDefault(
            c => string.Equals(c.Id, value.Id, StringComparison.OrdinalIgnoreCase));
        if (config is null)
            return;

        _applyingEditor = true;
        try
        {
            _baseline = config;
            EditId = config.Id;
            EditName = config.Name;
            EditType = config.Type;
            EditFolder = config.Folder;
            EditDeviceId = config.DeviceId;
            EditExposureUs = config.ExposureTimeUs?.ToString("0", CultureInfo.InvariantCulture) ?? "";
            EditGain = config.Gain?.ToString("0.##", CultureInfo.InvariantCulture) ?? "";
            if (IsHardwareType(config.Type))
                EditGrabTimeoutMs = config.GrabTimeoutMs > 0
                    ? config.GrabTimeoutMs
                    : AppConfig.DefaultGrabTimeoutMs;
            EditWidth = config.Width;
            EditHeight = config.Height;
            EditPattern = PatternOptions.Contains(config.Pattern, StringComparer.OrdinalIgnoreCase)
                ? config.Pattern
                : "Chessboard";
            EditCellPx = config.ChessCellPx;
            EditIntervalMs = config.IntervalMs;
            EditNoiseSigma = config.NoiseSigma.ToString("0.##", CultureInfo.InvariantCulture);

            if (config.ExposureTimeUs is > 0)
                ExposureUs = config.ExposureTimeUs.Value;
            if (config.Gain is >= 0)
                Gain = config.Gain.Value;
        }
        finally
        {
            _applyingEditor = false;
            NotifyDirtyState();
        }

        var live = LiveExposureControl;
        HasLiveExposureControl = live is not null;
        if (live is not null)
        {
            if (loadLiveParams)
                // pylon 参数读取可能阻塞（断线/网络延迟），后台执行避免选中即卡 UI
                UiFireAndForget.Run(() => LoadLiveParamsCore(live, value.Id, reportToMessage: false), _log);
        }
        else if (value.Registered && IsHardwareType(config.Type))
            Message = $"{value.Id} 已注册但无法调光（初始化占位或类型不支持 IExposureControl）";
        else if (!value.Registered)
            Message = $"{value.Id} 未注册（" + UnregisteredHint(config.Type) + "）";
    }

    private static bool IsHardwareType(string type) =>
        string.Equals(type, "Basler", StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, "GigEVision", StringComparison.OrdinalIgnoreCase);
    private static string UnregisteredHint(string type) => type switch
    {
        _ when string.Equals(type, "Basler", StringComparison.OrdinalIgnoreCase)
            => "未安装 pylon、相机未连接或序列号不符",
        _ when string.Equals(type, "GigEVision", StringComparison.OrdinalIgnoreCase)
            => "网口未发现相机、IP 网段不符或 UDP 被防火墙拦截",
        _ => "目录不存在或没有图片",
    };
}
