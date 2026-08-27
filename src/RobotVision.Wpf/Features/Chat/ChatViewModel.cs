using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobotVision.Hosting;
using RobotVision.Hosting.Chat;

namespace RobotVision.WpfHost.Features.Chat;

public sealed partial class ChatBubble : ObservableObject
{
    public ChatBubble(string role, string text)
    {
        Role = role;
        _text = text;
    }

    public string Role { get; }

    public bool IsUser => string.Equals(Role, "user", StringComparison.OrdinalIgnoreCase);

    [ObservableProperty]
    private string _text;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    private string? _imagePath;

    public bool HasImage => !string.IsNullOrEmpty(ImagePath);
}

/// <summary>本机 CPU 对话页：只连接 llama-server，不加载 9GB 权重。</summary>
public partial class ChatViewModel : ObservableObject
{
    public const string TitleText = "站内工艺助手";
    public const string ReadyStatus = "本机模型已就绪。查询走实时数据；改参、删除与停 TCP 须明确指令。";
    public const string EmptyTitle = "本机视觉调试助手";
    public const string EmptyLead = "面向光模块装配引导。先读本机实况，再给结论；不编造产量、坐标与结果码。公开资料可检索网页。";
    public const string EmptyQuery = "站况 — 相机是否可取图、配方与标定档案、TCP 与检测队列";
    public const string EmptyAnalysis = "分析 — 今日合格率、失败码、角度离散、按配方对比（与结果分析页同源）";
    public const string EmptyAction = "调试 / 检索 — 拍照或按配方试跑；公开网页用检索；删除配方/标定、停 TCP 请写明对象";
    public const string InputPlaceholder = "例如：今日合格率、相机能否取图、解除 1018 联锁";

    private readonly ILocalChatClient _client;
    private readonly ChatConfig _cfg;
    private readonly ChatAgent? _agent;
    private CancellationTokenSource? _sendCts;

    public ObservableCollection<ChatBubble> Messages { get; } = [];

    public string EndpointText => OpenAiChatClient.NormalizeEndpoint(_cfg.Endpoint);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _draft = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isReady;

    [ObservableProperty]
    private string _status = "尚未连接本机推理服务。";

    public ChatViewModel(ILocalChatClient client, ChatConfig cfg, ChatAgent? agent = null)
    {
        _client = client;
        _cfg = cfg;
        _agent = agent;
    }

    [RelayCommand]
    public async Task ProbeAsync()
    {
        Status = _cfg.AutoStart
            ? "正在加载本机模型，首次约 1–2 分钟…"
            : $"正在检测 {EndpointText}…";
        try
        {
            var ready = await _client.ProbeAsync();
            IsReady = ready;
            Status = ready
                ? ReadyStatus
                : (_client.LastError
                   ?? "未检测到 llama-server。请确认已下载 GGUF 并配置 Chat.GgufPath。");
        }
        catch (Exception ex)
        {
            IsReady = false;
            Status = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var text = Draft.Trim();
        if (text.Length == 0 || IsBusy)
            return;

        Draft = "";
        Messages.Add(new ChatBubble("user", text));
        var assistant = new ChatBubble("assistant", "");
        Messages.Add(assistant);

        _sendCts?.Cancel();
        _sendCts?.Dispose();
        _sendCts = new CancellationTokenSource();
        var token = _sendCts.Token;
        IsBusy = true;
        Status = IsReady ? "正在生成…" : "正在加载本机模型并生成（首次约 1–2 分钟）…";
        try
        {
            var turns = Messages
                .Where(m => m.Text.Length > 0)
                .Select(m => new ChatTurn(m.Role, m.Text))
                .ToList();
            // 当前助手气泡还是空的，不要当成历史
            if (turns.Count > 0 && turns[^1].Role == "assistant" && turns[^1].Content.Length == 0)
                turns.RemoveAt(turns.Count - 1);

            var any = false;
            if (_agent is not null)
            {
                await foreach (var ev in _agent.RunAsync(turns, token))
                {
                    switch (ev)
                    {
                        case ChatTextDelta delta:
                            any = true;
                            assistant.Text += delta.Text;
                            break;
                        case ChatToolNotice notice:
                            any = true;
                            if (assistant.Text.Length > 0 && !assistant.Text.EndsWith('\n'))
                                assistant.Text += "\n";
                            assistant.Text += $"〔{notice.Name}〕{notice.Detail}\n";
                            break;
                        case ChatImageEvent image:
                            any = true;
                            assistant.ImagePath = image.Path;
                            break;
                    }
                }
            }
            else
            {
                await foreach (var chunk in _client.CompleteStreamAsync(turns, token))
                {
                    any = true;
                    assistant.Text += chunk;
                }
            }

            if (!any && assistant.Text.Length == 0)
                assistant.Text = "（没有返回内容）";
            IsReady = true;
            Status = ReadyStatus;
        }
        catch (OperationCanceledException)
        {
            if (assistant.Text.Length == 0)
                assistant.Text = "（已停止）";
            else
                assistant.Text += "\n（已停止）";
            Status = "已停止生成。";
        }
        catch (Exception ex)
        {
            if (assistant.Text.Length == 0)
                assistant.Text = $"请求失败：{ex.Message}";
            else
                assistant.Text += $"\n请求失败：{ex.Message}";
            IsReady = false;
            Status = $"对话服务不可用（{EndpointText}）。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSend() => !IsBusy && !string.IsNullOrWhiteSpace(Draft);

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {
        _sendCts?.Cancel();
    }

    private bool CanStop() => IsBusy;

    [RelayCommand]
    private void Clear()
    {
        if (IsBusy)
            Stop();
        Messages.Clear();
        Status = IsReady ? ReadyStatus : Status;
    }
}
