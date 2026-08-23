using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Hosting.Lighting;
using RobotVision.Infrastructure.Lighting;

namespace RobotVision.UI;

public sealed record LightListItem(string Id, string Type, string Status, bool Registered, bool IsNoop);

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

    [ObservableProperty]
    private double _brightness = 128;

    [ObservableProperty]
    private string _message = "";

    /// <summary>新建控制器的 Id 输入。</summary>
    [ObservableProperty]
    private string _newId = "";

    /// <summary>新建控制器的类型（从注册表 TypeNames 下拉选择）。</summary>
    [ObservableProperty]
    private string _newType = "None";

    public double BrightnessMax => 255;

    public string BrightnessText => $"{Brightness:0}";

    /// <summary>可添加的光源类型下拉数据源（来自 LightControllerTypeRegistry，与相机页同构）。</summary>
    public IReadOnlyList<string> TypeOptions => _registry.TypeNames;

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
                string.Equals(light.Type, "None", StringComparison.OrdinalIgnoreCase)));
        }
        Selected = Items.FirstOrDefault(i => i.Id == Selected?.Id) ?? Items.FirstOrDefault();
        Message = $"共 {Items.Count} 个光源控制器";
    }

    partial void OnBrightnessChanged(double value) => OnPropertyChanged(nameof(BrightnessText));

    partial void OnSelectedChanged(LightListItem? value)
    {
        if (value is null)
            return;
        Message = value.Registered
            ? (value.IsNoop
                ? $"{value.Id} 是无操作控制器（None）：开灯不会点亮任何硬件，仅用于配方联调"
                : $"{value.Id} 已注册，可手动调光")
            : $"{value.Id} 未注册（appsettings 中类型暂未接入或初始化失败）";
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
        try
        {
            var instance = _registry.Create(entry)
                ?? throw new InvalidOperationException($"类型 {type} 工厂创建返回 null");
            var list = _cfg.LightControllers.ToList();
            list.Add(entry);

            _store.Save(list);
            _lighting.Register(instance);

            NewId = "";
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
