using System.Net.NetworkInformation;
using MRPrintHub.Core.DTOs;
using MRPrintHub.Core.Enums;
using MRPrintHub.Core.Network;
using MRPrintHub.Network;
using MRPrintHub.UnitTests.Fakes;
using Xunit;

namespace MRPrintHub.UnitTests.Network;

public class NetworkStatusProviderTests
{
    private sealed class FakeAdapterSource : IAdapterSource
    {
        public IReadOnlyList<NetworkAdapterInfo> Adapters { get; set; } = [];
        public IReadOnlyList<NetworkAdapterInfo> GetAdapters() => Adapters;
    }

    private sealed class InvalidationListener
    {
        public int Invalidations { get; private set; }
        public NetworkChangeReason? LastReason { get; private set; }
        public ActiveAdapterInfo? LastCurrent { get; private set; }

        public void Attach(INetworkStatusProvider provider)
        {
            provider.NetworkChanged += (_, e) =>
            {
                Invalidations++;
                LastReason = e.Reason;
                LastCurrent = e.Current;
            };
        }
    }

    [Fact]
    public void Start_WithActiveAdapter_ReportsOnline()
    {
        var source = new FakeAdapterSource { Adapters = [AdapterFactory.Wifi()] };
        using var provider = new NetworkStatusProvider(source);

        provider.Start();

        Assert.True(provider.IsOnline);
        Assert.Equal("192.168.1.5", provider.GetActiveAdapter()!.IpAddress);
    }

    [Fact]
    public void Start_WithNoAdapter_ReportsOffline()
    {
        var source = new FakeAdapterSource { Adapters = [] };
        using var provider = new NetworkStatusProvider(source);

        provider.Start();

        Assert.False(provider.IsOnline);
        Assert.Null(provider.GetActiveAdapter());
    }

    [Fact]
    public void Start_ThenRecheckWithoutChange_RaisesNothing()
    {
        var source = new FakeAdapterSource { Adapters = [AdapterFactory.Wifi()] };
        using var provider = new NetworkStatusProvider(source);
        var listener = new InvalidationListener();
        listener.Attach(provider);

        provider.Start();
        provider.ForceRecheck();

        Assert.Equal(0, listener.Invalidations);
    }

    [Fact]
    public void Disconnect_RaisesWentOffline()
    {
        var source = new FakeAdapterSource { Adapters = [AdapterFactory.Wifi()] };
        using var provider = new NetworkStatusProvider(source);
        var listener = new InvalidationListener();
        listener.Attach(provider);
        provider.Start();

        source.Adapters = [];
        provider.ForceRecheck();

        Assert.Equal(1, listener.Invalidations);
        Assert.Equal(NetworkChangeReason.WentOffline, listener.LastReason);
        Assert.False(provider.IsOnline);
    }

    [Fact]
    public void Reconnect_RaisesBecameOnline()
    {
        var source = new FakeAdapterSource { Adapters = [] };
        using var provider = new NetworkStatusProvider(source);
        var listener = new InvalidationListener();
        listener.Attach(provider);
        provider.Start();

        source.Adapters = [AdapterFactory.Wifi()];
        provider.ForceRecheck();

        Assert.Equal(1, listener.Invalidations);
        Assert.Equal(NetworkChangeReason.BecameOnline, listener.LastReason);
        Assert.True(provider.IsOnline);
    }

    [Fact]
    public void IpChange_RaisesAddressChanged()
    {
        var source = new FakeAdapterSource { Adapters = [AdapterFactory.Wifi()] };
        using var provider = new NetworkStatusProvider(source);
        var listener = new InvalidationListener();
        listener.Attach(provider);
        provider.Start();

        source.Adapters = [AdapterFactory.Wifi(ip: "192.168.1.200")];
        provider.ForceRecheck();

        Assert.Equal(1, listener.Invalidations);
        Assert.Equal(NetworkChangeReason.AddressChanged, listener.LastReason);
        Assert.Equal("192.168.1.200", listener.LastCurrent!.IpAddress);
    }

    [Fact]
    public void InterfaceChange_RaisesInterfaceChanged()
    {
        var source = new FakeAdapterSource { Adapters = [AdapterFactory.Wifi()] };
        using var provider = new NetworkStatusProvider(source);
        var listener = new InvalidationListener();
        listener.Attach(provider);
        provider.Start();

        source.Adapters = [AdapterFactory.Ethernet()];
        provider.ForceRecheck();

        Assert.Equal(1, listener.Invalidations);
        Assert.Equal(NetworkChangeReason.InterfaceChanged, listener.LastReason);
    }

    [Fact]
    public void NewVpnAppears_SelectionUnchanged_RaisesNothing()
    {
        var source = new FakeAdapterSource { Adapters = [AdapterFactory.Wifi()] };
        using var provider = new NetworkStatusProvider(source);
        var listener = new InvalidationListener();
        listener.Attach(provider);
        provider.Start();

        source.Adapters = [AdapterFactory.Wifi(), AdapterFactory.VirtualTunnel()];
        provider.ForceRecheck();

        Assert.Equal(0, listener.Invalidations);
    }
}