using Basler.Pylon;
using OpenCvSharp;
using System.Diagnostics;
using RobotVision.Infrastructure.Cameras;

if (args.Length > 0 && args[0] == "--bench")
{
    var rounds = 5;
    if (args.Length > 1 && int.TryParse(args[1], out var n) && n > 0)
        rounds = n;

    foreach (var info in CameraFinder.Enumerate())
    {
        var sn = info[CameraInfoKey.SerialNumber]!;
        Console.WriteLine($"\n=== SN {sn} | {info[CameraInfoKey.FriendlyName]} ===");

        // 1) 裸 pylon：连接 + 多次 GrabOne
        using (var cam = new Camera(sn))
        {
            var swConnect = Stopwatch.StartNew();
            cam.Open();
            cam.Parameters[PLCamera.TriggerMode].TrySetValue(PLCamera.TriggerMode.Off);
            cam.Parameters[PLCamera.ExposureAuto].TrySetValue(PLCamera.ExposureAuto.Off);
            cam.Parameters[PLCamera.GainAuto].TrySetValue(PLCamera.GainAuto.Off);
            swConnect.Stop();

            var w = cam.Parameters[PLCamera.Width].GetValue();
            var h = cam.Parameters[PLCamera.Height].GetValue();
            var expUs = TryReadFloat(cam, PLCamera.ExposureTime)
                ?? TryReadFloat(cam, PLCamera.ExposureTimeAbs);
            var frameRate = TryReadFloat(cam, PLCamera.ResultingFrameRate);
            Console.WriteLine($"  ROI: {w}x{h}  ExposureTime={expUs?.ToString() ?? "n/a"}us  ResultingFrameRate={frameRate?.ToString("F2") ?? "n/a"}fps  连接耗时: {swConnect.ElapsedMilliseconds}ms");

            var grabMs = new List<long>();
            for (var i = 0; i < rounds; i++)
            {
                var sw = Stopwatch.StartNew();
                using var r = cam.StreamGrabber.GrabOne(30000, TimeoutHandling.ThrowException);
                sw.Stop();
                grabMs.Add(sw.ElapsedMilliseconds);
                Console.WriteLine($"  pylon GrabOne #{i + 1}: {sw.ElapsedMilliseconds}ms -> {r.Width}x{r.Height}");
            }
            Console.WriteLine($"  pylon GrabOne 平均: {grabMs.Average():F1}ms  最小: {grabMs.Min()}ms  最大: {grabMs.Max()}ms");

            // 分解：GrabOne vs BGR 转换
            var swGrab = Stopwatch.StartNew();
            using var r2 = cam.StreamGrabber.GrabOne(30000, TimeoutHandling.ThrowException);
            swGrab.Stop();
            var conv = new PixelDataConverter { OutputPixelFormat = PixelType.BGR8packed };
            var packed = new byte[r2.Width * r2.Height * 3];
            var swConv = Stopwatch.StartNew();
            unsafe
            {
                fixed (byte* p = packed)
                    conv.Convert((nint)p, packed.Length, r2);
            }
            swConv.Stop();
            conv.Dispose();
            Console.WriteLine($"  分解: GrabOne={swGrab.ElapsedMilliseconds}ms  BGR转换={swConv.ElapsedMilliseconds}ms  (合计约 {swGrab.ElapsedMilliseconds + swConv.ElapsedMilliseconds}ms)");
        }

        // 2) BaslerCamera 生产路径
        using var basler = new BaslerCamera($"bench_{sn}", sn, grabTimeoutMs: 30000);
        var swFirst = Stopwatch.StartNew();
        int firstW, firstH;
        using (var frame1 = basler.Grab())
        {
            firstW = frame1.Image.Width;
            firstH = frame1.Image.Height;
        }
        swFirst.Stop();
        Console.WriteLine($"  BaslerCamera 首次 Grab（含连接）: {swFirst.ElapsedMilliseconds}ms -> {firstW}x{firstH}");

        var prodMs = new List<long>();
        for (var i = 0; i < rounds; i++)
        {
            var sw = Stopwatch.StartNew();
            using var frame = basler.Grab();
            sw.Stop();
            prodMs.Add(sw.ElapsedMilliseconds);
            Console.WriteLine($"  BaslerCamera Grab #{i + 1}: {sw.ElapsedMilliseconds}ms");
        }
        Console.WriteLine($"  BaslerCamera Grab 平均（已连接）: {prodMs.Average():F1}ms  最小: {prodMs.Min()}ms  最大: {prodMs.Max()}ms");
    }
    return 0;
}

// 从本机所有 Basler 相机各采一帧，保存到 data/captures/
var outDir = Path.GetFullPath(
    args.Length > 0 ? args[0] :
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "captures"));
Directory.CreateDirectory(outDir);

