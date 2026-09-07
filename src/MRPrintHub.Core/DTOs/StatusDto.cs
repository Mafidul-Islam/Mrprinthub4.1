namespace MRPrintHub.Core.DTOs;

public record StatusDto(
    bool IsOnline,
    string? IpAddress,
    string? InterfaceName,
    string? CurrentSessionToken,
    DateTime? SessionExpiry);
