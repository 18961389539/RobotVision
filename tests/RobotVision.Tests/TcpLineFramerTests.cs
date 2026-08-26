using System.Text;
using FluentAssertions;
using RobotVision.Infrastructure.Communication;
using Xunit;

namespace RobotVision.Tests;

public class TcpLineFramerTests
{
    private static (List<string> Complete, string Pending) Feed(params string[] chunks)
    {
        var pending = new StringBuilder();
        var complete = new List<string>();
        foreach (var chunk in chunks)
            TcpLineFramer.Append(pending, Encoding.ASCII.GetBytes(chunk), complete);
        return (complete, pending.ToString());
    }

    [Fact]
    public void Lf_SplitsImmediately()
    {
        var (complete, pending) = Feed("PING\n");
        complete.Should().Equal("PING");
        pending.Should().BeEmpty();
    }

    [Fact]
    public void CrLf_TreatedAsSingleDelimiter()
    {
        var (complete, pending) = Feed("PING\r\nSTATUS\r\n");
        complete.Should().Equal("PING", "STATUS");
        pending.Should().BeEmpty();
    }

    [Fact]
    public void BareCr_Splits()
    {
        var (complete, pending) = Feed("PING\r#1");
        complete.Should().Equal("PING");
        pending.Should().Be("#1");
    }

    [Fact]
    public void NoDelimiter_StaysPending()
    {
        var (complete, pending) = Feed("PING");
        complete.Should().BeEmpty();
        pending.Should().Be("PING");
    }

    [Fact]
    public void SplitAcrossPackets_Assembles()
    {
        var pending = new StringBuilder();
        var complete = new List<string>();
        TcpLineFramer.Append(pending, "PI"u8, complete);
        complete.Should().BeEmpty();
        TcpLineFramer.Append(pending, "NG\n"u8, complete);
        complete.Should().Equal("PING");
        pending.ToString().Should().BeEmpty();
    }

    [Fact]
    public void CrThenLfInNextPacket_DoesNotLeaveEmptyCommandStuck()
    {
        var pending = new StringBuilder();
        var complete = new List<string>();
        TcpLineFramer.Append(pending, "PING\r"u8, complete);
        complete.Should().Equal("PING");
        TcpLineFramer.Append(pending, "\n"u8, complete);
        complete.Should().Equal("PING", "");
    }

    [Fact]
    public void OverlongWithoutNewline_ForcedSplit()
    {
        var pending = new StringBuilder();
        var complete = new List<string>();
        var blob = new string('A', TcpLineFramer.MaxFrameChars + 10);
        TcpLineFramer.Append(pending, Encoding.ASCII.GetBytes(blob), complete);
        complete.Should().ContainSingle();
        complete[0].Length.Should().Be(TcpLineFramer.MaxFrameChars);
        pending.ToString().Should().Be(new string('A', 10));
    }
}
