using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;
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
    // ---- Basler 运行时调光 ----

    [RelayCommand]
    private async Task ReadLiveParamsAsync()
    {
        if (LiveExposureControl is not { } camera)
            return;
        var id = Selected?.Id ?? EditId.Trim();
        await LoadLiveParamsCore(camera, id, reportToMessage: true);
    }

    /// <summary>后台读取 pylon 光度参数并回写（仅当选中相机未变时生效，避免串扰）。</summary>
    /// <param name="reportToMessage">
    /// true：用户点了「读取当前值」，读失败写到 Message。
    /// false：选中/刷新时的静默回填，失败不改 Message（避免盖住「已保存」）。
    /// </param>
    private async Task LoadLiveParamsCore(IExposureControl camera, string cameraId, bool reportToMessage)
    {
        try
        {
            var (exposure, gain, exposureRange, gainRange) = await Task.Run(() => (
                camera.GetExposureTimeUs(), camera.GetGain(),
                camera.GetExposureRange(), camera.GetGainRange()));

            // 读取期间用户可能已切换相机：只回写仍选中的相机
            if (!string.Equals(Selected?.Id, cameraId, StringComparison.OrdinalIgnoreCase))
                return;

            if (exposureRange is { } er)
            {
                _exposureMin = Math.Max(1, er.Min);
                _exposureMax = er.Max;
                OnPropertyChanged(nameof(ExposureMin));
                OnPropertyChanged(nameof(ExposureMax));
            }
            if (gainRange is { } gr)
            {
                _gainMin = gr.Min;
                _gainMax = gr.Max;
                OnPropertyChanged(nameof(GainMin));
                OnPropertyChanged(nameof(GainMax));
            }

            ExposureUs = exposure ?? ExposureUs;
            Gain = gain ?? Gain;
            if (!reportToMessage)
                return;
            Message = exposure is null && gain is null
                ? "读取相机参数失败（无法连接相机或机型不支持读回曝光/增益，可先点「测试取图」确认在线）"
                : $"当前曝光 {ExposureUs:0} µs · 增益 {Gain:0.00} dB";
        }
        catch (Exception ex)
        {
            if (reportToMessage
                && string.Equals(Selected?.Id, cameraId, StringComparison.OrdinalIgnoreCase))
                Message = $"读取相机参数失败: {ex.Message}";
        }
    }

    /// <summary>应用滑块值到已注册相机并取一帧预览（与产线链路同一实例，立即生效）。</summary>
    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task ApplyAndGrabAsync()
    {
        var id = Selected?.Id ?? EditId.Trim();
        if (id.Length == 0 || !_cameras.TryGet(id, out var camera) || camera is not IExposureControl exposure)
            return;

        IsBusy = true;
        try
        {
            // NaN/Infinity 会穿透 Math.Clamp 与滑块校验，直接下发会污染硬件参数（NaN 曝光会令相机异常）
            if (!double.IsFinite(ExposureUs) || !double.IsFinite(Gain) || ExposureUs <= 0 || Gain < 0)
            {
                Message = $"光度参数非法（曝光/增益必须为有限正数）：曝光 {ExposureUs} · 增益 {Gain}，未下发";
                return;
            }
            var label = PreviewCameraLabel();
            Message = $"下发光度参数并取图中 · {label}";
            var entry = _cfg.Cameras.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase))
                ?? BuildConfig(id);
            var snap = await Task.Run(() =>
            {
                exposure.TrySetExposureTimeUs(ExposureUs);
                exposure.TrySetGain(Gain);
                return GrabFrameSnapshot(entry, id);
            });
            ApplyPreviewSnapshot("调光取图", label, snap);
            Message = $"取图成功 · 曝光 {ExposureUs:0} µs · 增益 {Gain:0.00} dB · {snap.ElapsedMs:0} ms";
        }
        catch (Exception ex)
        {
            Message = $"调光取图失败: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 把当前滑块值填入编辑框（曝光/增益），点"保存"后持久化。
    /// 与"保存"职责分离：滑块只做运行时调光，持久化统一走"保存"。
    /// </summary>
    [RelayCommand]
    private void SaveLiveParams()
    {
        var id = Selected?.Id ?? EditId.Trim();
        if (id.Length == 0)
            return;

        var entry = _cfg.Cameras.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
        if (entry is null || !IsHardwareType(entry.Type))
        {
            Message = $"相机 {id} 不支持写回光度参数";
            return;
        }

        EditExposureUs = ExposureUs.ToString("0", CultureInfo.InvariantCulture);
        EditGain = Gain.ToString("0.##", CultureInfo.InvariantCulture);
        Message = $"已填入编辑框：曝光 {ExposureUs:0} µs · 增益 {Gain:0.00} dB（点「保存」持久化到配置）";
    }

    /// <summary>已注册且实现 IExposureControl 的相机（不限品牌；未注册或类型不支持时返回 null）。</summary>
    private IExposureControl? LiveExposureControl
    {
        get
        {
            var id = Selected?.Id ?? EditId.Trim();
            if (id.Length == 0 || !_cameras.TryGet(id, out var camera))
                return null;
            return camera as IExposureControl;
        }
    }
}
