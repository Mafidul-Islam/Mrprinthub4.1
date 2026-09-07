using System;
using MRPrintHub.Core.Enums;

namespace MRPrintHub.Core.DTOs.Cloud;

/// <summary>
/// Represents an active customer upload session opened via QR code.
/// </summary>
public record CloudUploadSessionDto(
    string SessionId,
    string ShopCode,
    string? ShopName,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    CloudUploadStatus Status);
