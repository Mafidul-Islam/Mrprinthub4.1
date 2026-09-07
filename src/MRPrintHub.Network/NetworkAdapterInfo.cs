using System.Net.NetworkInformation;

namespace MRPrintHub.Network;

/// <summary>
/// Raw read-only snapshot of a single network interface, decoupled from
/// <see cref="System.Net.NetworkInformation"/> so tests can construct any
/// adapter topology by hand.
/// </summary>
public sealed class NetworkAdapterInfo
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required NetworkInterfaceType InterfaceType { get; init; }
    public required bool IsOperational { get; init; }
    public required bool HasDefaultGateway { get; init; }
    public required IReadOnlyList<string> IPv4Addresses { get; init; } = [];
}