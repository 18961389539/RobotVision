using System;
using System.Windows;
using System.Windows.Input;
using RobotVision.WpfHost.Features.About;
using RobotVision.WpfHost.Features.Analysis;
using RobotVision.WpfHost.Features.Calibration;
using RobotVision.WpfHost.Features.CalibrationWizard;
using RobotVision.WpfHost.Features.Cameras;
using RobotVision.WpfHost.Features.Chat;
using RobotVision.WpfHost.Features.Communication;
using RobotVision.WpfHost.Features.Failures;
using RobotVision.WpfHost.Features.Lightings;
using RobotVision.WpfHost.Features.Logs;
using RobotVision.WpfHost.Features.Models;
using RobotVision.WpfHost.Features.Monitor;
using RobotVision.WpfHost.Features.Recipe;
using RobotVision.WpfHost.Features.Settings;
using RobotVision.WpfHost.Features.SystemInfo;

namespace RobotVision.WpfHost.Shared;

/// <summary>侧栏导航快捷键：Alt+1…9/0、Alt+Shift+1…5，顺序与 <see cref="MainWindow"/> 菜单一致。</summary>
internal static class NavigationShortcuts
{
    private static readonly (Key Key, bool Shift, Type PageType)[] Routes =
    [
        (Key.D1, false, typeof(MonitorPage)),
        (Key.D2, false, typeof(CamerasPage)),
        (Key.D3, false, typeof(LightingsPage)),
        (Key.D4, false, typeof(CalibrationWizardPage)),
        (Key.D5, false, typeof(CalibrationPage)),
        (Key.D6, false, typeof(ModelsPage)),
        (Key.D7, false, typeof(RecipePage)),
        (Key.D8, false, typeof(FailuresPage)),
        (Key.D9, false, typeof(AnalysisPage)),
        (Key.D0, false, typeof(CommunicationPage)),
        (Key.D1, true, typeof(ChatPage)),
        (Key.D2, true, typeof(LogsPage)),
        (Key.D3, true, typeof(SettingsPage)),
        (Key.D4, true, typeof(SystemPage)),
        (Key.D5, true, typeof(AboutPage)),
    ];

    internal static string GestureFor(int menuIndex) => menuIndex switch
    {
        >= 0 and <= 8 => $"Alt+{menuIndex + 1}",
        9 => "Alt+0",
        10 => "Alt+Shift+1",
        11 => "Alt+Shift+2",
        12 => "Alt+Shift+3",
        13 => "Alt+Shift+4",
        14 => "Alt+Shift+5",
        _ => string.Empty,
    };

    internal static void Register(UIElement host, Action<Type> navigate)
    {
        KeyEventHandler handler = (_, e) =>
        {
            if (!TryResolve(e, out var pageType))
                return;
            navigate(pageType);
            e.Handled = true;
        };
        host.AddHandler(Keyboard.PreviewKeyDownEvent, handler, true);
    }

    internal static bool TryResolve(KeyEventArgs e, out Type pageType)
    {
        pageType = null!;
        if (!TryGetDigitKey(e, out var digitKey))
            return false;

        var mods = Keyboard.Modifiers;
        if (!mods.HasFlag(ModifierKeys.Alt) || mods.HasFlag(ModifierKeys.Control))
            return false;

        bool shift = mods.HasFlag(ModifierKeys.Shift);
        foreach (var (key, routeShift, type) in Routes)
        {
            if (digitKey == key && shift == routeShift)
            {
                pageType = type;
                return true;
            }
        }

        return false;
    }

    /// <summary>Alt 组合键在 WPF 中常以 <see cref="Key.System"/> / <see cref="Key.ImeProcessed"/> 上报，真实键在 <see cref="KeyEventArgs.SystemKey"/>。</summary>
    internal static bool TryGetDigitKey(KeyEventArgs e, out Key digitKey)
    {
        if (IsDigitKey(e.Key))
        {
            digitKey = e.Key;
            return true;
        }

        if (e.Key is Key.System or Key.ImeProcessed && IsDigitKey(e.SystemKey))
        {
            digitKey = e.SystemKey;
            return true;
        }

        digitKey = default;
        return false;
    }

    private static bool IsDigitKey(Key key) =>
        key is Key.D0 or Key.D1 or Key.D2 or Key.D3 or Key.D4
            or Key.D5 or Key.D6 or Key.D7 or Key.D8 or Key.D9;
}
