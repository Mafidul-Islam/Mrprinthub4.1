namespace MRPrintHub.Core.DTOs.Cloud;

/// <summary>
/// Heartbeat message sent periodically over persistent connection.
/// </summary>
public record DeviceHeartbeatDto(
    string DeviceId,
    DateTime TimestampUtc,
    string Status);
