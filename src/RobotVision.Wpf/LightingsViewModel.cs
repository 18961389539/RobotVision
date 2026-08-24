using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Hosting.Lighting;
using RobotVision.Infrastructure.Lighting;

namespace RobotVision.WpfHost;

public sealed record LightListItem(string Id, string Type, string Status, bool Registered, bool IsNoop, string Summary);

/// <summary>
/// 光源管理：展示 appsettings 注册的光源控制器（Id/类型/注册状态），
/// 手动开灯/关灯/调亮度（经 LightingManager 作用于运行中的服务，立即生效）。
/// 支持添加控制器（类型从 LightControllerTypeRegistry 下拉选择，None 为调试兜底）
/// 与删除控制器，与相机管理页同构。
/// </summary>
public partial class LightingsViewModel : ObservableObject
{
    private readonly AppConfig _cfg;
    private readonly LightingManager _lighting;
    private readonly LightingConfigStore _store;
    private readonly RecipeLoader _recipes;
    private readonly LightControllerTypeRegistry _registry;

    public ObservableCollection<LightListItem> Items { get; } = [];

    [ObservableProperty]
    private LightListItem? _selected;

    [ObservableProperty]
    private int _channel = 1;

    /// <summary>亮度（0-255，整数）。Slider/NumberBox 输入经 setter 强制取整，避免下发小数。</summary>
    private double _brightness = 128;

