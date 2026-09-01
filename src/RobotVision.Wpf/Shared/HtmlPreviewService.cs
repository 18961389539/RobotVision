using System.Diagnostics;
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
            // 丢弃 Task 而非 async void：异常在 InitializePreviewAsync 内部全捕获，
            // 不会直冲同步上下文导致进程崩溃，也不会变成不可观察的静默失败。
            window.Loaded += (_, _) => _ = InitializePreviewAsync(view, html, window);
            window.Show();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"网页预览不可用: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 初始化 WebView2 并注入 HTML。失败时关闭窗口并记录原因，不向上抛：
    /// 调用方是 Loaded 事件处理器（fire-and-forget），异常无处可catch，
    /// 抛出会导致进程级崩溃（async void 的典型危害）。
    /// </summary>
    private static async Task InitializePreviewAsync(WebView2 view, string html, Window window)
    {
        try
        {
            await view.EnsureCoreWebView2Async();
            view.CoreWebView2.Settings.IsScriptEnabled = false;
            view.NavigateToString(html);
        }
        catch (Exception ex)
        {
            Trace.TraceError("[HtmlPreview] WebView2 初始化失败，已关闭预览窗口: {0}", ex);
            try
            {
                if (window.Dispatcher.CheckAccess())
                    window.Close();
                else
                    await window.Dispatcher.InvokeAsync(window.Close);
            }
            catch (Exception closeEx)
            {
                Trace.TraceWarning("[HtmlPreview] 关闭预览窗口失败: {0}", closeEx.Message);
            }
        }
    }
}
