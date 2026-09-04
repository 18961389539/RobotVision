using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using GenICam.Net.GigEVision.Gvcp;

namespace RobotVision.Infrastructure.Cameras;

/// <summary>
/// 开源 GigE Vision 相机的**发现与设备选择**（GVCP broadcast），与"已连接会话的采集/参数"无关。
/// 从 <see cref="GigEVisionCamera"/> 下沉：适配器只管 connect/grab/参数，网卡发现/IPv4 解析/
/// 序列号·IP·MAC·用户自定义名匹配等一次性静态逻辑集中在此，可独立复用（Basler 的 FORCEIP 对齐也调用它）。
/// </summary>
internal static class GigECameraDiscovery
{
    public static readonly TimeSpan DiscoverTimeout = TimeSpan.FromSeconds(3);

    /// <summary>网口广播发现（含不同网段的 APIPA 相机）。</summary>
    public static IReadOnlyList<GigECameraInfo> DiscoverCameras() => Discover(DiscoverTimeout);

    /// <summary>枚举网口可见的 GigE Vision 相机。失败返回空列表，不抛异常。</summary>
    public static IReadOnlyList<string> EnumerateDevices()
    {
        try
        {
            return Discover(DiscoverTimeout)
                .Select(FormatDevice)
                .ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>deviceId 是点分 IPv4 则解析出来（供连接前判断是否已在固定网段）。</summary>
    public static bool TryParseIpv4(string deviceId, out IPAddress ip) =>
        IPAddress.TryParse(deviceId, out ip!) && ip.AddressFamily == AddressFamily.InterNetwork;

    /// <summary>按序列号/IP/用户自定义名/MAC 匹配目标相机；deviceId 空且仅一台时绑定它。</summary>
    public static GigECameraInfo? SelectDevice(IReadOnlyList<GigECameraInfo> cameras, string deviceId) =>
        CameraDeviceSelection.Resolve(cameras, deviceId, static (camera, needle) =>
            string.Equals(camera.SerialNumber, needle, StringComparison.OrdinalIgnoreCase)
            || string.Equals(camera.IpAddress.ToString(), needle, StringComparison.OrdinalIgnoreCase)
            || string.Equals(camera.UserDefinedName, needle, StringComparison.OrdinalIgnoreCase)
            || MacMatches(camera.MacAddress, needle));

    public static string FormatDevice(GigECameraInfo info)
    {
        var id = string.IsNullOrWhiteSpace(info.SerialNumber)
            ? info.IpAddress.ToString()
            : info.SerialNumber;
        var name = string.Join(" ", new[] { info.ManufacturerName, info.ModelName }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        if (name.Length == 0)
            name = info.UserDefinedName;
        return $"{id} | {info.IpAddress} | {name}";
    }

    private static IReadOnlyList<GigECameraInfo> Discover(TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout + TimeSpan.FromSeconds(2));
        return DiscoverOnAllInterfacesAsync(timeout, cts.Token).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    /// <summary>
    /// 在每个本机 IPv4 网卡上绑定后广播发现。默认 255.255.255.255 会走默认路由（常为 Wi-Fi），
    /// 相机所在的 USB 网口（如 169.254.x）收不到包。
    /// </summary>
    private static async Task<IReadOnlyList<GigECameraInfo>> DiscoverOnAllInterfacesAsync(
        TimeSpan timeout, CancellationToken ct)
    {
        var locals = LocalIpv4Addresses();
        if (locals.Count == 0)
            locals.Add(IPAddress.Any);

        var tasks = locals.Select(local => DiscoverOnInterfaceAsync(local, timeout, ct));
        var batches = await Task.WhenAll(tasks);
        var found = new Dictionary<string, GigECameraInfo>(StringComparer.Ordinal);
        foreach (var camera in batches.SelectMany(b => b))
            found.TryAdd(camera.IpAddress.ToString(), camera);
        return found.Values.ToList();
    }

    private static async Task<IReadOnlyList<GigECameraInfo>> DiscoverOnInterfaceAsync(
        IPAddress local, TimeSpan timeout, CancellationToken ct)
    {
        try
        {
            using var client = new UdpClient(new IPEndPoint(local, 0));
            using var transport = new UdpTransportAdapter(client);
            using var discovery = new GigEDiscovery(transport);
            return await discovery.DiscoverAsync((int)timeout.TotalMilliseconds, cancellationToken: ct);
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static List<IPAddress> LocalIpv4Addresses()
    {
        var list = new List<IPAddress>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
                continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;
            foreach (var ua in nic.GetIPProperties().UnicastAddresses)
            {
                if (ua.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ua.Address))
                    list.Add(ua.Address);
            }
        }
        return list;
    }

    private static bool MacMatches(byte[] mac, string deviceId)
    {
        if (mac.Length < 6)
            return false;
        var dashed = FormatMac(mac);
        var compact = dashed.Replace("-", "", StringComparison.Ordinal);
        var needle = deviceId.Replace(":", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal);
        return string.Equals(dashed, deviceId, StringComparison.OrdinalIgnoreCase)
               || string.Equals(compact, needle, StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatMac(byte[] mac) =>
        mac.Length >= 6
            ? string.Join("-", mac.Take(6).Select(b => b.ToString("X2", CultureInfo.InvariantCulture)))
            : "";
}
