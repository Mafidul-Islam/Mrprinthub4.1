using System.Net.NetworkInformation;
using MRPrintHub.Network;

namespace MRPrintHub.Network;

/// <summary>Heuristics that identify virtual / VPN / tunneling adapter
/// candidates which must never be selected as "the shop network". Pure.</summary>
public static class VirtualAdapterDetector
{
    private static readonly string[] VirtualKeywords =
    {
        "virtual", "vpn", "tap", "tun-", "tunnel", "tunneling",
        "vmware", "virtualbox", "hyper-v", "vethernet", "hamachi",
        "isatap", "teredo", "6to4", "4to6", "loopback", "wsl",
        "bluetooth", "nordvpn", "wireguard", "openvpn", "zerotier",
        "tailscale", "hamachi", "vpnclient", "vpngate", "zeroTier",
    };

    public static bool IsVirtualOrVpn(NetworkAdapterInfo adapter)
    {
        if (adapter.InterfaceType == NetworkInterfaceType.Loopback
            || adapter.InterfaceType == NetworkInterfaceType.Tunnel)
            return true;

        var haystack = ($"{adapter.Name} {adapter.Description}".ToLowerInvariant());
        foreach (var keyword in VirtualKeywords)
        {
            if (haystack.Contains(keyword.ToLowerInvariant()))
                return true;
        }

        return false;
    }
}