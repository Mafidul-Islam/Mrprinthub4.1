namespace MRPrintHub.Core.DTOs.Cloud;

/// <summary>
/// Request payload sent by Desktop application to register with Cloud Server.
/// </summary>
public record DeviceRegistrationRequest(
    string? ShopCode,
    string DeviceName,
    string ClientVersion,
    string? HardwareFingerprint);
