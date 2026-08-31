using System.Windows;
using System.Windows.Media;
using Microsoft.Web.WebView2.Wpf;

namespace RobotVision.WpfHost.Shared;

/// <summary>在独立窗口中用 WebView2 预览 HTML（脚本已禁用）。</summary>
public interface IHtmlPreviewService
{
    void Show(string html, string title = "HTML 预览（模型生成内容，脚本已禁用）");
}

public sealed class HtmlPreviewService : IHtmlPreviewService
{
    public void Show(string html, string title = "HTML 预览（模型生成内容，脚本已禁用）")
    {
        if (string.IsNullOrWhiteSpace(html))
            return;
        try
        {
            var view = new WebView2();
            var window = new Window
            {
                Title = title,
                Width = 920,
                Height = 660,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20)),
                Content = view,
            };
            window.Loaded += (_, _) => InitializePreview(view, html, window);
            window.Show();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"网页预览不可用: {ex.Message}", ex);
        }
    }

    private static async void InitializePreview(WebView2 view, string html, Window window)
    {
        try
        {
            await view.EnsureCoreWebView2Async();
            view.CoreWebView2.Settings.IsScriptEnabled = false;
            view.NavigateToString(html);
        }
        catch
        {
            window.Close();
            throw;
        }
    }
}
