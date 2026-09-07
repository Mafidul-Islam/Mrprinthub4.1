using MRPrintHub.Core.DTOs;
using System.Net.NetworkInformation;
using MRPrintHub.Network;

namespace MRPrintHub.Network;

/// <summary>
/// Pure selection of the best local adapter from an arbitrary adapter list.
/// No I/O - takes already-enumerated adapters so it's fully deterministic in tests.
/// </summary>
public static class AdapterResolver
{
    /// <summary>
    /// Returns the adapter to bind the HTTP server to, or null when nothing usable
    /// is connected. Selection rules (in priority order):
    /// 1. Must be operational, not virtual/VPN, and have at least one IPv4 address.
    /// 2. Prefer adapters that carry a default gateway (the shop's LAN).
    /// 3. Prefer physical interface types (Ethernet over unknown).
    /// 4. Deterministic tie-break by adapter name.
    /// </summary>
    public static ActiveAdapterInfo? SelectBest(IEnumerable<NetworkAdapterInfo> adapters)
    {
        var candidates = adapters
            .Where(a => a.IsOperational)
            .Where(a => !VirtualAdapterDetector.IsVirtualOrVpn(a))
            .Where(a => a.IPv4Addresses.Count > 0)
            .ToList();

        if (candidates.Count == 0)
            return null;

        var withGateway = candidates.Where(a => a.HasDefaultGateway).ToList();
        var pool = withGateway.Count > 0 ? withGateway : candidates;

        var best = pool
            .OrderByDescending(a => InterfaceTypePriority(a.InterfaceType))
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .First();

        return new ActiveAdapterInfo(
            best.IPv4Addresses[0],
            best.Name,
            best.Description);
    }

    private static int InterfaceTypePriority(NetworkInterfaceType type) => type switch
    {
        NetworkInterfaceType.Ethernet => 100,
        NetworkInterfaceType.Wireless80211 => 90,
        _ => 50
    };
}