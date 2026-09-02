using ImageViewer.Controls;
using RobotVision.WpfHost.Shared;
using ViewerControl = ImageViewer.Controls.ImageViewer;

namespace RobotVision.WpfHost.Features.Recipe;

/// <summary>向导内 ImageViewer：检测 ROI / 特征 ROI 与 <see cref="RecipeRoiEditor"/> 双向同步。</summary>
internal sealed class RecipeWizardImageHost : IDisposable
{
    private readonly RecipeRoiEditor _roi;
    private readonly Func<bool> _usesFeatureTeachRoi;
    private readonly IRoiViewport _viewport;
    private readonly RecipeRoiLiveSync _sync;

    public RecipeWizardImageHost(
        ViewerControl viewer,
        IRecipeWorkspace host,
        RecipeRoiEditor roi,
        Func<bool> usesFeatureTeachRoi)
        : this(new ImageViewerRoiViewport(viewer), host, roi, usesFeatureTeachRoi)
    {
    }

    internal RecipeWizardImageHost(
        IRoiViewport viewport,
        IRecipeWorkspace host,
        RecipeRoiEditor roi,
        Func<bool> usesFeatureTeachRoi)
    {
        _roi = roi;
        _usesFeatureTeachRoi = usesFeatureTeachRoi;
        _viewport = viewport;
        _sync = new RecipeRoiLiveSync(
            roi,
            () => _viewport,
            [_viewport],
            () => roi.HasRoiRefFrame ? host.Editor.Roi : null,
            () => usesFeatureTeachRoi() ? host.Editor.Template?.Roi : null,
            usesFeatureTeachRoi,
            () => usesFeatureTeachRoi() && roi.UseTemplateRoi);
    }

    public void Wire() => _sync.Wire();

    public void Unwire() => _sync.Unwire();

    public void Dispose() => _sync.Dispose();

    public void BeginDetectionRoiDraw()
    {
        _sync.IsTemplateDrawTarget = false;
        _sync.StartDrawAfterGrab = false;
        _sync.StartDrawTemplateAfterGrab = false;
        if (_roi.HasRoiRefFrame)
        {
            _sync.SyncFromRecipe();
            _sync.StartRoiMode();
            return;
        }

        _sync.StartDrawAfterGrab = true;
    }

    public void BeginFeatureRoiDraw()
    {
        if (!_usesFeatureTeachRoi())
            return;
        _sync.IsTemplateDrawTarget = true;
        _sync.StartDrawAfterGrab = false;
        _sync.StartDrawTemplateAfterGrab = false;
        if (_roi.HasRoiRefFrame)
        {
            _roi.EnsureFeatureRoiDrawable();
            _sync.SyncFromRecipe();
            _sync.StartRoiMode();
            return;
        }

        _sync.StartDrawTemplateAfterGrab = true;
    }

    public void SyncFromRecipe() => _sync.SyncFromRecipe();
}
