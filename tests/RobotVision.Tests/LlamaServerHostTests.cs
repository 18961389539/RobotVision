using RobotVision.Hosting;
using RobotVision.Hosting.Chat;
using Xunit;

namespace RobotVision.Tests;

public sealed class LlamaServerHostTests
{
    private const string SampleGgufName = "Qwen3.5-4B-Q4_K_M.gguf";

    [Fact]
    public void BuildArguments_PinsCpuAndDisablesThinking()
    {
        var args = LlamaServerHost.BuildArguments(SampleGgufName, new ChatConfig
        {
            Port = 8080,
            Threads = 8,
            ContextSize = 4096,
        });
        Assert.Contains("--host 127.0.0.1", args, StringComparison.Ordinal);
        Assert.Contains("--port 8080", args, StringComparison.Ordinal);
        Assert.Contains("-c 4096", args, StringComparison.Ordinal);
        Assert.Contains("-t 8", args, StringComparison.Ordinal);
        Assert.Contains("-ngl 0", args, StringComparison.Ordinal);
        Assert.Contains("--parallel 1", args, StringComparison.Ordinal);
        Assert.Contains("--jinja", args, StringComparison.Ordinal);
        Assert.Contains("--no-webui", args, StringComparison.Ordinal);
        Assert.Contains(SampleGgufName, args, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildArguments_DefaultContextIs8192()
    {
        var args = LlamaServerHost.BuildArguments(SampleGgufName, new ChatConfig
        {
            Port = 8080,
            Threads = 8,
        });
        Assert.Contains("-c 8192", args, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveGguf_ExistingFile_ReturnsFullPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "rv_llama_" + Guid.NewGuid().ToString("N") + ".gguf");
        try
        {
            File.WriteAllText(path, "fake");
            Assert.Equal(Path.GetFullPath(path), LlamaServerHost.ResolveGguf(new ChatConfig { GgufPath = path }));
        }
        finally
        {
            try { File.Delete(path); } catch (IOException) { }
        }
    }

    [Fact]
    public void ResolveGguf_MissingFile_ReturnsNull()
    {
        var cfg = new ChatConfig { GgufPath = @"Z:\does-not-exist\no.gguf" };
        Assert.Null(LlamaServerHost.ResolveGguf(cfg));
    }
}
