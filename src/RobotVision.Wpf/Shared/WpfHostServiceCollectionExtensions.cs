using Microsoft.Extensions.DependencyInjection;
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
using Wpf.Ui;
using AboutPage = RobotVision.WpfHost.Features.About.AboutPage;

namespace RobotVision.WpfHost.Shared;

internal static class WpfHostServiceCollectionExtensions
{
    /// <summary>注册导航页、页面 ViewModel（Transient）与壳层 <see cref="MainViewModel"/>（Singleton）。</summary>
    public static IServiceCollection AddWpfNavigation(this IServiceCollection services)
    {
        services.AddSingleton<IPageService, DiPageService>();
        services.AddSingleton<MainViewModel>();

        services.AddTransient<RecipeViewModel>();
        services.AddTransient<CamerasViewModel>();
        services.AddTransient<LightingsViewModel>();
        services.AddTransient<ModelsViewModel>();
        services.AddTransient<CalibrationViewModel>();
        services.AddTransient<CalibrationWizardViewModel>();
        services.AddTransient<FailuresViewModel>();
        services.AddTransient<AnalysisViewModel>();
        services.AddTransient<CommunicationViewModel>();
        services.AddTransient<ChatViewModel>();
        services.AddTransient<LogsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<SystemViewModel>();

        services.AddTransient<MonitorPage>();
        services.AddTransient<CamerasPage>();
        services.AddTransient<LightingsPage>();
        services.AddTransient<RecipePage>();
        services.AddTransient<ModelsPage>();
        services.AddTransient<CalibrationPage>();
        services.AddTransient<CalibrationWizardPage>();
        services.AddTransient<FailuresPage>();
        services.AddTransient<AnalysisPage>();
        services.AddTransient<CommunicationPage>();
        services.AddTransient<ChatPage>();
        services.AddTransient<LogsPage>();
        services.AddTransient<SettingsPage>();
        services.AddTransient<SystemPage>();
        services.AddTransient<AboutPage>();

        return services;
    }
}
