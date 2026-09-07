using System;
using MRPrintHub.Core.Enums;

namespace MRPrintHub.Core.DTOs.Cloud;

/// <summary>
/// Result returned to the mobile client after uploading a file.
/// </summary>
public record CloudUploadResultDto(
    bool Success,
    string UploadId,
    string OriginalFilename,
    long FileSizeBytes,
    CloudUploadStatus Status,
    string Message,
    DateTime TimestampUtc);
