using RobotVision.Hosting;
using RobotVision.Hosting.Chat;
using Xunit;

namespace RobotVision.Tests;

public sealed class LlamaServerHostTests
{
    [Fact]
    public void BuildArguments_PinsCpuAndDisablesThinking()
    {
        var args = LlamaServerHost.BuildArguments(@"E:\光模块\llm\Qwen3.5-4B-Q4_K_M.gguf", new ChatConfig
        {
            Port = 8080,
            Threads = 8,
            ContextSize = 4096,
        });
        Assert.Contains("--host 127.0.0.1", args);
        Assert.Contains("--port 8080", args);
        Assert.Contains("-c 4096", args);
        Assert.Contains("-t 8", args);
        Assert.Contains("-ngl 0", args);
        Assert.Contains("--parallel 1", args);
        Assert.Contains("--jinja", args);
        Assert.Contains("--no-webui", args);
        Assert.Contains("Qwen3.5-4B-Q4_K_M.gguf", args);
    }

    [Fact]
    public void BuildArguments_DefaultContextIs8192()
    {
        var args = LlamaServerHost.BuildArguments(@"E:\光模块\llm\Qwen3.5-4B-Q4_K_M.gguf", new ChatConfig
        {
            Port = 8080,
            Threads = 8,
        });
        Assert.Contains("-c 8192", args);
    }

    [Fact]
    public void ResolveGguf_ExistingFile_ReturnsFullPath()
    {
        var path = @"E:\光模块\llm\Qwen3.5-4B-Q4_K_M.gguf";
        if (!File.Exists(path))
            return;
        Assert.Equal(Path.GetFullPath(path), LlamaServerHost.ResolveGguf(new ChatConfig { GgufPath = path }));
    }

    [Fact]
    public void ResolveGguf_MissingFile_ReturnsNull()
    {
        var cfg = new ChatConfig { GgufPath = @"Z:\does-not-exist\no.gguf" };
        Assert.Null(LlamaServerHost.ResolveGguf(cfg));
    }
}
