using System.ComponentModel;
using ImageViewer.Controls;
using ViewerControl = ImageViewer.Controls.ImageViewer;

namespace RobotVision.WpfHost.Features.Recipe;

/// <summary>配方页 ROI 框选：协调 ViewModel 与双 ImageViewer，不落在 Page code-behind。</summary>
internal sealed class RecipePageRoiCoordinator : IDisposable
{
    private readonly RecipeViewModel _vm;
    private readonly ViewerControl _testViewer;
    private readonly ViewerControl _roiViewer;
    private readonly RecipeRoiLiveSync _sync;
    private bool _wired;

    public RecipePageRoiCoordinator(RecipeViewModel vm, ViewerControl testViewer, ViewerControl roiViewer)
    {
        _vm = vm;
        _testViewer = testViewer;
        _roiViewer = roiViewer;
        _sync = new RecipeRoiLiveSync(
            vm.Roi,
            ResolveActiveViewer,
            [testViewer, roiViewer],
            () => _vm.Roi.UseRoi ? _vm.Editor.Roi : null,
            () => _vm.UsesFeatureTeachRoi ? _vm.Editor.Template?.Roi : null,
            () => _vm.UsesFeatureTeachRoi,
            ShowTeachFeatureRect);
    }

    public void Wire()
    {
        if (_wired)
            return;
        _wired = true;
        _sync.Wire();
        _vm.PropertyChanged += OnVmPropertyChanged;
        _vm.Test.PropertyChanged += OnTestPropertyChanged;
    }

    public void Unwire()
    {
        if (!_wired)
            return;
        _wired = false;
        _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm.Test.PropertyChanged -= OnTestPropertyChanged;
        _sync.Unwire();
    }

    public void Dispose()
    {
        Unwire();
        _sync.Dispose();
    }

    public void BeginRoiDraw(bool template)
    {
        if (template && !_vm.UsesFeatureTeachRoi)
            return;

        _sync.IsTemplateDrawTarget = template;
        _sync.StartDrawAfterGrab = false;
        _sync.StartDrawTemplateAfterGrab = false;

        var hasResult = _vm.Test.ResultImage is not null;
        var adopted = _vm.Roi.TryAdoptDisplayedImage(
            _vm.Test.ResultImage,
            _vm.Editor.CameraId,
            template ? "框选特征：沿用当前结果图" : "框选检测区：沿用当前结果图",
            keepCurrentPreview: hasResult);
        if (adopted)
        {
            _vm.ShowTestImageViewCommand.Execute(null);
            _sync.SyncFromRecipe();
            _sync.StartRoiMode();
            return;
        }

        _vm.ShowRoiPreviewViewCommand.Execute(null);
        if (_vm.Roi.HasRoiRefFrame)
        {
            if (template)
                _vm.Roi.EnsureFeatureRoiDrawable();
            _sync.StartRoiMode();
            return;
        }

        if (template)
            _sync.StartDrawTemplateAfterGrab = true;
        else
            _sync.StartDrawAfterGrab = true;
        _vm.Roi.PreviewRoiCommand.Execute(null);
    }

    private ViewerControl ResolveActiveViewer() =>
        _vm is { ShowTestImage: true, Test.ResultImage: not null } ? _testViewer : _roiViewer;

    private bool ShowTeachFeatureRect()
    {
        if (!_vm.UsesFeatureTeachRoi || !_vm.Roi.UseTemplateRoi)
            return false;
        if (_vm.ShowTestImage && _vm.Test.ResultImage is not null && !_sync.IsTemplateDrawTarget)
            return false;
        return true;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RecipeViewModel.ShowTestImage) && _vm.Roi.HasRoiRefFrame)
            _sync.OnViewContextChanged();
        if (e.PropertyName is nameof(RecipeViewModel.IsTemplateMethod)
            or nameof(RecipeViewModel.UsesFeatureTeachRoi)
            or nameof(RecipeViewModel.Editor))
        {
            if (_vm.Roi.HasRoiRefFrame)
                _sync.OnViewContextChanged();
        }
    }

    private void OnTestPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(RecipeTestSession.ResultImage))
            return;
        if (_vm.ShowTestImage)
            _sync.IsTemplateDrawTarget = false;
        if (_vm.Roi.HasRoiRefFrame)
            _sync.OnViewContextChanged();
    }
}
