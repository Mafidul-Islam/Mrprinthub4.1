using System.Net.NetworkInformation;
using MRPrintHub.Network;

namespace MRPrintHub.UnitTests.Fakes;

public static class AdapterFactory
{
    public static NetworkAdapterInfo Ethernet(
        string ip = "192.168.0.10",
        string name = "Ethernet",
        string desc = "Realtek PCIe GbE Family Controller",
        bool operational = true,
        bool gateway = true)
    {
        return new NetworkAdapterInfo
        {
            Name = name,
            Description = desc,
            InterfaceType = NetworkInterfaceType.Ethernet,
            IsOperational = operational,
            HasDefaultGateway = gateway,
            IPv4Addresses = operational ? new[] { ip } : Array.Empty<string>()
        };
    }

    public static NetworkAdapterInfo Wifi(
        string ip = "192.168.1.5",
        string name = "Wi-Fi",
        string desc = "Intel(R) Wi-Fi 6 AX201",
        bool operational = true,
        bool gateway = true)
    {
        return new NetworkAdapterInfo
        {
            Name = name,
            Description = desc,
            InterfaceType = NetworkInterfaceType.Wireless80211,
            IsOperational = operational,
            HasDefaultGateway = gateway,
            IPv4Addresses = operational ? new[] { ip } : Array.Empty<string>()
        };
    }

    public static NetworkAdapterInfo VirtualTunnel(
        string name = "TAP-Windows Adapter V9",
        string desc = "TAP-Windows Adapter V9",
        string ip = "10.8.0.2",
        NetworkInterfaceType type = NetworkInterfaceType.Tunnel,
        bool gateway = true)
    {
        return new NetworkAdapterInfo
        {
            Name = name,
            Description = desc,
            InterfaceType = type,
            IsOperational = true,
            HasDefaultGateway = gateway,
            IPv4Addresses = new[] { ip }
        };
    }

    public static NetworkAdapterInfo Custom(
        string name,
        string desc,
        NetworkInterfaceType type,
        bool operational,
        bool gateway,
        params string[] ips)
    {
        return new NetworkAdapterInfo
        {
            Name = name,
            Description = desc,
            InterfaceType = type,
            IsOperational = operational,
            HasDefaultGateway = gateway,
            IPv4Addresses = ips
        };
    }
}