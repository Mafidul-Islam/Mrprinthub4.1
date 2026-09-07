using System.Threading.Tasks;
using MRPrintHub.Core.DTOs;
using MRPrintHub.Core.Enums;

namespace MRPrintHub.Storage;

/// <summary>
/// Interface for storage service handling upload file persistence with duplicate protection.
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Saves an uploaded file and returns a DTO with the upload status.
    /// Supports idempotency via unique uploadId to prevent duplicate file writes.
    /// </summary>
    Task<UploadInfoDto> SaveAsync(string token, string originalFilename, byte[] fileData, string? uploadId = null);

    /// <summary>
    /// Gets the current upload status for a given token.
    /// </summary>
    UploadStatus GetUploadStatus(string token);

    /// <summary>
    /// Gets all active tokens/uploads as a comma-separated string.
    /// </summary>
    string GetAllTokenStatuses();

    /// <summary>
    /// Checks if a given uploadId has already been persisted to prevent duplicate downloads.
    /// </summary>
    bool IsUploadProcessed(string uploadId);
}
