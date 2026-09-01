using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using GenICam.Net.GigEVision.Gvcp;

namespace RobotVision.Infrastructure.Cameras;

/// <summary>
/// 相机在 APIPA（169.254.x）而工控机网卡在固定网段时，pylon 枚举不到设备（No matching camera found），
/// GVCP 单播也打不通。按 GigE Vision FORCEIP 给相机临时分配与网卡同网段的 IP。
/// </summary>
public static class GigEForceIp
{
    public const int GvcpPort = 3956;
    public const byte GvcpMagic = 0x42;
    public const ushort ForceIpCommand = 0x0004;

    /// <summary>
    /// 把 GVCP FORCEIP 报文打成 64 字节（8 头 + 56 负载，符合 GigE Vision spec）。
    /// 注意：1e8fca9 曾误改成 28 字节紧凑布局（MAC@8、flags=0x00、length=20），
    /// 不符合规范的 reserved 偏移与 ACK/broadcast flags，相机固件会直接丢弃报文，
    /// 导致 FORCEIP 静默失效——已恢复规范布局（与 GigEForceIpTests 断言一致）。
    /// </summary>
    public static byte[] BuildPacket(ReadOnlySpan<byte> mac6, IPAddress ip, IPAddress subnetMask, IPAddress gateway,
        ushort requestId = 1)
    {
        ArgumentNullException.ThrowIfNull(ip);
        ArgumentNullException.ThrowIfNull(subnetMask);
        ArgumentNullException.ThrowIfNull(gateway);
        if (mac6.Length < 6)
            throw new ArgumentException("MAC 至少 6 字节", nameof(mac6));

        var packet = new byte[64];
        packet[0] = GvcpMagic;
        packet[1] = 0x11; // ACK required + broadcast
        packet[2] = (byte)(ForceIpCommand >> 8);
        packet[3] = (byte)ForceIpCommand;
        packet[4] = 0x00;
        packet[5] = 56;
        packet[6] = (byte)(requestId >> 8);
        packet[7] = (byte)requestId;
        mac6[..6].CopyTo(packet.AsSpan(10, 6));
        WriteIpv4(packet, 28, ip);
        WriteIpv4(packet, 44, subnetMask);
        WriteIpv4(packet, 60, gateway);
        return packet;
    }

    /// <summary>
    /// 若目标相机 IP 已与本机固定网段同网段则直接返回；否则 FORCEIP 到该网段空闲地址，
    /// 并写回 <see cref="GigECameraInfo.IpAddress"/>。
    /// </summary>
    public static GigECameraInfo EnsureReachable(GigECameraInfo camera, ILogger? log = null)
    {
        ArgumentNullException.ThrowIfNull(camera);
        if (IsOnLocalFixedSubnet(camera.IpAddress))
            return camera;

        var plan = TryPlan(camera)
            ?? throw new InvalidOperationException(
                $"相机 {camera.SerialNumber} 的 IP {camera.IpAddress} 与本机网卡不在同一网段，且没有可用的固定网段可分配临时 IP。请在 pylon IP Configurator 把相机设成与网卡同网段。");

        Send(plan);
        var aligned = new GigECameraInfo
        {
            IpAddress = plan.NewIp,
            SerialNumber = camera.SerialNumber,
            MacAddress = camera.MacAddress,
            ManufacturerName = camera.ManufacturerName,
            ModelName = camera.ModelName,
            UserDefinedName = camera.UserDefinedName,
        };
        if (log is { } logger)
            GigEForceIpLog.ForceIpApplied(
                logger, camera.SerialNumber, FormatMac(plan.Mac),
                plan.OldIp.ToString(), plan.NewIp.ToString(), plan.Mask.ToString());
        Thread.Sleep(800);
        return aligned;
    }

    public static IReadOnlyList<GigECameraInfo> EnsureAllReachable(
        IReadOnlyList<GigECameraInfo> cameras, ILogger? log = null)
    {
        ArgumentNullException.ThrowIfNull(cameras);
        if (cameras.Count == 0)
            return cameras;
        var aligned = new GigECameraInfo[cameras.Count];
        for (var i = 0; i < cameras.Count; i++)
            aligned[i] = EnsureReachable(cameras[i], log);
        return aligned;
    }

    public static IReadOnlyList<GigECameraInfo> EnsureAllReachable(IReadOnlyList<GigECameraInfo> cameras) =>
        EnsureAllReachable(cameras, null);

    public static bool IsOnLocalFixedSubnet(IPAddress cameraIp)
    {
        ArgumentNullException.ThrowIfNull(cameraIp);
        foreach (var (local, mask) in LocalFixedUnicasts())
        {
            if (SameSubnet(local, mask, cameraIp))
                return true;
        }

        return false;
    }

