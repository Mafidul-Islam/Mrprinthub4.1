using MRPrintHub.Core.DTOs;
using MRPrintHub.Core.Enums;

namespace MRPrintHub.Core.Network;

public class NetworkChangedEventArgs : EventArgs
{
    public required NetworkChangeReason Reason { get; init; }
    public ActiveAdapterInfo? Previous { get; init; }
    public ActiveAdapterInfo? Current { get; init; }
}