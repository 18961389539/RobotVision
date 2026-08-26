using System.Collections.Concurrent;
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
    private static readonly ConcurrentDictionary<string, IPAddress> Assigned = new(StringComparer.OrdinalIgnoreCase);

    public const int GvcpPort = 3956;
    public const byte GvcpMagic = 0x42;
    public const ushort ForceIpCommand = 0x0004;

    /// <summary>把 GVCP FORCEIP 报文打成 64 字节（8 头 + 56 负载，符合 GigE Vision spec）。</summary>
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
    /// 若目标相机 IP 已与本机固定网段同网段则原样返回；否则 FORCEIP 到该网段空闲地址，
    /// 返回带新 IP 的相机信息。
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
        Assigned[FormatMac(plan.Mac)] = plan.NewIp;
        log?.LogWarning(
            "相机 SN={Sn} MAC={Mac} 原 IP {Old} 与网卡不同网段，已临时 FORCEIP 为 {New}/{Mask}。请在 pylon Viewer 写成永久静态 IP，避免下次再分配。",
            camera.SerialNumber, FormatMac(plan.Mac), plan.OldIp, plan.NewIp, plan.Mask);
        Thread.Sleep(1500);
        return CloneWithIp(camera, plan.NewIp);
    }

    /// <summary>对发现列表中的每台相机做网段对齐；已在固定网段的相机原样返回。</summary>
    public static IReadOnlyList<GigECameraInfo> EnsureAllReachable(
        IEnumerable<GigECameraInfo> cameras, ILogger? log = null)
    {
        ArgumentNullException.ThrowIfNull(cameras);
        var aligned = new List<GigECameraInfo>();
        foreach (var camera in cameras)
            aligned.Add(EnsureReachable(camera, log));
        return aligned;
    }

    private static GigECameraInfo CloneWithIp(GigECameraInfo camera, IPAddress ip) =>
        new()
        {
            IpAddress = ip,
            SerialNumber = camera.SerialNumber,
            ManufacturerName = camera.ManufacturerName,
            ModelName = camera.ModelName,
            UserDefinedName = camera.UserDefinedName,
            MacAddress = camera.MacAddress,
        };

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

    public static IPAddress PickFreeAddress(IPAddress local, IPAddress mask, IReadOnlyCollection<IPAddress> used)
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
        used.AddRange(Assigned.Values);
        used.Add(camera.IpAddress);

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
        udp.Send(packet, packet.Length, new IPEndPoint(IPAddress.Parse("169.254.255.255"), GvcpPort));
        var subnetBroadcast = FromUint((ToUint(plan.Local) & ToUint(plan.Mask)) | ~ToUint(plan.Mask));
        udp.Send(packet, packet.Length, new IPEndPoint(subnetBroadcast, GvcpPort));
        udp.Send(packet, packet.Length, new IPEndPoint(plan.OldIp, GvcpPort));
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

    public static bool SameSubnet(IPAddress local, IPAddress mask, IPAddress camera)
    {
        var m = ToUint(mask);
        return (ToUint(local) & m) == (ToUint(camera) & m);
    }

    public static bool IsApipa(IPAddress ip)
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
        mac.Length >= 6 ? string.Join("-", mac.Take(6).Select(b => b.ToString("X2"))) : "";
}
