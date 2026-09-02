using System.Windows;
using RobotVision.Hosting;
using RobotVision.WpfHost.Features.Recipe;

namespace RobotVision.WpfHost.Shared;

/// <summary>配方相关模态窗口（ViewModel 不直接 new Window）。</summary>
public interface IRecipeWindowService
{
    bool ShowSetupWizard(RecipeWorkspaceContext workspace);

    bool ShowRefineDetails(RecipeViewModel host, out bool requestTemplateRoiDraw);
}

public sealed class RecipeWindowService(
    ICameraRuntime cameras,
    IModelRuntime models,
    ICalibrationRuntime calibration,
    ILightingRuntime lighting,
    IRecipeSetupAnalysisService analysis) : IRecipeWindowService
{
    public bool ShowSetupWizard(RecipeWorkspaceContext workspace)
    {
        using var wizard = new RecipeSetupWizardViewModel(
            workspace.Host, cameras, models, calibration, lighting, analysis, workspace.Roi, workspace.Test);
        using var window = new RecipeSetupWizardWindow
        {
            Owner = Application.Current?.MainWindow,
            DataContext = wizard,
        };
        window.ShowDialog();
        return wizard.Applied;
    }

    public bool ShowRefineDetails(RecipeViewModel host, out bool requestTemplateRoiDraw)
    {
        var dialogVm = new RefineMethodDetailsViewModel(host);
        var window = new RefineMethodDetailsWindow
        {
            Owner = Application.Current?.MainWindow,
            DataContext = dialogVm,
        };
        window.ShowDialog();
        requestTemplateRoiDraw = dialogVm.RequestTemplateRoiDrawAfterClose;
        return dialogVm.AcceptedByUser;
    }
}
