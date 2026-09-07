namespace MRPrintHub.Network;

/// <summary>Source of network adapters, abstracted so tests can feed synthetic
/// adapter lists into <see cref="NetworkStatusProvider"/>.</summary>
public interface IAdapterSource
{
    IReadOnlyList<NetworkAdapterInfo> GetAdapters();
}