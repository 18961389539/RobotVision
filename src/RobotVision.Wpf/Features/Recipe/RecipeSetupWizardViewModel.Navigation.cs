using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Infrastructure;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Inference;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Teach;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Recipe;
internal sealed partial class RecipeSetupWizardViewModel
{
    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void Back()
    {
        if (Step > SetupWizardStep.Welcome)
            Step--;
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void Next()
    {
        if (Step == SetupWizardStep.Result)
            ApplyRecommendationToEditor();
        if (Step < SetupWizardStep.TeachVerify)
            Step++;
    }

    [RelayCommand]
    private void Cancel()
    {
        ReleasePreview();
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void GoTo(SetupWizardStep step)
    {
        if (IsBusy)
            return;
        if (step == SetupWizardStep.Result && _scene is null && !UseBlobsWithoutModel && !HasTwoLandmarks)
        {
            Message = "请先在「分析」页运行画面分析后再查看建议。";
            return;
        }
        if (step == SetupWizardStep.TeachVerify && _chosen is null)
        {
            Message = "请先在「建议」页选定方案后再示教与验证。";
            return;
        }
        if (step == SetupWizardStep.TeachVerify)
            ApplyRecommendationToEditor();
        Step = step;
    }

    private bool CanGrabFrame => !IsBusy && !string.IsNullOrWhiteSpace(_host.Editor.CameraId);

    [RelayCommand(CanExecute = nameof(CanGrabFrame))]
    private async Task GrabFrameAsync()
    {
        if (_roi.PreviewRoiCommand.CanExecute(null))
            await _roi.PreviewRoiCommand.ExecuteAsync(null);
        OnPropertyChanged(nameof(ViewerImage));
    }

    [RelayCommand(CanExecute = nameof(CanGrabFrame))]
    private void DrawDetectionRoi()
    {
        _roi.UseRoi = true;
        RequestBeginDetectionRoiDraw?.Invoke();
    }

    [RelayCommand]
    private void DrawFeatureRoi()
    {
        if (IsTeachVerify && _test.ResultImage is not null)
            _roi.TryAdoptDisplayedImage(_test.ResultImage, _host.Editor.CameraId, "框选特征：沿用试触发结果图");
        RequestBeginFeatureRoiDraw?.Invoke();
    }
}
