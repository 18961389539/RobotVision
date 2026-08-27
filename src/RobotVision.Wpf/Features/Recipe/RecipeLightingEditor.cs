using CommunityToolkit.Mvvm.ComponentModel;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Lighting;

namespace RobotVision.WpfHost.Features.Recipe;

/// <summary>配方光源扁平编辑字段，直接写 <see cref="RecipeConfig.Lighting"/>。</summary>
public sealed class RecipeLightingEditor : ObservableObject
{
    private readonly IRecipeWorkspace _host;
    private readonly LightingManager _lighting;

    internal RecipeLightingEditor(IRecipeWorkspace host, LightingManager lighting)
    {
        _host = host;
        _lighting = lighting;
    }

    private RecipeConfig Editor => _host.Editor;

    public IReadOnlyList<string> LightControllerIds => _lighting.ControllerIds.ToList();

    public bool UseLighting
    {
        get => Editor.Lighting is not null;
        set
        {
            if (value)
            {
                Editor.Lighting ??= NewLightingConfig();
                Editor.LightControllerId ??= LightControllerIds.FirstOrDefault();
            }
            else
            {
                Editor.Lighting = null;
                Editor.LightControllerId = null;
            }
            OnPropertyChanged();
            _host.NotifyDirty();
        }
    }

    public string? SelectedLightControllerId
    {
        get => Editor.LightControllerId;
        set
        {
            Editor.LightControllerId = string.IsNullOrWhiteSpace(value) ? null : value;
            OnPropertyChanged();
            _host.NotifyDirty();
        }
    }

    public int LightChannel
    {
        get => Editor.Lighting?.Channels.FirstOrDefault()?.Channel ?? 1;
        set
        {
            if (Editor.Lighting is { } l)
            {
                Channel0(l).Channel = Math.Max(1, value);
                OnPropertyChanged();
                _host.NotifyDirty();
            }
        }
    }

    public int LightBrightness
    {
        get => Editor.Lighting?.Channels.FirstOrDefault()?.Brightness ?? 128;
        set
        {
            if (Editor.Lighting is { } l)
            {
                Channel0(l).Brightness = Math.Clamp(value, 0, 255);
                OnPropertyChanged();
                _host.NotifyDirty();
            }
        }
    }

    public int LightStabilizeDelayMs
    {
        get => Editor.Lighting?.StabilizeDelayMs ?? 0;
        set
        {
            if (Editor.Lighting is { } l)
            {
                l.StabilizeDelayMs = Math.Max(0, value);
                OnPropertyChanged();
                _host.NotifyDirty();
            }
        }
    }

    public bool LightTurnOffAfterGrab
    {
        get => Editor.Lighting?.TurnOffAfterGrab ?? true;
        set
        {
            if (Editor.Lighting is { } l)
            {
                l.TurnOffAfterGrab = value;
                OnPropertyChanged();
                _host.NotifyDirty();
            }
        }
    }

    public void NotifyFromEditor()
    {
        OnPropertyChanged(nameof(UseLighting));
        OnPropertyChanged(nameof(SelectedLightControllerId));
        OnPropertyChanged(nameof(LightChannel));
        OnPropertyChanged(nameof(LightBrightness));
        OnPropertyChanged(nameof(LightStabilizeDelayMs));
        OnPropertyChanged(nameof(LightTurnOffAfterGrab));
    }

    public void RefreshControllerIds() => OnPropertyChanged(nameof(LightControllerIds));

    private static LightingConfig NewLightingConfig() => new()
    {
        Channels = [new LightingChannelConfig { Channel = 1, Brightness = 128 }],
        StabilizeDelayMs = 0,
        TurnOffAfterGrab = true,
    };

    private static LightingChannelConfig Channel0(LightingConfig lighting)
    {
        if (lighting.Channels.Count == 0)
            lighting.Channels.Add(new LightingChannelConfig());
        return lighting.Channels[0];
    }
}
