using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using MRPrintHub.Core.DTOs;
using MRPrintHub.Core.Enums;

namespace MRPrintHub.Storage;

/// <summary>
/// Production disk-backed implementation of IStorageService for upload pipeline.
/// </summary>
public class StorageService : IStorageService
{
    private readonly ConcurrentDictionary<string, UploadInfoDto> _uploads = new();
    private readonly ConcurrentDictionary<string, UploadInfoDto> _processedUploadsById = new();
    private readonly string _storageDir;

    public StorageService()
    {
        _storageDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "MRPrintHub",
            "Received");

        try
        {
            if (!Directory.Exists(_storageDir))
            {
                Directory.CreateDirectory(_storageDir);
            }
        }
        catch
        {
            // Fallback to local Received folder if ProgramData is unavailable
            _storageDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Received");
            if (!Directory.Exists(_storageDir))
            {
                Directory.CreateDirectory(_storageDir);
            }
        }
    }

    public bool IsUploadProcessed(string uploadId)
    {
        if (string.IsNullOrWhiteSpace(uploadId))
            return false;

        return _processedUploadsById.ContainsKey(uploadId.Trim());
    }

    public async Task<UploadInfoDto> SaveAsync(string token, string originalFilename, byte[] fileData, string? uploadId = null)
    {
        if (string.IsNullOrEmpty(token))
            throw new ArgumentException("Token is required", nameof(token));

        // Idempotency: Duplicate protection
        if (!string.IsNullOrWhiteSpace(uploadId) && _processedUploadsById.TryGetValue(uploadId.Trim(), out var existingDto))
        {
            return existingDto;
        }

        if (fileData == null || fileData.Length == 0)
        {
            var failedDto = new UploadInfoDto(
                Id: Guid.NewGuid().ToString(),
                OriginalFilename: originalFilename ?? "unknown",
                StoredFilename: string.Empty,
                Size: 0,
                Status: UploadStatus.Failed,
                CreatedAt: DateTime.UtcNow,
                CompletedAt: DateTime.UtcNow,
                ErrorReason: "No file received",
                Token: token
            );
            return failedDto;
        }

        var safeName = Path.GetFileName(originalFilename ?? "upload.bin");
        var nameWithoutExt = Path.GetFileNameWithoutExtension(safeName);
        var ext = Path.GetExtension(safeName);
        var targetPath = Path.Combine(_storageDir, safeName);

        // Resolve collision with (1), (2)...
        int counter = 1;
        while (File.Exists(targetPath))
        {
            targetPath = Path.Combine(_storageDir, $"{nameWithoutExt} ({counter++}){ext}");
        }

        // Persist bytes to disk
        await File.WriteAllBytesAsync(targetPath, fileData);

        var dto = new UploadInfoDto(
            Id: uploadId ?? Guid.NewGuid().ToString(),
            OriginalFilename: safeName,
            StoredFilename: Path.GetFileName(targetPath),
            Size: fileData.Length,
            Status: UploadStatus.Complete,
            CreatedAt: DateTime.UtcNow,
            CompletedAt: DateTime.UtcNow,
            ErrorReason: null,
            Token: token
        );

        _uploads[token] = dto;
        if (!string.IsNullOrWhiteSpace(uploadId))
        {
            _processedUploadsById[uploadId.Trim()] = dto;
        }

        return dto;
    }

    public UploadStatus GetUploadStatus(string token)
    {
        if (_uploads.TryGetValue(token, out var dto))
            return dto.Status;

        return UploadStatus.Failed;
    }

    public string GetAllTokenStatuses()
    {
        return string.Join(",", _uploads.Keys);
    }
}
