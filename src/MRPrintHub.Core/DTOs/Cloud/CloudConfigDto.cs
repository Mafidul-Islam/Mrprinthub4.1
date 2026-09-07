namespace MRPrintHub.Core.DTOs.Cloud;

/// <summary>
/// Local configuration settings for cloud connectivity.
/// </summary>
public record CloudConfigDto(
    string CloudBaseUrl,
    bool Enabled,
    string? ShopCode,
    string? DeviceId,
    string? DeviceToken);
