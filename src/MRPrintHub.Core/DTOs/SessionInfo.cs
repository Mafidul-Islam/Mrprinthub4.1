namespace MRPrintHub.Core.DTOs;

using MRPrintHub.Core.Enums;

public record SessionInfo(
    string Token,
    string IpAddress,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    string InterfaceName,
    SessionState State);
