namespace MRPrintHub.Core.DTOs.Cloud;

using MRPrintHub.Core.Enums;

/// <summary>
/// Internal cloud connection status snapshot for Desktop application.
/// </summary>
public record CloudStatusDto(
    CloudConnectionState State,
    string? ShopCode,
    string? DeviceId,
    DateTime? LastConnectedAt,
    string? ErrorMessage);
