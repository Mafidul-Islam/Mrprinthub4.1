namespace MRPrintHub.Core.Enums;

public enum NetworkChangeReason
{
    None = 0,
    BecameOnline,
    WentOffline,
    AddressChanged,
    InterfaceChanged,
    PowerModeChanged
}