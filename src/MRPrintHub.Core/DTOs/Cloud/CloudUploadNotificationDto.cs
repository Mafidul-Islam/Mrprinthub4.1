using System;

namespace MRPrintHub.Core.DTOs.Cloud;

/// <summary>
/// Notification pushed to the Desktop PC when a customer uploads a file to the Cloud.
/// </summary>
public record CloudUploadNotificationDto(
    string UploadId,
    string SessionId,
    string ShopCode,
    string OriginalFilename,
    long FileSizeBytes,
    string ContentType,
    string DownloadToken,
    DateTime CreatedAt);
