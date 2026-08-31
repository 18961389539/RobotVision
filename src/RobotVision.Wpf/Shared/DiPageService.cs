using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using Wpf.Ui;

namespace RobotVision.WpfHost.Shared;

/// <summary>用 DI 容器解析 <see cref="INavigationView"/> 目标页，替代无参构造 + Service Locator。</summary>
public sealed class DiPageService(IServiceProvider services) : IPageService
{
    public T GetPage<T>() where T : class =>
        services.GetRequiredService<T>();

    public FrameworkElement? GetPage(Type pageType) =>
        (FrameworkElement)services.GetRequiredService(pageType);
}
