using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Chat;

public partial class ChatPage : Page
{
    private readonly ChatViewModel _vm;
    private bool _webviewReady;

    public ChatPage(ChatViewModel viewModel)
    {
        _vm = viewModel;
        ViewModelPageLifetime.Attach(this, viewModel, onUnloading: DisposeWebView);
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm.UiReset -= OnUiReset;
        _vm.UiReset += OnUiReset;
        _ = InitializePageAsync();
    }

    private async Task InitializePageAsync()
    {
        await _vm.EnsureChatUiAsync();
        _vm.ScheduleProbe();
        if (_vm.HasChatUi)
            await InitializeWebViewAsync();
    }

    private async Task InitializeWebViewAsync()
    {
        if (_webviewReady)
        {
            ChatView.CoreWebView2?.Navigate(_vm.ChatUiUrl);
            return;
        }

        try
        {
            var userData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RobotVision", "webview2-chat");
            Directory.CreateDirectory(userData);
            var env = await CoreWebView2Environment.CreateAsync(null, userData);
            await ChatView.EnsureCoreWebView2Async(env);
            var core = ChatView.CoreWebView2;
            core.Settings.IsScriptEnabled = true;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreHostObjectsAllowed = false;
            var origin = _vm.ChatUiOrigin.TrimEnd('/');
            if (origin.Length > 0)
            {
                core.AddWebResourceRequestedFilter($"{origin}/*", CoreWebView2WebResourceContext.All);
                core.WebResourceRequested += (_, args) =>
                {
                    if (_vm.ChatUiToken.Length > 0)
                        args.Request.Headers.SetHeader("X-RobotVision-Token", _vm.ChatUiToken);
                };
            }
            core.Navigate(_vm.ChatUiUrl);
            _webviewReady = true;
        }
        catch (Exception ex)
        {
            _vm.Status = $"工艺助手界面不可用: {ex.Message}";
        }
    }

    private void OnUiReset()
    {
        if (_webviewReady)
            ChatView.CoreWebView2?.Reload();
    }

    private void DisposeWebView()
    {
        _vm.UiReset -= OnUiReset;
        try
        {
            ChatView.Dispose();
        }
        catch
        {
            // 页面离开时尽力释放 WebView2 进程
        }
    }
}
