using CommunityToolkit.Mvvm.ComponentModel;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;

namespace RobotVision.WpfHost.Features.Recipe;

/// <summary>配方光源编辑：一台双通道控制器上的 CH1/CH2 亮度，直接写 <see cref="RecipeConfig.Lighting"/>。</summary>
public sealed class RecipeLightingEditor : ObservableObject
{
    private readonly IRecipeWorkspace _host;
    private readonly ILightingRuntime _lighting;

    internal RecipeLightingEditor(IRecipeWorkspace host, ILightingRuntime lighting)
    {
        _host = host;
        _lighting = lighting;
    }

    private RecipeConfig Editor => _host.Editor;

    public IReadOnlyList<string> LightControllerIds => [.. _lighting.ControllerIds];

    public bool UseLighting
    {
        get => Editor.Lighting is not null;
        set
        {
            if (value)
            {
                Editor.Lighting ??= NewLightingConfig();
                Editor.LightControllerId ??= PreferredControllerId();
            }
            else
            {
                Editor.Lighting = null;
                Editor.LightControllerId = null;
            }
            NotifyFromEditor();
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

    public int LightBrightness1
    {
        get => BrightnessOf(1);
        set => SetBrightness(1, value);
    }

    public int LightBrightness2
    {
        get => BrightnessOf(2);
        set => SetBrightness(2, value);
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
        OnPropertyChanged(nameof(LightBrightness1));
        OnPropertyChanged(nameof(LightBrightness2));
        OnPropertyChanged(nameof(LightStabilizeDelayMs));
        OnPropertyChanged(nameof(LightTurnOffAfterGrab));
    }

    public void RefreshControllerIds() => OnPropertyChanged(nameof(LightControllerIds));

    internal static LightingConfig NewLightingConfig() => new()
    {
        Channels =
        [
            new LightingChannelConfig { Channel = 1, Brightness = 128 },
            new LightingChannelConfig { Channel = 2, Brightness = 128 },
        ],
        StabilizeDelayMs = 20,
        TurnOffAfterGrab = true,
    };

    private string? PreferredControllerId() =>
        _lighting.ControllerIds.FirstOrDefault(id =>
            string.Equals(id, "dongguan", StringComparison.OrdinalIgnoreCase))
        ?? _lighting.ControllerIds.FirstOrDefault();

    private int BrightnessOf(int channel)
    {
        var hit = Editor.Lighting?.Channels.FirstOrDefault(c => c.Channel == channel);
        return hit?.Brightness ?? 0;
    }

    private void SetBrightness(int channel, int brightness)
    {
        if (Editor.Lighting is not { } lighting)
            return;
        var value = Math.Clamp(brightness, 0, 255);
        var hit = lighting.Channels.FirstOrDefault(c => c.Channel == channel);
        if (hit is null)
        {
            lighting.Channels.Add(new LightingChannelConfig { Channel = channel, Brightness = value });
            lighting.Channels.Sort((a, b) => a.Channel.CompareTo(b.Channel));
        }
        else
        {
            hit.Brightness = value;
        }

        OnPropertyChanged(channel == 1 ? nameof(LightBrightness1) : nameof(LightBrightness2));
        _host.NotifyDirty();
    }
}