var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
var devices = CameraFinder.Enumerate()
    .Select(i => i[CameraInfoKey.SerialNumber] + " | " + i[CameraInfoKey.FriendlyName])
    .ToList();
if (devices.Count == 0)
{
    Console.WriteLine("未发现 Basler 相机（检查 pylon 运行库与网线）");
    return 1;
}

var saved = 0;
foreach (var line in devices)
{
    var sn = line.Split('|')[0].Trim();
    Console.WriteLine($"采集 {sn} ...");
    try
    {
        if (!TryCapture(sn, fullSensor: true, out var mat, out var note))
        {
            Console.WriteLine($"  全幅失败，降分辨率重试 ...");
            if (!TryCapture(sn, fullSensor: false, out mat, out note))
            {
                Console.WriteLine($"  失败: {note}");
                continue;
            }
        }
        using (mat)
        {
            var path = Path.Combine(outDir, $"{sn}_{stamp}.png");
            Cv2.ImWrite(path, mat);
            Console.WriteLine($"  已保存: {path} ({mat.Width}x{mat.Height}) [{note}]");
            saved++;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  异常: {ex.Message}");
    }
}

Console.WriteLine(saved > 0
    ? $"\n完成：{saved} 张，目录 {outDir}"
    : "\n未保存任何图片");
return saved > 0 ? 0 : 1;

static bool TryCapture(string serial, bool fullSensor, out Mat mat, out string note)
{
    mat = null!;
    note = "";
    using var cam = new Camera(serial);
    cam.Open();
    cam.Parameters[PLCamera.TriggerSelector].TrySetValue(PLCamera.TriggerSelector.FrameStart);
    cam.Parameters[PLCamera.TriggerMode].TrySetValue(PLCamera.TriggerMode.Off);
    cam.Parameters[PLCamera.ExposureAuto].TrySetValue(PLCamera.ExposureAuto.Off);
    cam.Parameters[PLCamera.GainAuto].TrySetValue(PLCamera.GainAuto.Off);
    if (cam.Parameters[PLCamera.ExposureTime].IsWritable)
        cam.Parameters[PLCamera.ExposureTime].SetValue(8000);
    if (cam.Parameters[PLCamera.GevSCPSPacketSize].IsWritable)
        cam.Parameters[PLCamera.GevSCPSPacketSize].SetValue(1500);

    if (!fullSensor)
    {
        if (cam.Parameters[PLCamera.OffsetX].IsWritable)
            cam.Parameters[PLCamera.OffsetX].SetValue(0);
        if (cam.Parameters[PLCamera.OffsetY].IsWritable)
            cam.Parameters[PLCamera.OffsetY].SetValue(0);
        if (cam.Parameters[PLCamera.Width].IsWritable)
            cam.Parameters[PLCamera.Width].SetValue(1280);
        if (cam.Parameters[PLCamera.Height].IsWritable)
            cam.Parameters[PLCamera.Height].SetValue(960);
        note = "1280x960";
    }
    else
    {
        note = "full sensor";
    }

    using var result = cam.StreamGrabber.GrabOne(15000, TimeoutHandling.Return);
    if (result is null || !result.GrabSucceeded)
    {
        note = result?.ErrorDescription ?? "无结果";
        return false;
    }

    mat = ToBgrMat(result);
    note += $" {result.Width}x{result.Height}";
    return true;
}

static double? TryReadFloat(Camera cam, FloatName name)
{
    try
    {
        var p = cam.Parameters[name];
        return p.IsEmpty || !p.IsReadable ? null : p.GetValue();
    }
    catch { return null; }
}

static Mat ToBgrMat(IGrabResult result)
{
    var conv = new PixelDataConverter { OutputPixelFormat = PixelType.BGR8packed };
    var mat = new Mat(result.Height, result.Width, MatType.CV_8UC3);
    try
    {
        var stride = result.Width * 3;
        if (stride % 4 == 0)
        {
            conv.Convert(mat.Data, mat.Total() * mat.ElemSize(), result);
            return mat;
        }

        var packed = new byte[result.Height * stride];
        unsafe
        {
            fixed (byte* src = packed)
            {
                conv.Convert((nint)src, packed.Length, result);
                var dst = (byte*)mat.Data;
                var step = (nint)mat.Step();
                for (var r = 0; r < result.Height; r++)
                    Buffer.MemoryCopy(src + r * stride, dst + r * step, stride, stride);
            }
        }
        return mat;
    }
    catch
    {
        mat.Dispose();
        throw;
    }
    finally
    {
        conv.Dispose();
    }
}
