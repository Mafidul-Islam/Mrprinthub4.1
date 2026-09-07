using System;

namespace MRPrintHub.Core.DTOs.Cloud;

/// <summary>
/// Delivery acknowledgement sent by Desktop back to Cloud after saving a file to disk.
/// </summary>
public record CloudUploadAckDto(
    string UploadId,
    string DeviceId,
    bool Success,
    string? ErrorMessage,
    DateTime AcknowledgedAtUtc);
