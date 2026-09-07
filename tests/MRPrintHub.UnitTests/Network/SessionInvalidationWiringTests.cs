using MRPrintHub.Core.DTOs;
using MRPrintHub.Core.Enums;
using MRPrintHub.Core.Network;
using Xunit;

namespace MRPrintHub.UnitTests.Network;

/// <summary>
/// Integration-style contract test: the rest of the app must subscribe to
/// <see cref="INetworkStatusProvider.NetworkChanged"/> and invalidate the current
/// upload session whenever the bound adapter's IP changes or connectivity is lost.
/// Uses a hand-rolled fake provider to drive the events.
/// </summary>
public class SessionInvalidationWiringTests
{
    private sealed class FakeNetworkStatusProvider : INetworkStatusProvider
    {
        public event EventHandler<NetworkChangedEventArgs>? NetworkChanged;
        public bool IsOnline { get; private set; }

        public ActiveAdapterInfo? Active { get; private set; }

        public ActiveAdapterInfo? GetActiveAdapter() => Active;

        public void Start() { }

        public void Stop() { }

        public void Raise(NetworkChangeReason reason, ActiveAdapterInfo? current)
        {
            Active = current;
            IsOnline = current is not null;
            NetworkChanged?.Invoke(this, new NetworkChangedEventArgs
            {
                Reason = reason,
                Current = current
            });
        }
    }

    private sealed class SessionManagerHarness
    {
        public int InvalidationCount { get; private set; }
        public NetworkChangeReason? LastReason { get; private set; }
        public bool CurrentSessionIsNull { get; private set; }

        public SessionManagerHarness(INetworkStatusProvider provider)
        {
            provider.NetworkChanged += OnNetworkChanged;
        }

        private void OnNetworkChanged(object? sender, NetworkChangedEventArgs e)
        {
            // Any change to the adapter/IP the server is bound to means the QR
            // URL is stale -> invalidate the session so it cannot be used further.
            if (e.Reason is NetworkChangeReason.AddressChanged
                or NetworkChangeReason.InterfaceChanged
                or NetworkChangeReason.WentOffline
                or NetworkChangeReason.BecameOnline)
            {
                InvalidationCount++;
                LastReason = e.Reason;
                CurrentSessionIsNull = true;
            }
        }
    }

    private static FakeNetworkStatusProvider Wire(out SessionManagerHarness harness)
    {
        var provider = new FakeNetworkStatusProvider();
        harness = new SessionManagerHarness(provider);
        return provider;
    }

    [Fact]
    public void IpChange_InvalidatesSession()
    {
        var provider = Wire(out var harness);

        provider.Raise(NetworkChangeReason.AddressChanged,
            new ActiveAdapterInfo("192.168.1.200", "Wi-Fi", "Intel Wi-Fi"));

        Assert.Equal(1, harness.InvalidationCount);
        Assert.Equal(NetworkChangeReason.AddressChanged, harness.LastReason);
        Assert.True(harness.CurrentSessionIsNull);
    }

    [Fact]
    public void Disconnect_InvalidatesSession()
    {
        var provider = Wire(out var harness);

        provider.Raise(NetworkChangeReason.WentOffline, null);

        Assert.Equal(1, harness.InvalidationCount);
        Assert.Equal(NetworkChangeReason.WentOffline, harness.LastReason);
    }

    [Fact]
    public void ReconnectWithNewIp_InvalidatesSession()
    {
        var provider = Wire(out var harness);

        provider.Raise(NetworkChangeReason.BecameOnline,
            new ActiveAdapterInfo("192.168.0.44", "Ethernet", "Realtek GbE"));

        Assert.Equal(1, harness.InvalidationCount);
        Assert.Equal(NetworkChangeReason.BecameOnline, harness.LastReason);
    }

    [Fact]
    public void SpuriousEvent_DoesNotInvalidate()
    {
        var provider = Wire(out var harness);

        provider.Raise(NetworkChangeReason.None,
            new ActiveAdapterInfo("192.168.1.5", "Wi-Fi", "Intel Wi-Fi"));

        Assert.Equal(0, harness.InvalidationCount);
    }

    [Fact]
    public void MultipleChanges_EachInvalidate()
    {
        var provider = Wire(out var harness);

        provider.Raise(NetworkChangeReason.AddressChanged,
            new ActiveAdapterInfo("192.168.1.200", "Wi-Fi", "Intel Wi-Fi"));
        provider.Raise(NetworkChangeReason.AddressChanged,
            new ActiveAdapterInfo("192.168.1.201", "Wi-Fi", "Intel Wi-Fi"));
        provider.Raise(NetworkChangeReason.WentOffline, null);

        Assert.Equal(3, harness.InvalidationCount);
    }
}