using System.Net;
using RobotVision.Infrastructure.Cameras;
using Xunit;

namespace RobotVision.Tests;

public class GigEForceIpTests
{
    [Fact]
    public void BuildPacket_HasGvcpForceIpHeaderAndAddresses()
    {
        var mac = new byte[] { 0x00, 0x30, 0x53, 0x37, 0x30, 0x69 };
        var packet = GigEForceIp.BuildPacket(
            mac,
            IPAddress.Parse("192.168.4.254"),
            IPAddress.Parse("255.255.255.0"),
            IPAddress.Parse("192.168.4.99"));

        Assert.Equal(64, packet.Length);
        Assert.Equal(GigEForceIp.GvcpMagic, packet[0]);
        Assert.Equal(0x11, packet[1]);
        Assert.Equal(0x00, packet[2]);
        Assert.Equal(0x04, packet[3]);
        Assert.Equal(56, packet[5]);
        Assert.Equal(new byte[] { 0x00, 0x30, 0x53, 0x37, 0x30, 0x69 }, packet[10..16]);
        Assert.Equal(new byte[] { 192, 168, 4, 254 }, packet[28..32]);
        Assert.Equal(new byte[] { 255, 255, 255, 0 }, packet[44..48]);
        Assert.Equal(new byte[] { 192, 168, 4, 99 }, packet[60..64]);
    }

    [Fact]
    public void IsApipa_DetectsLinkLocal()
    {
        Assert.True(GigEForceIp.IsApipa(IPAddress.Parse("169.254.106.48")));
        Assert.False(GigEForceIp.IsApipa(IPAddress.Parse("192.168.4.99")));
    }

    [Fact]
    public void SameSubnet_MatchesMask()
    {
        var mask = IPAddress.Parse("255.255.255.0");
        Assert.True(GigEForceIp.SameSubnet(
            IPAddress.Parse("192.168.4.99"), mask, IPAddress.Parse("192.168.4.10")));
        Assert.False(GigEForceIp.SameSubnet(
            IPAddress.Parse("192.168.4.99"), mask, IPAddress.Parse("169.254.106.48")));
    }

    [Fact]
    public void PickFreeAddress_SkipsUsedAndPrefersHighHost()
    {
        var ip = GigEForceIp.PickFreeAddress(
            IPAddress.Parse("192.168.4.99"),
            IPAddress.Parse("255.255.255.0"),
            [IPAddress.Parse("192.168.4.254"), IPAddress.Parse("192.168.4.10")]);
        Assert.Equal(IPAddress.Parse("192.168.4.253"), ip);
    }
}
