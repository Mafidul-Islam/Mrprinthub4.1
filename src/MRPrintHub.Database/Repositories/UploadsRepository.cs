using Microsoft.EntityFrameworkCore;
using MRPrintHub.Core.Enums;
using MRPrintHub.Database.Entities;

namespace MRPrintHub.Database.Repositories;

public class UploadsRepository : IUploadsRepository
{
    private readonly AppDbContext _db;

    public UploadsRepository(AppDbContext db) => _db = db;

    public async Task<UploadEntity> CreateAsync(int sessionId, string originalFilename, string storedFilename, long size)
    {
        var upload = new UploadEntity
        {
            SessionId = sessionId,
            OriginalFilename = originalFilename,
            StoredFilename = storedFilename,
            Size = size,
            Status = UploadStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _db.Uploads.Add(upload);
        await _db.SaveChangesAsync();
        return upload;
    }

    public async Task UpdateStatusAsync(int uploadId, UploadStatus status, string? errorReason = null)
    {
        var upload = await _db.Uploads.FindAsync(uploadId);
        if (upload is not null)
        {
            upload.Status = status;
            upload.ErrorReason = errorReason;
            if (status is UploadStatus.Complete or UploadStatus.Failed)
                upload.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    public async Task MarkCompleteAsync(int uploadId)
    {
        var upload = await _db.Uploads.FindAsync(uploadId);
        if (upload is not null)
        {
            upload.Status = UploadStatus.Complete;
            upload.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<List<UploadEntity>> GetBySessionAsync(int sessionId)
    {
        return await _db.Uploads
            .AsNoTracking()
            .Where(u => u.SessionId == sessionId)
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<UploadEntity>> GetAllAsync(int maxCount = 100)
    {
        return await _db.Uploads
            .AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .Take(maxCount)
            .ToListAsync();
    }

    public async Task<UploadEntity?> GetByIdAsync(int uploadId)
    {
        return await _db.Uploads
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == uploadId);
    }
}
