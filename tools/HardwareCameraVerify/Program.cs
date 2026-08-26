using OpenCvSharp;
using RobotVision.Infrastructure;
using RobotVision.Infrastructure.Cameras;

var outDir = Path.Combine(Path.GetTempPath(), "RobotVision-camera-test");
Directory.CreateDirectory(outDir);

Console.WriteLine("[0] GigE 发现 + FORCEIP（对齐到网卡网段）");
var discovered = GigEVisionCamera.DiscoverCameras();
try
{
    discovered = GigEForceIp.EnsureAllReachable(discovered);
    foreach (var cam in discovered)
    {
        var mac = cam.MacAddress is { Length: >= 6 } m
            ? string.Join("-", m.Take(6).Select(b => b.ToString("X2")))
            : "(no mac)";
        Console.WriteLine($"  SN={cam.SerialNumber} IP={cam.IpAddress} MAC={mac} reachable={GigEForceIp.IsOnLocalFixedSubnet(cam.IpAddress)}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"  发现失败: {ex}");
    discovered = [];
}
Console.WriteLine();

Console.WriteLine("[1] GigE Vision 枚举");
var gigeDevices = GigEVisionCamera.EnumerateDevices();
foreach (var d in gigeDevices)
    Console.WriteLine($"  发现: {d}");
Console.WriteLine(gigeDevices.Count == 0 ? "  (无设备)\n" : "\n");

Console.WriteLine("[2] Basler pylon 枚举");
IReadOnlyList<string> pylonDevices;
try
{
    pylonDevices = BaslerCamera.EnumerateDevices();
    foreach (var d in pylonDevices)
        Console.WriteLine($"  发现: {d}");
    if (pylonDevices.Count == 0)
        Console.WriteLine("  (无设备 — 可能未安装 pylon 运行库或枚举异常被吞)");
}
catch (Exception ex)
{
    Console.WriteLine($"  枚举异常: {ex.GetType().Name}: {ex.Message}");
    pylonDevices = [];
}
Console.WriteLine();

// 与 WPF Type=Basler 一致：先 pylon 采图。GigEVision.Net 若先占 CCP，pylon 会报 exclusive。
var pylonSns = pylonDevices.Select(l => l.Split('|')[0].Trim()).Where(s => s.Length > 0).ToList();
if (pylonSns.Count == 0)
{
    pylonSns = discovered.Select(c => c.SerialNumber).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
    if (pylonSns.Count == 0)
        pylonSns = gigeDevices.Select(l => l.Split('|')[0].Trim()).Where(s => s.Length > 0).Distinct().ToList();
}

var pylonOk = 0;
Console.WriteLine("[3] pylon / BaslerCamera 采图（WPF 生产路径）");
foreach (var sn in pylonSns.Distinct())
{
    Console.WriteLine($"  SN={sn}");
    try
    {
        using var cam = new BaslerCamera($"verify_pylon_{sn}", sn, grabTimeoutMs: 15000);
        using var frame = cam.Grab();
        Console.WriteLine($"  已连接: SN={cam.SerialNumber} Name={cam.FriendlyName}");
        using var mat = VisionImageCv.AsMat(frame.Image);
        var path = Path.Combine(outDir, $"pylon_{sn}.png");
        Cv2.ImWrite(path, mat);
        Console.WriteLine($"  采图: {frame.Image.Width}x{frame.Image.Height}");
        Console.WriteLine($"  已保存: {path}\n");
        pylonOk++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  失败: {ex.GetType().Name}: {ex.Message}");
        if (ex.InnerException is not null)
            Console.WriteLine($"  内部: {ex.InnerException.Message}");
        Console.WriteLine();
    }
}

if (pylonSns.Count == 0)
    Console.WriteLine("  (没有可测的序列号)\n");

// CameraManager 路径：模拟 WPF cam_basler（DeviceId 留空则绑定第一台）
var managerId = pylonSns.FirstOrDefault() ?? discovered.FirstOrDefault()?.SerialNumber;
if (!string.IsNullOrWhiteSpace(managerId))
{
    Console.WriteLine($"[4] CameraManager + BaslerCamera (DeviceId={managerId})");
    try
    {
        using var mgr = new CameraManager();
        using var cam = new BaslerCamera("cam_basler", managerId, grabTimeoutMs: 15000);
        mgr.Register(cam);
        using var frame = mgr.Grab("cam_basler");
        using var mat = VisionImageCv.AsMat(frame.Image);
        var path = Path.Combine(outDir, "cam_basler.png");
        Cv2.ImWrite(path, mat);
        Console.WriteLine($"  采图: {frame.Image.Width}x{frame.Image.Height}");
        Console.WriteLine($"  已保存: {path}\n");
        pylonOk++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  失败: {ex.Message}\n");
    }
}

Console.WriteLine($"完成。pylon 成功次数={pylonOk} 图片目录: {outDir}");
return pylonOk > 0 ? 0 : 1;
