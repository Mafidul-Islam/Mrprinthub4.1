using MRPrintHub.Network;
using MRPrintHub.UnitTests.Fakes;
using Xunit;
using System.Net.NetworkInformation;

namespace MRPrintHub.UnitTests.Network;

public class AdapterResolverTests
{
    [Fact]
    public void SelectBest_WifiOnly_ReturnsWifi()
    {
        var adapters = new[] { AdapterFactory.Wifi() };

        var result = AdapterResolver.SelectBest(adapters);

        Assert.NotNull(result);
        Assert.Equal("192.168.1.5", result!.IpAddress);
        Assert.Equal("Wi-Fi", result.InterfaceName);
    }

    [Fact]
    public void SelectBest_EthernetOnly_ReturnsEthernet()
    {
        var adapters = new[] { AdapterFactory.Ethernet() };

        var result = AdapterResolver.SelectBest(adapters);

        Assert.NotNull(result);
        Assert.Equal("192.168.0.10", result!.IpAddress);
    }

    [Fact]
    public void SelectBest_BothWithGateway_PrefersEthernet()
    {
        var adapters = new[] { AdapterFactory.Wifi(), AdapterFactory.Ethernet() };

        var result = AdapterResolver.SelectBest(adapters);

        Assert.NotNull(result);
        Assert.Equal("Ethernet", result!.InterfaceName);
    }

    [Fact]
    public void SelectBest_EthernetNoGateway_WifiWithGateway_PrefersWifi()
    {
        var adapters = new[]
        {
            AdapterFactory.Ethernet(gateway: false),
            AdapterFactory.Wifi()
        };

        var result = AdapterResolver.SelectBest(adapters);

        Assert.NotNull(result);
        Assert.Equal("Wi-Fi", result!.InterfaceName);
    }

    [Fact]
    public void SelectBest_VpnPresent_PrefersPhysicalAdapter()
    {
        var adapters = new[]
        {
            AdapterFactory.VirtualTunnel(),
            AdapterFactory.Wifi()
        };

        var result = AdapterResolver.SelectBest(adapters);

        Assert.NotNull(result);
        Assert.Equal("Wi-Fi", result!.InterfaceName);
        Assert.Equal("192.168.1.5", result.IpAddress);
    }

    [Fact]
    public void SelectBest_AllDown_ReturnsNull()
    {
        var adapters = new[]
        {
            AdapterFactory.Wifi(operational: false),
            AdapterFactory.Ethernet(operational: false)
        };

        Assert.Null(AdapterResolver.SelectBest(adapters));
    }

    [Fact]
    public void SelectBest_OnlyVirtualOrLoopback_ReturnsNull()
    {
        var adapters = new[]
        {
            AdapterFactory.VirtualTunnel(),
            AdapterFactory.Custom("Loopback", "Software Loopback Interface 1",
                NetworkInterfaceType.Loopback, true, false, "127.0.0.1")
        };

        Assert.Null(AdapterResolver.SelectBest(adapters));
    }

    [Fact]
    public void SelectBest_VpnDescriptionKeywords_FilteredOut()
    {
        var adapters = new[]
        {
            AdapterFactory.Custom("VPN - VPN Client", "Cisco AnyConnect Secure Mobility Client",
                NetworkInterfaceType.Ethernet, true, true, "10.100.0.5"),
            AdapterFactory.Ethernet()
        };

        var result = AdapterResolver.SelectBest(adapters);

        Assert.NotNull(result);
        Assert.Equal("Ethernet", result!.InterfaceName);
    }

    [Fact]
    public void SelectBest_NoGatewayAnywhere_StillPicksOperationalWithIp()
    {
        var adapters = new[]
        {
            AdapterFactory.Wifi(gateway: false),
            AdapterFactory.Ethernet(gateway: false)
        };

        var result = AdapterResolver.SelectBest(adapters);

        Assert.NotNull(result);
        Assert.Equal("Ethernet", result!.InterfaceName);
    }

    [Fact]
    public void SelectBest_EmptyList_ReturnsNull()
    {
        Assert.Null(AdapterResolver.SelectBest(Array.Empty<NetworkAdapterInfo>()));
    }
}