using MRPrintHub.Core.Enums;
using MRPrintHub.Database.Entities;

namespace MRPrintHub.Database.Repositories;

public interface IUploadsRepository
{
    Task<UploadEntity> CreateAsync(int sessionId, string originalFilename, string storedFilename, long size);
    Task UpdateStatusAsync(int uploadId, UploadStatus status, string? errorReason = null);
    Task MarkCompleteAsync(int uploadId);
    Task<List<UploadEntity>> GetBySessionAsync(int sessionId);
    Task<List<UploadEntity>> GetAllAsync(int maxCount = 100);
    Task<UploadEntity?> GetByIdAsync(int uploadId);
}
