using FluentAssertions;

namespace RobotVision.IntegrationTests;

/// <summary>
/// 并发与压力集成测试：多连接同时触发、排队语义（Busy 拒绝）、
/// 慢相机下的管线占用与状态机一致性。
/// </summary>
public class ConcurrencyIntegrationTests
{
    [Fact]
    public async Task ConcurrentTriggers_AllGetReplies_WithoutCrash()
    {
        await using var server = await TestServer.StartAsync();
        server.WriteRecipe("SLOW", """
            {"cameraId": "cam_virtual", "angleMode": "MaskMinAreaRect", "models": ["no_such.onnx"], "intervalMs": 50}
            """);
        // 相机间隔缩短：并发下每请求都走完管线（模型缺失 1005）
        server.Cfg.Cameras[0].IntervalMs = 50;

        var tasks = Enumerable.Range(0, 6).Select(_ => server.SendAsync("SLOW")).ToArray();
        var replies = await Task.WhenAll(tasks);

        replies.Should().OnlyContain(r => r.StartsWith("ERR,10"));
        server.Vision.Health.Total.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SlowPipeline_ConcurrentTriggers_QueueOrRejectButNeverDeadlock()
    {
        await using var server = await TestServer.StartAsync();
        server.Cfg.Cameras[0].IntervalMs = 200; // 慢相机：每请求 ≥200ms
        server.WriteRecipe("SLOW", """
            {"cameraId": "cam_virtual", "angleMode": "MaskMinAreaRect", "models": ["no_such.onnx"]}
            """);

        var tasks = Enumerable.Range(0, 8)
            .Select(_ => server.SendAsync("SLOW", timeoutMs: 15000))
            .ToArray();
        var replies = await Task.WhenAll(tasks);

        // 所有请求都有最终应答（ERR: 1005 走完 / 1009 busy / 1008/1010 超时）
        replies.Should().OnlyContain(r => r.StartsWith("ERR,1"));
        // 管线存活：随后 PING 仍正常
        (await server.SendAsync("PING")).Should().Be("PONG");
    }

    [Fact]
    public async Task Status_And_Trigger_Interleaved_AreConsistent()
    {
        await using var server = await TestServer.StartAsync();
        server.WriteRecipe("SLOW", """
            {"cameraId": "cam_virtual", "angleMode": "MaskMinAreaRect", "models": ["no_such.onnx"]}
            """);

        for (var i = 0; i < 3; i++)
        {
            var status = await server.SendAsync("STATUS");
            status.Should().StartWith("OK,");

            var trigger = await server.SendAsync("SLOW");
            trigger.Should().StartWith("ERR,1005,");
        }

        // 最终恢复 ready
        var final = await server.SendAsync("STATUS");
        final.Should().StartWith("OK,ready,");
    }

    [Fact]
    public async Task ManyPings_UnderLoad_AlwaysPong()
    {
        await using var server = await TestServer.StartAsync();

        var tasks = Enumerable.Range(0, 20).Select(_ => server.SendAsync("PING")).ToArray();
        var replies = await Task.WhenAll(tasks);

        replies.Should().OnlyContain(r => r == "PONG");
    }
}
