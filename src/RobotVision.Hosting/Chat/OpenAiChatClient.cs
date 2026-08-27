using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RobotVision.Hosting.Chat;

/// <summary>对接 llama-server（/health、/v1/chat/completions 流式，含 tools）。</summary>
public sealed class OpenAiChatClient : ILocalChatClient, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly ChatConfig _cfg;
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    public OpenAiChatClient(ChatConfig cfg)
        : this(cfg, new HttpClient { Timeout = TimeSpan.FromMinutes(5) }, ownsHttp: true)
    {
    }

    internal OpenAiChatClient(ChatConfig cfg, HttpClient http, bool ownsHttp)
    {
        _cfg = cfg;
        _http = http;
        _ownsHttp = ownsHttp;
    }

    public string Endpoint => NormalizeEndpoint(_cfg.Endpoint);

    public string? LastError => null;

    public async Task<bool> ProbeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            using var response = await _http.GetAsync(Endpoint + "/health", cts.Token).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
                return true;
            using var models = await _http.GetAsync(Endpoint + "/v1/models", cts.Token).ConfigureAwait(false);
            return models.IsSuccessStatusCode;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    public async IAsyncEnumerable<string> CompleteStreamAsync(
        IReadOnlyList<ChatTurn> turns,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messages = turns.Select(t => new ChatApiMessage(t.Role, t.Content)).ToList();
        await foreach (var part in CompletePartsAsync(messages, tools: null, cancellationToken).ConfigureAwait(false))
        {
            if (!string.IsNullOrEmpty(part.Content))
                yield return part.Content;
        }
    }

    public async IAsyncEnumerable<ChatStreamPart> CompletePartsAsync(
        IReadOnlyList<ChatApiMessage> messages,
        IReadOnlyList<ChatToolSpec>? tools,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var dtoMessages = new List<ChatMessageDto>(messages.Count + 1);
        dtoMessages.Add(new ChatMessageDto("system", ChatSystemPrompt.Resolve(_cfg.SystemPrompt)));
        foreach (var msg in messages)
        {
            IReadOnlyList<ToolCallDto>? calls = null;
            if (msg.ToolCalls is { Count: > 0 })
            {
                calls = msg.ToolCalls
                    .Select(c => new ToolCallDto(c.Id, "function", new ToolFnDto(c.Name, c.Arguments)))
                    .ToList();
            }
            dtoMessages.Add(new ChatMessageDto(msg.Role, msg.Content, msg.ToolCallId, msg.Name, calls));
        }

        IReadOnlyList<ToolSpecDto>? toolDtos = null;
        string? toolChoice = null;
        if (tools is { Count: > 0 })
        {
            toolDtos = tools.Select(t => new ToolSpecDto("function", new ToolFnSpecDto(t.Name, t.Description, t.Parameters))).ToList();
            toolChoice = "auto";
        }

        var body = new ChatRequestDto(
            string.IsNullOrWhiteSpace(_cfg.Model) ? "qwen" : _cfg.Model,
            dtoMessages,
            Stream: true,
            MaxTokens: Math.Clamp(_cfg.MaxTokens, 16, 4096),
            ChatTemplateKwargs: new ChatTemplateKwargs(EnableThinking: false),
            Tools: toolDtos,
            ToolChoice: toolChoice);
        var json = JsonSerializer.Serialize(body, JsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint + "/v1/chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await _http.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"对话服务返回 {(int)response.StatusCode}：{TrimError(err)}");
        }

        var acc = new Dictionary<int, ToolAcc>();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            var parsed = ChatSseParser.TryParse(line);
            if (!parsed.Ok)
                continue;
            if (parsed.Finished)
                break;
            if (!string.IsNullOrEmpty(parsed.Content))
                yield return new ChatStreamPart(parsed.Content, null, false);
            if (parsed.ToolDeltas is { Count: > 0 })
            {
                foreach (var d in parsed.ToolDeltas)
                    Merge(acc, d);
            }
        }

        if (acc.Count > 0)
        {
            var calls = acc.OrderBy(kv => kv.Key)
                .Select(kv => kv.Value.ToCall())
                .ToList();
            yield return new ChatStreamPart(null, calls, false);
        }
    }

    public void Dispose()
    {
        if (_ownsHttp)
            _http.Dispose();
    }

    public static string NormalizeEndpoint(string? endpoint)
    {
        var value = (endpoint ?? "").Trim().TrimEnd('/');
        return string.IsNullOrEmpty(value) ? "http://127.0.0.1:8080" : value;
    }

    private static void Merge(Dictionary<int, ToolAcc> acc, SseToolCallDelta d)
    {
        if (!acc.TryGetValue(d.Index, out var cur))
        {
            cur = new ToolAcc();
            acc[d.Index] = cur;
        }
        if (!string.IsNullOrEmpty(d.Id))
            cur.Id = d.Id;
        if (!string.IsNullOrEmpty(d.Name))
            cur.Name = d.Name;
        if (d.Arguments is not null)
            cur.Arguments.Append(d.Arguments);
    }

    private static string TrimError(string body)
    {
        var t = body.Trim().ReplaceLineEndings(" ");
        return t.Length <= 240 ? t : t[..240] + "…";
    }

    private sealed class ToolAcc
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public StringBuilder Arguments { get; } = new();

        public ChatToolCall ToCall() => new(
            string.IsNullOrEmpty(Id) ? "call_" + Name : Id,
            Name,
            Arguments.Length == 0 ? "{}" : Arguments.ToString());
    }

    private sealed record ChatRequestDto(
        string Model,
        IReadOnlyList<ChatMessageDto> Messages,
        bool Stream,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("chat_template_kwargs")] ChatTemplateKwargs ChatTemplateKwargs,
        IReadOnlyList<ToolSpecDto>? Tools = null,
        [property: JsonPropertyName("tool_choice")] string? ToolChoice = null);

    private sealed record ChatTemplateKwargs(
        [property: JsonPropertyName("enable_thinking")] bool EnableThinking);

    private sealed record ChatMessageDto(
        string Role,
        string? Content,
        [property: JsonPropertyName("tool_call_id")] string? ToolCallId = null,
        string? Name = null,
        [property: JsonPropertyName("tool_calls")] IReadOnlyList<ToolCallDto>? ToolCalls = null);

    private sealed record ToolCallDto(string Id, string Type, ToolFnDto Function);

    private sealed record ToolFnDto(string Name, string Arguments);

    private sealed record ToolSpecDto(string Type, ToolFnSpecDto Function);

    private sealed record ToolFnSpecDto(string Name, string Description, JsonElement Parameters);
}
