using System.Net.NetworkInformation;
using MRPrintHub.Core.DTOs;
using MRPrintHub.Core.Enums;
using MRPrintHub.Core.Network;
using Microsoft.Win32;

namespace MRPrintHub.Network;

/// <summary>
/// Real <see cref="INetworkStatusProvider"/> that watches the system via
/// <see cref="NetworkChange"/> availability/address events and
/// <see cref="SystemEvents.PowerModeChanged"/>, re-resolving the active adapter
/// on any change, and raising <see cref="NetworkChanged"/> when the selected
/// adapter comes up, goes down, or changes identity (so sessions / QR codes
/// can be regenerated).
/// </summary>
public sealed class NetworkStatusProvider : INetworkStatusProvider, IDisposable
{
    private readonly IAdapterSource _adapterSource;
    private ActiveAdapterInfo? _current;
    private bool _started;
    private bool _disposed;

    public event EventHandler<NetworkChangedEventArgs>? NetworkChanged;

    public NetworkStatusProvider(IAdapterSource? adapterSource = null)
        => _adapterSource = adapterSource ?? new SystemAdapterSource();

    public bool IsOnline => _current is not null;

    public ActiveAdapterInfo? GetActiveAdapter() => _current;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started)
            return;

        _started = true;
        _current = AdapterResolver.SelectBest(_adapterSource.GetAdapters());

        NetworkChange.NetworkAddressChanged += OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    public void Stop()
    {
        if (!_started)
            return;

        NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _started = false;
    }

    /// <summary>
    /// Re-reads adapters and re-evaluates the active selection, raising
    /// <see cref="NetworkChanged"/> only when something actually changed.
    /// Public so power-mode handlers and tests can force a fresh check.
    /// </summary>
    public void ForceRecheck()
    {
        var next = AdapterResolver.SelectBest(_adapterSource.GetAdapters());
        var reason = DetermineReason(_current, next);
        if (reason == NetworkChangeReason.None)
            return;

        var previous = _current;
        _current = next;
        Raise(reason, previous, next);
    }

    private void OnNetworkChanged(object? sender, EventArgs e) => ForceRecheck();

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        // Never trust stale adapter state across sleep/wake.
        if (e.Mode is PowerModes.Resume or PowerModes.Suspend or PowerModes.StatusChange)
            ForceRecheck();
    }

    private static NetworkChangeReason DetermineReason(ActiveAdapterInfo? previous, ActiveAdapterInfo? current)
    {
        if (previous is null && current is null)
            return NetworkChangeReason.None;

        if (previous is null && current is not null)
            return NetworkChangeReason.BecameOnline;

        if (previous is not null && current is null)
            return NetworkChangeReason.WentOffline;

        if (!string.Equals(previous!.InterfaceName, current!.InterfaceName, StringComparison.Ordinal))
            return NetworkChangeReason.InterfaceChanged;

        if (!string.Equals(previous.IpAddress, current.IpAddress, StringComparison.Ordinal))
            return NetworkChangeReason.AddressChanged;

        return NetworkChangeReason.None;
    }

    private void Raise(NetworkChangeReason reason, ActiveAdapterInfo? previous, ActiveAdapterInfo? current)
    {
        NetworkChanged?.Invoke(this, new NetworkChangedEventArgs
        {
            Reason = reason,
            Previous = previous,
            Current = current
        });
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        Stop();
        _disposed = true;
    }
}