    public double Brightness
    {
        get => _brightness;
        set
        {
            var rounded = Math.Round(value);
            if (Math.Abs(_brightness - rounded) > 0.001)
            {
                _brightness = rounded;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BrightnessText));
            }
        }
    }

    [ObservableProperty]
    private string _message = "";

    /// <summary>新建控制器的 Id 输入。</summary>
    [ObservableProperty]
    private string _newId = "";

    /// <summary>新建控制器的类型（从注册表 TypeNames 下拉选择）。</summary>
    [ObservableProperty]
    private string _newType = "None";

    /// <summary>新建区是否需要显示 Network 参数（仅选 Network 时显示，避免选 None/Serial 时多填）。</summary>
    public bool IsNewNetwork => string.Equals(NewType, "Network", StringComparison.OrdinalIgnoreCase);

    /// <summary>新建区是否需要显示 Serial 参数。</summary>
    public bool IsNewSerial => string.Equals(NewType, "Serial", StringComparison.OrdinalIgnoreCase);

    // ---- 新建 Serial 专属参数 ----

    [ObservableProperty]
    private string _newPort = "";

    [ObservableProperty]
    private int _newBaudRate = 9600;

    // ---- 新建 Network 专属参数 ----

    [ObservableProperty]
    private string _newEndpoint = "";

    [ObservableProperty]
    private string _newProtocol = "Tcp";

    [ObservableProperty]
    private string _newLocalEndpoint = "";

    [ObservableProperty]
    private int _newTimeoutMs = 200;

    [ObservableProperty]
    private int _newReconnectAttempts = 3;

    partial void OnNewTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsNewNetwork));
        OnPropertyChanged(nameof(IsNewSerial));
    }

    public double BrightnessMax => 255;

    public string BrightnessText => $"{Brightness:0}";

    /// <summary>可添加的光源类型下拉数据源（来自 LightControllerTypeRegistry，与相机页同构）。</summary>
    public IReadOnlyList<string> TypeOptions => _registry.TypeNames;

    /// <summary>选中光源是否为 Network（决定是否显示网络专属参数编辑面板）。</summary>
    public bool IsNetwork => Selected is not null
        && string.Equals(Selected.Type, "Network", StringComparison.OrdinalIgnoreCase);

    /// <summary>选中光源是否为 Serial（决定是否显示串口专属参数编辑面板）。</summary>
    public bool IsSerial => Selected is not null
        && string.Equals(Selected.Type, "Serial", StringComparison.OrdinalIgnoreCase);

    // ---- Serial 专属参数（编辑区绑定） ----

    [ObservableProperty]
    private string _editPort = "";

    [ObservableProperty]
    private int _editBaudRate = 9600;

    // ---- Network 专属参数（编辑区绑定） ----

    [ObservableProperty]
    private string _editEndpoint = "";

    [ObservableProperty]
    private string _editProtocol = "Tcp";

    [ObservableProperty]
    private string _editLocalEndpoint = "";

    [ObservableProperty]
    private int _editTimeoutMs = 200;

    [ObservableProperty]
    private int _editReconnectAttempts = 3;

    /// <summary>协议调试：待发送的原始指令文本（支持 \r \n \t 转义）。</summary>
    [ObservableProperty]
    private string _debugCommand = "";

    /// <summary>最近一次发送结果（便于确认已发出）。</summary>
    [ObservableProperty]
    private string _debugResult = "";

    /// <summary>发送原始指令到选中的光源控制器（协议调试）。</summary>
    [RelayCommand]
    private void SendDebug()
    {
        if (Selected is null)
        {
            Message = "请先选择光源控制器";
            return;
        }
        if (string.IsNullOrWhiteSpace(DebugCommand))
        {
            Message = "请输入要发送的指令（支持 \r \n \t 转义）";
            return;
        }
        if (!_lighting.TryGet(Selected.Id, out var controller) || controller is null)
        {
            Message = $"{Selected.Id} 未注册，无法发送";
            return;
        }
        try
        {
            controller.SendRaw(DebugCommand);
            DebugResult = DebugCommand;
            Message = $"已发送到 {Selected.Id}：{DebugCommand}";
        }
        catch (Exception ex)
        {
            Message = $"发送失败: {ex.Message}";
        }
    }

    public LightingsViewModel(
        AppConfig cfg,
        LightingManager lighting,
        LightingConfigStore store,
        RecipeLoader recipes,
        LightControllerTypeRegistry registry)
    {
        _cfg = cfg;
        _lighting = lighting;
        _store = store;
        _recipes = recipes;
        _registry = registry;
        Refresh();
    }

    [RelayCommand]
    public void Refresh()
    {
        Items.Clear();
        foreach (var light in _cfg.LightControllers)
        {
            var registered = _lighting.IsRegistered(light.Id);
            Items.Add(new LightListItem(
                light.Id, light.Type,
                registered ? "已注册" : "未注册", registered,
                string.Equals(light.Type, "None", StringComparison.OrdinalIgnoreCase),
                Summarize(light)));
        }
        Selected = Items.FirstOrDefault(i => i.Id == Selected?.Id) ?? Items.FirstOrDefault();
        Message = $"共 {Items.Count} 个光源控制器";
    }

    /// <summary>列表项第二行摘要：Network 显示端点、Serial 显示串口、None 显示无操作提示。</summary>
    private static string Summarize(LightControllerConfig light)
    {
        if (string.Equals(light.Type, "Network", StringComparison.OrdinalIgnoreCase))
        {
            var endpoint = string.IsNullOrWhiteSpace(light.Endpoint) ? "未配置端点" : light.Endpoint;
            return $"{light.Protocol} {endpoint} · 超时 {light.TimeoutMs}ms";
        }
        if (string.Equals(light.Type, "Serial", StringComparison.OrdinalIgnoreCase))
        {
            var port = string.IsNullOrWhiteSpace(light.Port) ? "未配置串口" : light.Port;
            return $"{port} @ {light.BaudRate}";
        }
        return "无操作虚拟控制器（调试兜底）";
    }

    partial void OnSelectedChanged(LightListItem? value)
    {
        // 无论选中与否都通知类型面板可见性重新计算
        OnPropertyChanged(nameof(IsNetwork));
        OnPropertyChanged(nameof(IsSerial));

        if (value is null)
            return;

        // 加载该光源的现有配置到编辑字段
        var config = _cfg.LightControllers.FirstOrDefault(
            c => string.Equals(c.Id, value.Id, StringComparison.OrdinalIgnoreCase));
        if (config is null)
        {
            EditEndpoint = "";
            EditProtocol = "Tcp";
            EditLocalEndpoint = "";
            EditTimeoutMs = 200;
            EditReconnectAttempts = 3;
            EditPort = "";
            EditBaudRate = 9600;
        }
        else
        {
            EditEndpoint = config.Endpoint;
            EditProtocol = string.IsNullOrWhiteSpace(config.Protocol) ? "Tcp" : config.Protocol;
            EditLocalEndpoint = config.LocalEndpoint;
            EditTimeoutMs = config.TimeoutMs > 0 ? config.TimeoutMs : 200;
            EditReconnectAttempts = config.ReconnectAttempts;
            EditPort = config.Port;
            EditBaudRate = config.BaudRate is >= 1200 and <= 921600 ? config.BaudRate : 9600;
        }

        Message = value.Registered
            ? (value.IsNoop
                ? $"{value.Id} 是无操作控制器（None）：开灯不会点亮任何硬件，仅用于配方联调"
                : $"{value.Id} 已注册，可手动调光")
            : $"{value.Id} 未注册（appsettings 中类型暂未接入或初始化失败）";
    }

    /// <summary>保存当前选中光源的参数到 appsettings（Network 类型写 Endpoint/Protocol 等；其他类型仅刷新）。</summary>
    [RelayCommand]
    private void SaveSelected()
    {
        if (Selected is null)
        {
            Message = "保存失败：请先选择光源";
            return;
        }
        var id = Selected.Id;
        var list = _cfg.LightControllers.ToList();
        var idx = list.FindIndex(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
        if (idx < 0)
        {
            Message = $"保存失败：找不到 {id}";
            return;
        }
        var entry = list[idx];

        if (string.Equals(entry.Type, "Network", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(EditEndpoint))
            {
                Message = "保存失败：Network 光源需填写 Endpoint（host:port）";
                return;
            }
            entry.Endpoint = EditEndpoint.Trim();
            entry.Protocol = string.IsNullOrWhiteSpace(EditProtocol) ? "Tcp" : EditProtocol.Trim();
            entry.LocalEndpoint = EditLocalEndpoint?.Trim() ?? "";
            entry.TimeoutMs = EditTimeoutMs > 0 ? EditTimeoutMs : 200;
            entry.ReconnectAttempts = Math.Max(0, EditReconnectAttempts);
        }
        else if (string.Equals(entry.Type, "Serial", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(EditPort))
            {
                Message = "保存失败：Serial 光源需填写串口名（如 COM3）";
                return;
            }
            entry.Port = EditPort.Trim();
            entry.BaudRate = EditBaudRate is >= 1200 and <= 921600 ? EditBaudRate : 9600;
        }

        try
        {
            _store.Save(list);
            // 类型/参数变更后重新创建并注册运行时实例（保持光照服务立即生效）
            _lighting.Unregister(id);
            if (_registry.Create(entry) is { } newInstance)
                _lighting.Register(newInstance);
            Refresh();
            Message = $"已保存 {id}（{entry.Type}）";
        }
        catch (Exception ex)
        {
            Message = $"保存失败: {ex.Message}";
        }
    }

    /// <summary>添加控制器（类型从注册表下拉选择；实例经注册表工厂创建，与相机页同构）。</summary>
    [RelayCommand]
    private void Add()
    {
        var id = NewId.Trim();
        var type = NewType?.Trim() ?? "";
        if (id.Length == 0)
        {
            Message = "添加失败：请先填写控制器 Id";
            return;
        }
        if (!RecipeLoader.IsValidRecipeName(id))
        {
            Message = "添加失败：Id 只允许字母、数字、下划线、中划线（长度 ≤ 64）";
            return;
        }
        if (_cfg.LightControllers.Any(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase)))
        {
            Message = $"添加失败：控制器 {id} 已存在";
            return;
        }
        if (!_registry.IsKnown(type))
        {
            Message = $"添加失败：类型 {type} 无工厂（实现 ILightControllerFactory 并调用 LightControllerTypeRegistry.Register 一行接入）";
            return;
        }

        var entry = new LightControllerConfig { Id = id, Type = type };
        if (string.Equals(type, "Network", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(NewEndpoint))
            {
                Message = "添加失败：Network 光源需填写 Endpoint（host:port）";
                return;
            }
            entry.Endpoint = NewEndpoint.Trim();
            entry.Protocol = string.IsNullOrWhiteSpace(NewProtocol) ? "Tcp" : NewProtocol.Trim();
            entry.LocalEndpoint = NewLocalEndpoint?.Trim() ?? "";
            entry.TimeoutMs = NewTimeoutMs > 0 ? NewTimeoutMs : 200;
            entry.ReconnectAttempts = Math.Max(0, NewReconnectAttempts);
        }
        else if (string.Equals(type, "Serial", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(NewPort))
            {
                Message = "添加失败：Serial 光源需填写串口名（如 COM3）";
                return;
            }
            entry.Port = NewPort.Trim();
            entry.BaudRate = NewBaudRate is >= 1200 and <= 921600 ? NewBaudRate : 9600;
        }
        try
        {
            var instance = _registry.Create(entry)
                ?? throw new InvalidOperationException($"类型 {type} 工厂创建返回 null");
            var list = _cfg.LightControllers.ToList();
            list.Add(entry);

            _store.Save(list);
            _lighting.Register(instance);

            NewId = "";
            NewEndpoint = "";
            NewLocalEndpoint = "";
            NewPort = "";
            Refresh();
            Message = $"已添加 {type} 控制器 {id}（运行时已注册）";
        }
        catch (Exception ex)
        {
            Message = $"添加失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Delete()
    {
        if (Selected is null)
        {
            Message = "请先选择要删除的控制器";
            return;
        }
        var id = Selected.Id;

        var referenced = _recipes.ListNames()
            .Where(n =>
            {
                try { return _recipes.Get(n).LightControllerId == id; }
                catch { return false; }
            })
            .ToList();
        var warn = referenced.Count > 0
            ? $"\n有 {referenced.Count} 个配方引用该控制器（{string.Join("、", referenced)}），删除后触发将返回 1006 错误。"
            : "";

        if (MessageBox.Show($"确定删除光源控制器 {id}？{warn}", "删除光源控制器",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        try
        {
            var list = _cfg.LightControllers
                .Where(c => !string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase))
                .ToList();
            _store.Save(list);
            _lighting.Unregister(id);
            Refresh();
            Message = $"已删除 {id}";
        }
        catch (Exception ex)
        {
            Message = $"删除失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void TurnOn()
    {
        if (Selected is not { Registered: true } item)
        {
            Message = "请选择已注册的光源控制器";
            return;
        }
        try
        {
            _lighting.TurnOn(item.Id, Math.Max(1, Channel), (int)Math.Round(Brightness));
            Message = item.IsNoop
                ? $"{item.Id} 是无操作控制器（None）：已模拟点亮，未点亮任何硬件"
                : $"已点亮 {item.Id} · 通道 {Channel} · 亮度 {BrightnessText}（持续到手动熄灯或下次取图点亮）";
        }
        catch (Exception ex)
        {
            Message = $"开灯失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void TurnOff()
    {
        if (Selected is not { Registered: true } item)
        {
            Message = "请选择已注册的光源控制器";
            return;
        }
        try
        {
            _lighting.TurnOff(item.Id);
            Message = $"已熄灯 {item.Id}";
        }
        catch (Exception ex)
        {
            Message = $"熄灯失败: {ex.Message}";
        }
    }
}
