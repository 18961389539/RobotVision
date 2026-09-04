using System.ComponentModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Teach;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Recipe;

internal sealed partial class RecipeSetupWizardViewModel
{
    private bool CanAnalyze => !IsBusy && !string.IsNullOrWhiteSpace(_host.Editor.CameraId);

    [RelayCommand(CanExecute = nameof(CanAnalyze))]
    private async Task AnalyzeAsync()
    {
        var cameraId = _host.Editor.CameraId;
        if (string.IsNullOrWhiteSpace(cameraId))
        {
            Message = "请先在配方里选择相机。";
            return;
        }

        var generation = _pageSession.CaptureGeneration();
        var ct = _pageSession.Token;
        var work = AnalyzeCoreAsync(cameraId, generation, ct);
        _pageSession.Track(work);
        await work;
    }

    private async Task AnalyzeCoreAsync(string cameraId, int generation, CancellationToken ct)
    {
        IsBusy = true;
        _host.IsBusy = true;
        try
        {
            _host.CommitEdits();
            this.Commit();
            using var lightingScope = _lighting.Apply(_host.Editor.LightControllerId, _host.Editor.Lighting);
            if (lightingScope.StabilizeDelayMs > 0)
                await Task.Delay(lightingScope.StabilizeDelayMs, ct);

            var request = BuildAnalysisRequest(cameraId);
            var progress = new Progress<string>(text => Report(text, ct));
            RecipeSetupAnalysisResult result = await Task.Run(() =>
            {
                if (ScoreAllPlayback && _cameras.GetPlaybackFiles(cameraId) is { Count: > 0 })
                    return _analysis.AnalyzePlayback(request, ct, progress);
                return _analysis.AnalyzeGrab(request, ct, progress);
            }, ct).ConfigureAwait(true);

            if (!_pageSession.IsCurrent(generation) || ct.IsCancellationRequested)
            {
                result.Dispose();
                return;
            }

            ApplyAnalysis(result);
            result.Dispose();
            _userPicked = false;
            RefreshPlaybook();
            Step = SetupWizardStep.Result;
            OnPropertyChanged(nameof(ViewerImage));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (_pageSession.IsCurrent(generation))
                Message = $"分析失败：{ex.Message}";
        }
        finally
        {
            if (_pageSession.IsCurrent(generation))
            {
                _host.IsBusy = false;
                IsBusy = false;
            }
        }
    }

    private RecipeSetupAnalysisRequest BuildAnalysisRequest(string cameraId) => new(
        _host.Editor,
        Constraints,
        CurrentPrior,
        UseBlobsWithoutModel,
        ScoreAllPlayback,
        cameraId);

    private void ApplyAnalysis(RecipeSetupAnalysisResult result)
    {
        _scene = result.Scene;
        _bakeoff = result.BakeOff;
        _sceneVotes = result.SceneVotes;
        _perFrame = result.PerFrame ?? [];
        _instanceCounts = result.InstanceCounts ?? [];
        _edgePolarity = result.Edge;
        _tabPolarity = result.Tab;
        _featureRanks = result.FeatureRanks;
        _refineAdvice = result.Locks;
        _previewBuffer = result.Preview;
        result.Preview = null;
        _previewBitmap = _previewBuffer is null ? null : ImageConverter.ToBitmapSource(_previewBuffer);
        Preview = _previewBitmap;
        Detected = result.Detected;
        Total = result.Total;
        _syncingFeature = true;
        var pickLocal = result.FeatureRanks.Count > 0 && result.Scene is { Separability: < 0.10 };
        SelectedFeatureIndex = pickLocal ? 1 : 0;
        _featureRoi = pickLocal ? result.FeatureRanks[0].Roi : null;
        RebuildFeatureRoiRows();
        _syncingFeature = false;
        SyncFeatureOverlay();
        OnPropertyChanged(nameof(PreviewPixelWidth));
        OnPropertyChanged(nameof(PreviewPixelHeight));
        SceneSummary = result.Scene is { } s
            ? $"{ScenePlaybook.SceneLabel(s.Kind)} · {LightingLabel(s.Lighting)} · 轴比 {s.Aspect:0.0} · 圆度 {s.Circularity:0.00} · 熵 {s.TextureEntropy:0.0}（相对 {s.RelativeEntropy:+0.0;-0.0}） · 0/180 分差 {s.Separability:0.00} · {(s.HoleOk ? $"有孔/槽 {s.HoleQuality:0.00}" : "无孔")}"
              + (s.KindConfidence < 1 ? $" · 分类把握 {s.KindConfidence:0.00}" : "")
              + (result.Total > 1 ? $" · 帧 {result.Detected}/{result.Total}" : "")
              + (result.InstanceCount > 0 ? $" · 本帧 {result.InstanceCount} 件" : "")
              + (Constraints.ExpectedCount > 0 ? $"（期望 {Constraints.ExpectedCount}）" : "")
              + "。" + s.Why
              + (result.CountUnstable
                  ? " 件数与期望不符或不稳，场景按置信最高且件数匹配的帧（没有则退回冠军件），请核对漏检。"
                  : "")
            : "未检出分割目标，仅按任务约束推荐。";
        Message = result.Message;
        OnPropertyChanged(nameof(HasEnoughForResult));
        OnPropertyChanged(nameof(NextLabel));
    }

    private static string LightingLabel(LightingClass lighting) => lighting switch
    {
        LightingClass.DarkField => "暗场",
        LightingClass.BrightField => "亮场",
        _ => "打光未分",
    };

    private void Report(string text, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return;
        UiDispatch.Begin(() =>
        {
            if (!ct.IsCancellationRequested)
                Message = text;
        });
    }
}
