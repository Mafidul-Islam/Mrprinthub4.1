using System.Net.NetworkInformation;
using MRPrintHub.Network;
using MRPrintHub.UnitTests.Fakes;
using Xunit;

namespace MRPrintHub.UnitTests.Network;

public class VirtualAdapterDetectorTests
{
    [Fact]
    public void IsVirtualOrVpn_ShouldNotMatchRealEthernetAdapter()
    {
        var adapter = AdapterFactory.Custom("Intel Ethernet Controller", "Intel(R) Ethernet Connection I218-V", NetworkInterfaceType.Ethernet, true, true, "192.168.1.5");
        Assert.False(VirtualAdapterDetector.IsVirtualOrVpn(adapter));
    }

    [Fact]
    public void IsVirtualOrVpn_DetectsVirtualByTypeLoopback()
    {
        var adapter = AdapterFactory.Custom("Loopback Interface", "Software Loopback Interface 1", NetworkInterfaceType.Loopback, true, true, "127.0.0.1");
        Assert.True(VirtualAdapterDetector.IsVirtualOrVpn(adapter));
    }

    [Fact]
    public void IsVirtualOrVpn_DetectsVirtualByTypeTunnel()
    {
        var adapter = AdapterFactory.Custom("Some Tunnel", "Generic tunnel", NetworkInterfaceType.Tunnel, true, true, "10.0.0.1");
        Assert.True(VirtualAdapterDetector.IsVirtualOrVpn(adapter));
    }

    [Fact]
    public void IsVirtualOrVpn_DetectsVirtualByKeywords_Tap()
    {
        var adapter = AdapterFactory.Custom("TAP-Windows Adapter V9", "TAP-Windows Adapter V9", NetworkInterfaceType.Ethernet, true, true, "10.8.0.2");
        Assert.True(VirtualAdapterDetector.IsVirtualOrVpn(adapter));
    }

    [Fact]
    public void IsVirtualOrVpn_DetectsVirtualByKeywords_Vpn()
    {
        var adapter = AdapterFactory.Custom("NordVPN Client", "NordVPN TUN", NetworkInterfaceType.Ethernet, true, true, "10.100.0.5");
        Assert.True(VirtualAdapterDetector.IsVirtualOrVpn(adapter));
    }

    [Fact]
    public void IsVirtualOrVpn_DetectsVirtualByKeywords_Wireguard()
    {
        var adapter = AdapterFactory.Custom("WireGuard", "WireGuard VPN", NetworkInterfaceType.Ethernet, true, true, "1.2.3.4");
        Assert.True(VirtualAdapterDetector.IsVirtualOrVpn(adapter));
    }

    [Fact]
    public void IsVirtualOrVpn_ExcludesRealPhysicalAdapters()
    {
        var adapter = AdapterFactory.Custom("Intel Ethernet Controller", "Intel(R) Ethernet Connection I218-V", NetworkInterfaceType.Ethernet, true, true, "192.168.1.5");
        Assert.False(VirtualAdapterDetector.IsVirtualOrVpn(adapter));
    }

    [Theory]
    [InlineData("Ethernet", "Realtek PCIe GbE Family Controller")]
    [InlineData("Wi-Fi", "Intel(R) Wi-Fi 6 AX201")]
    [InlineData("Ethernet 2", "Broadcom NetXtreme Gigabit Ethernet")]
    public void IsVirtualOrVpn_ExcludesRealPhysicalAdapters2(string name, string desc)
    {
        var adapter = AdapterFactory.Custom(name, desc, NetworkInterfaceType.Ethernet, true, true, "192.168.1.5");
        Assert.False(VirtualAdapterDetector.IsVirtualOrVpn(adapter));
    }

    [Fact]
    public void IsVirtualOrVpn_WirelessType_Passes()
    {
        var adapter = AdapterFactory.Custom("Wi-Fi", "Intel(R) Wi-Fi 6 AX201", NetworkInterfaceType.Wireless80211, true, true, "192.168.1.5");
        Assert.False(VirtualAdapterDetector.IsVirtualOrVpn(adapter));
    }
}