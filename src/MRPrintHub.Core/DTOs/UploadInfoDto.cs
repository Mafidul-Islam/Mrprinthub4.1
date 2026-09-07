namespace MRPrintHub.Core.DTOs;

using MRPrintHub.Core.Enums;

public record UploadInfoDto(
    string Id,
    string OriginalFilename,
    string StoredFilename,
    long Size,
    UploadStatus Status,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string? ErrorReason,
    string? Token);
