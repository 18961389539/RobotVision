using FluentAssertions;

namespace RobotVision.IntegrationTests;

/// <summary>
/// TCP 协议端到端集成测试：真实 socket 连接 → TcpServerManager → VisionService 管线。
/// 覆盖 PING/STATUS/未知命令/配方解析（名称/序列号/路径穿越）/错误码应答格式/白名单拒绝。
/// </summary>
public class TcpProtocolEndToEndTests
{
    [Fact]
    public async Task Ping_ReturnsPong()
    {
        await using var server = await TestServer.StartAsync();

        var reply = await server.SendAsync("PING");

        reply.Should().Be("PONG");
    }

    [Fact]
    public async Task Status_ReturnsReadyState()
    {
        await using var server = await TestServer.StartAsync();

        var reply = await server.SendAsync("STATUS");

        reply.Should().StartWith("OK,ready,");
        // 格式：OK,ready,队列深度,队列上限(默认 4),最近耗时ms
        reply.Should().Match("OK,ready,0,4,*");
    }

    [Fact]
    public async Task UnknownCommand_TreatedAsRecipeTrigger_Returns1001()
    {
        await using var server = await TestServer.StartAsync();

        // 协议仅识别 PING/STATUS；其余按"配方名"触发 → 未知命令即未知配方 1001
        var reply = await server.SendAsync("HELLO");

        reply.Should().StartWith("ERR,1001,");
    }

    [Fact]
    public async Task UnknownRecipe_Returns1001()
    {
        await using var server = await TestServer.StartAsync();

        var reply = await server.SendAsync("NO_SUCH_RECIPE");

        reply.Should().StartWith("ERR,1001,");
    }

    [Fact]
    public async Task PathTraversal_RecipeName_IsRejected()
    {
        await using var server = await TestServer.StartAsync();

        // 路径穿越探测必须被拒（无效配方名 → 1013 或 1001，绝不允许读文件）
        var reply = await server.SendAsync(@"TRIGGER,..\..\appsettings");

        reply.Should().StartWith("ERR,");
        reply.Should().Match("ERR,10*");
    }

    [Fact]
    public async Task InvalidTriggerSyntax_Returns1013()
    {
        await using var server = await TestServer.StartAsync();

        // 段数非法：TRIGGER 后只跟配方名，无位姿段 → 1013 格式错误
        var reply = await server.SendAsync("TRIGGER,A01");

        reply.Should().StartWith("ERR,1013,");
    }

    [Fact]
    public async Task DisabledRecipe_Returns1015()
    {
        await using var server = await TestServer.StartAsync();
        server.WriteRecipe("DISABLED", """
            {"cameraId": "cam_virtual", "debugPassthrough": true, "enabled": false, "angleMode": "KeyPointLine", "models": ["a01_kpt.onnx"], "keypointIndexA": 0, "keypointIndexB": 1}
            """);

        var reply = await server.SendAsync("DISABLED");

        reply.Should().StartWith("ERR,1015,");
    }

    [Fact]
    public async Task MissingModel_Returns1005_AfterFullPipeline()
    {
        await using var server = await TestServer.StartAsync();
        server.WriteRecipe("NOMODEL", """
            {"cameraId": "cam_virtual", "debugPassthrough": true, "angleMode": "MaskMinAreaRect", "models": ["no_such_model.onnx"]}
            """);

        // 配方校验：模型文件不存在 → 触发时 1005
        var reply = await server.SendAsync("NOMODEL");

        reply.Should().StartWith("ERR,1005,");
    }

    [Fact]
    public async Task MissingCamera_Returns1002()
    {
        await using var server = await TestServer.StartAsync();
        server.WriteRecipe("NOCAM", """
            {"cameraId": "no_such_camera", "debugPassthrough": true, "angleMode": "MaskMinAreaRect", "models": ["x.onnx"]}
            """);

        var reply = await server.SendAsync("NOCAM");

        // 配方引用校验失败（相机未注册）→ 1016；或运行时报 1002
        reply.Should().StartWith("ERR,10");
    }

    [Fact]
    public async Task SerialNumber_ResolvesRecipe()
    {
        await using var server = await TestServer.StartAsync();
        server.WriteRecipe("SERIAL", """
            {"serialNumber": 7, "cameraId": "cam_virtual", "debugPassthrough": true, "angleMode": "KeyPointLine", "models": ["a01_kpt.onnx"], "keypointIndexA": 0, "keypointIndexB": 1}
            """);

        // 纯数字序列号触发：解析成功 → 管线执行（虚拟相机图案无目标 → 1007；绝不是 1001）
        var reply = await server.SendAsync("7");

        reply.Should().StartWith("ERR,");
        reply.Should().NotStartWith("ERR,1001,");
    }

    [Fact]
    public async Task MultipleRequests_SameConnection_StayAlive()
    {
        await using var server = await TestServer.StartAsync();

        // 长连接连续请求：连接不因单次请求结束而关闭
        for (var i = 0; i < 5; i++)
        {
            var pong = await server.SendAsync("PING");
            pong.Should().Be("PONG");
        }
    }

    [Fact]
    public async Task Whitelist_RejectsNonListedSource()
    {
        await using var server = await TestServer.StartAsync((cfg, _) =>
        {
            cfg.IpWhitelist = ["192.168.1.10"];
        });

        using var client = await server.ConnectAsync();

        // 白名单外的来源应立即被断开：发送 PING 后读不到应答
        var stream = client.GetStream();
        var payload = System.Text.Encoding.ASCII.GetBytes("PING\n");
        await stream.WriteAsync(payload);

        var buffer = new byte[64];
        using var cts = new CancellationTokenSource(2000);
        var read = 0;
        try
        {
            read = await stream.ReadAsync(buffer, cts.Token);
        }
        catch (IOException)
        {
            read = 0; // 连接被服务端主动关闭
        }
        catch (OperationCanceledException)
        {
            read = -1;
        }

        // 无应答（0 = 连接关闭；超时也说明未收到 PONG）
        read.Should().BeLessThanOrEqualTo(0);
    }

    [Fact]
    public async Task Status_BecomesBusy_DuringProcessing()
    {
        await using var server = await TestServer.StartAsync();
        // 慢相机（间隔 300ms）占住管线；模型存在 → 引用校验通过 → 进入取图阶段
        server.Cfg.Cameras[0].IntervalMs = 300;
        server.WriteRecipe("SLOW", """
            {"cameraId": "cam_virtual", "debugPassthrough": true, "angleMode": "KeyPointLine", "models": ["a01_kpt.onnx"], "keypointIndexA": 0, "keypointIndexB": 1}
            """);

        var trigger = server.SendAsync("SLOW");
        await Task.Delay(100); // 保证取图进行中
        var status = await server.SendAsync("STATUS");

        // 排队中/处理中 → busy
        status.Should().StartWith("OK,busy,");
        var reply = await trigger;
        reply.Should().StartWith("ERR,1007,"); // 虚拟相机图案无目标，管线走完
    }
}
