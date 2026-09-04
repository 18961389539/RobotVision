using System.ComponentModel;
using ImageViewer.Controls;
using RobotVision.Core.Recipe;
using RobotVision.WpfHost.Shared;
using ViewerControl = ImageViewer.Controls.ImageViewer;

namespace RobotVision.WpfHost.Features.Recipe;

/// <summary>配方页 ROI 框选：协调 ViewModel 与双 ImageViewer，不落在 Page code-behind。</summary>
internal sealed class RecipePageRoiCoordinator : IDisposable
{
    private readonly RecipeViewModel _vm;
    private readonly IRoiViewport _testViewport;
    private readonly IRoiViewport _roiViewport;
    private readonly RecipeRoiLiveSync _sync;
    private bool _wired;

    public RecipePageRoiCoordinator(RecipeViewModel vm, ViewerControl testViewer, ViewerControl roiViewer)
        : this(vm, new ImageViewerRoiViewport(testViewer), new ImageViewerRoiViewport(roiViewer))
    {
    }

    internal RecipePageRoiCoordinator(RecipeViewModel vm, IRoiViewport testViewport, IRoiViewport roiViewport)
    {
        _vm = vm;
        _testViewport = testViewport;
        _roiViewport = roiViewport;
        _sync = new RecipeRoiLiveSync(
            vm.Roi,
            () => _vm is { ShowTestImage: true, Test.ResultImage: not null } ? _testViewport : _roiViewport,
            [_testViewport, _roiViewport],
            () => _vm.Roi.UseRoi ? _vm.Editor.Roi : null,
            () => _vm.UsesFeatureTeachRoi ? _vm.Editor.Template?.Roi : null,
            () => _vm.UsesFeatureTeachRoi,
            ShowTeachFeatureRect,
            () => _vm.Editor.Template?.RefineLine,
            () => TemplateOptions.UsesTaughtRefineLine(_vm.Editor.Template?.RefineMethod ?? SegmentRefineMethod.Template),
            () => _vm.IsDualBlobMode ? _vm.Editor.Blob.SecondaryRoi : null,
            () => _vm.IsDualBlobMode);
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

    public void BeginRoiDraw(bool template) =>
        BeginRoiDraw(template ? RecipeRoiDrawKind.Template : RecipeRoiDrawKind.Detection);

    public void BeginRoiDraw(RecipeRoiDrawKind kind)
    {
        if (kind == RecipeRoiDrawKind.Template && !_vm.UsesFeatureTeachRoi)
            return;
        if (kind == RecipeRoiDrawKind.SecondaryBlob && !_vm.IsDualBlobMode)
            return;

        _sync.SetDrawTarget(kind);
        _sync.StartDrawAfterGrab = false;
        _sync.StartDrawTemplateAfterGrab = false;
        _sync.StartDrawSecondaryAfterGrab = false;

        var caption = kind switch
        {
            RecipeRoiDrawKind.Template => "框选特征：沿用当前结果图",
            RecipeRoiDrawKind.SecondaryBlob => "框选 ROI2（BLOB2）：沿用当前结果图",
            _ => "框选检测区：沿用当前结果图",
        };

        var hasResult = _vm.Test.ResultImage is not null;
        var adopted = _vm.Roi.TryAdoptDisplayedImage(
            _vm.Test.ResultImage,
            _vm.Editor.CameraId,
            caption,
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
            if (kind == RecipeRoiDrawKind.Template)
                _vm.Roi.EnsureFeatureRoiDrawable();
            if (kind == RecipeRoiDrawKind.SecondaryBlob)
                _vm.Roi.UseSecondaryRoi = true;
            _sync.StartRoiMode();
            return;
        }

        if (kind == RecipeRoiDrawKind.Template)
            _sync.StartDrawTemplateAfterGrab = true;
        else if (kind == RecipeRoiDrawKind.SecondaryBlob)
            _sync.StartDrawSecondaryAfterGrab = true;
        else
            _sync.StartDrawAfterGrab = true;
        _vm.Roi.PreviewRoiCommand.Execute(null);
    }

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
            or nameof(RecipeViewModel.UsesRefineLine)
            or nameof(RecipeViewModel.IsDualBlobMode)
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
