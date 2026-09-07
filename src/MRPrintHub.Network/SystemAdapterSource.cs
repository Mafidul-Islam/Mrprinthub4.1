using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MRPrintHub.Network;

/// <summary>Real adapter enumeration backed by
/// <see cref="NetworkInterface.GetAllNetworkInterfaces"/>.</summary>
public sealed class SystemAdapterSource : IAdapterSource
{
    public IReadOnlyList<NetworkAdapterInfo> GetAdapters()
    {
        var results = new List<NetworkAdapterInfo>();

        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            IPInterfaceProperties props;
            try
            {
                props = ni.GetIPProperties();
            }
            catch
            {
                // Some virtual/tunneling adapters throw on property access.
                continue;
            }

            var ipv4 = props.UnicastAddresses
                .Where(u => u.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(u => u.Address.ToString())
                .ToList();

            results.Add(new NetworkAdapterInfo
            {
                Name = ni.Name,
                Description = ni.Description,
                InterfaceType = ni.NetworkInterfaceType,
                IsOperational = ni.OperationalStatus == OperationalStatus.Up,
                HasDefaultGateway = props.GatewayAddresses.Count > 0,
                IPv4Addresses = ipv4
            });
        }

        return results;
    }
}