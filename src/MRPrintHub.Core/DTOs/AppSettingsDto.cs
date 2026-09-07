namespace MRPrintHub.Core.DTOs;

public record AppSettingsDto(
    string StoragePath,
    long MaxFileSizeBytes,
    long LowDiskThresholdBytes,
    int SessionTtlMinutes,
    int Port,
    string[] AllowedExtensions);