    internal static IPAddress PickFreeAddress(IPAddress local, IPAddress mask, IReadOnlyCollection<IPAddress> used)
    {
        var network = ToUint(local) & ToUint(mask);
        var wildcard = ~ToUint(mask);
        var taken = new HashSet<uint>(used.Select(ToUint)) { ToUint(local) };
        var maxHost = wildcard > 254 ? 254u : wildcard;
        for (uint host = maxHost - 1; host >= 1; host--)
        {
            if (host == wildcard)
                continue;
            var candidate = FromUint(network | host);
            if (taken.Add(ToUint(candidate)))
                return candidate;
        }

        throw new InvalidOperationException($"网段 {local}/{mask} 没有空闲地址可分配给相机");
    }

    private readonly record struct Plan(
        IPAddress Local, IPAddress NewIp, IPAddress Mask, IPAddress Gateway, byte[] Mac, IPAddress OldIp);

    private static Plan? TryPlan(GigECameraInfo camera)
    {
        var nic = BestFixedUnicast();
        if (nic is null)
            return null;

        var used = new List<IPAddress> { nic.Value.Local };
        foreach (var other in GigEVisionCamera.DiscoverCameras())
            used.Add(other.IpAddress);

        var mac = camera.MacAddress is { Length: >= 6 } m ? m[..6].ToArray() : new byte[6];
        var ip = PickFreeAddress(nic.Value.Local, nic.Value.Mask, used);
        return new Plan(nic.Value.Local, ip, nic.Value.Mask, nic.Value.Local, mac, camera.IpAddress);
    }

    private static void Send(Plan plan)
    {
        var packet = BuildPacket(plan.Mac, plan.NewIp, plan.Mask, plan.Gateway);
        using var udp = new UdpClient(new IPEndPoint(plan.Local, 0));
        udp.EnableBroadcast = true;
        udp.Client.SendTimeout = 1000;
        udp.Send(packet, packet.Length, new IPEndPoint(IPAddress.Broadcast, GvcpPort));
        var subnetBroadcast = FromUint((ToUint(plan.Local) & ToUint(plan.Mask)) | ~ToUint(plan.Mask));
        udp.Send(packet, packet.Length, new IPEndPoint(subnetBroadcast, GvcpPort));
    }

    private static (IPAddress Local, IPAddress Mask)? BestFixedUnicast()
    {
        (IPAddress Local, IPAddress Mask, long Speed, bool Ethernet)? best = null;
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
                continue;
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                continue;

            var ethernet = nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet;
            foreach (var ua in nic.GetIPProperties().UnicastAddresses)
            {
                if (ua.Address.AddressFamily != AddressFamily.InterNetwork || ua.IPv4Mask is null)
                    continue;
                if (IPAddress.IsLoopback(ua.Address) || IsApipa(ua.Address))
                    continue;

                var better = best is null
                    || (ethernet && !best.Value.Ethernet)
                    || (ethernet == best.Value.Ethernet && nic.Speed > best.Value.Speed);
                if (better)
                    best = (ua.Address, ua.IPv4Mask, nic.Speed, ethernet);
            }
        }

        return best is null ? null : (best.Value.Local, best.Value.Mask);
    }

    private static IEnumerable<(IPAddress Local, IPAddress Mask)> LocalFixedUnicasts()
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
                continue;
            foreach (var ua in nic.GetIPProperties().UnicastAddresses)
            {
                if (ua.Address.AddressFamily != AddressFamily.InterNetwork || ua.IPv4Mask is null)
                    continue;
                if (IPAddress.IsLoopback(ua.Address) || IsApipa(ua.Address))
                    continue;
                yield return (ua.Address, ua.IPv4Mask);
            }
        }
    }

    internal static bool SameSubnet(IPAddress local, IPAddress mask, IPAddress camera)
    {
        var m = ToUint(mask);
        return (ToUint(local) & m) == (ToUint(camera) & m);
    }

    internal static bool IsApipa(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();
        return bytes.Length == 4 && bytes[0] == 169 && bytes[1] == 254;
    }

    private static void WriteIpv4(byte[] packet, int offset, IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();
        if (bytes.Length != 4)
            throw new ArgumentException("仅支持 IPv4", nameof(ip));
        Buffer.BlockCopy(bytes, 0, packet, offset, 4);
    }

    private static uint ToUint(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
    }

    private static IPAddress FromUint(uint value) =>
        new([(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value]);

    private static string FormatMac(byte[] mac) =>
        mac.Length >= 6 ? string.Join("-", mac.Take(6).Select(b => b.ToString("X2", CultureInfo.InvariantCulture))) : "";
}
