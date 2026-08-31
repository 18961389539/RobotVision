using System.Windows;
using Microsoft.Web.WebView2.Wpf;
using RobotVision.Hosting;
using RobotVision.WpfHost.Features.Recipe;

namespace RobotVision.WpfHost.Shared;

/// <summary>配方相关模态窗口（ViewModel 不直接 new Window）。</summary>
public interface IRecipeWindowService
{
    bool ShowSetupWizard(
        IRecipeWorkspace host,
        ICameraRuntime cameras,
        IModelRuntime models,
        ICalibrationRuntime calibration,
        ILightingRuntime lighting,
        RecipeRoiEditor roi,
        RecipeTestSession test);

    bool ShowRefineDetails(RecipeViewModel host, out bool requestTemplateRoiDraw);
}

public sealed class RecipeWindowService : IRecipeWindowService
{
    public bool ShowSetupWizard(
        IRecipeWorkspace host,
        ICameraRuntime cameras,
        IModelRuntime models,
        ICalibrationRuntime calibration,
        ILightingRuntime lighting,
        RecipeRoiEditor roi,
        RecipeTestSession test)
    {
        using var wizard = new RecipeSetupWizardViewModel(host, cameras, models, calibration, lighting, roi, test);
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
