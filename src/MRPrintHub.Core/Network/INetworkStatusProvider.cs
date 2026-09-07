using MRPrintHub.Core.DTOs;

namespace MRPrintHub.Core.Network;

/// <summary>
/// Abstraction over local-network state that the rest of the app depends on,
/// so network behavior is mockable in tests and swappable per host. Lives in
/// Core so QR/Server/Service can consume it without referencing the Network
/// project directly.
/// </summary>
public interface INetworkStatusProvider
{
    /// <summary>Raised whenever the active adapter comes up, goes down, or changes.</summary>
    event EventHandler<NetworkChangedEventArgs>? NetworkChanged;

    /// <summary>True when a suitable local adapter is currently selected.</summary>
    bool IsOnline { get; }

    /// <summary>Currently selected adapter (IP bound by the HTTP server), or null when offline.</summary>
    ActiveAdapterInfo? GetActiveAdapter();

    void Start();

    void Stop();
}