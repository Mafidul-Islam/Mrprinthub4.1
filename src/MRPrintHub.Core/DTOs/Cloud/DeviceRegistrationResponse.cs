namespace MRPrintHub.Core.DTOs.Cloud;

/// <summary>
/// Response returned by Cloud Server upon device registration.
/// </summary>
public record DeviceRegistrationResponse(
    bool Success,
    string ShopCode,
    string DeviceId,
    string? DeviceToken,
    string Message,
    DateTime CreatedAt);
