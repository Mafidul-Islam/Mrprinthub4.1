using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MRPrintHub.Cloud.Data;
using MRPrintHub.Cloud.Data.Entities;
using MRPrintHub.Cloud.Hubs;
using MRPrintHub.Core.DTOs.Cloud;
using MRPrintHub.Core.Enums;
using MRPrintHub.Security;

namespace MRPrintHub.Cloud.Services;

public interface ICloudUploadService
{
    Task<CloudUploadSessionDto?> CreateSessionAsync(string shopCode, TimeSpan? ttl = null);
    Task<CloudUploadSessionDto?> GetSessionAsync(string sessionId);
    Task<CloudUploadResultDto> ProcessFileUploadAsync(string sessionId, string rawFilename, string contentType, long size, Stream fileStream, string? customUploadId = null);
    Task<(CloudUploadEntity? Entity, Stream? FileStream)> GetDownloadAsync(string uploadId, string downloadToken);
    Task<bool> AcknowledgeDeliveryAsync(string uploadId, string deviceId);
    Task<IReadOnlyList<CloudUploadNotificationDto>> GetPendingUploadsForShopAsync(string shopCode);
    Task<int> PurgeExpiredUploadsAsync();
}

public class CloudUploadService : ICloudUploadService
{
    private static readonly string[] AllowedExtensions = [
        "pdf", "docx", "doc", "jpg", "jpeg", "png", "txt", "rtf", "xlsx", "xls", "csv", "pptx", "zip"
    ];

    private const long MaxFileSizeBytes = 100 * 1024 * 1024; // 100 MB max

    private readonly CloudDbContext _db;
    private readonly ICloudStorageRelayService _storageRelay;
    private readonly IShopService _shopService;
    private readonly IHubContext<DeviceHub> _hubContext;

    public CloudUploadService(
        CloudDbContext db,
        ICloudStorageRelayService storageRelay,
        IShopService shopService,
        IHubContext<DeviceHub> hubContext)
    {
        _db = db;
        _storageRelay = storageRelay;
        _shopService = shopService;
        _hubContext = hubContext;
    }

    public async Task<CloudUploadSessionDto?> CreateSessionAsync(string shopCode, TimeSpan? ttl = null)
    {
        var shop = await _shopService.GetByCodeAsync(shopCode);
        if (shop == null || !shop.IsActive)
            return null;

        var sessionId = GenerateId("UPL", 8);
        var duration = ttl ?? TimeSpan.FromMinutes(30);

        var session = new UploadSessionEntity
        {
            SessionId = sessionId,
            ShopId = shop.Id,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(duration),
            Status = CloudUploadStatus.Created
        };

        _db.UploadSessions.Add(session);
        await _db.SaveChangesAsync();

        return new CloudUploadSessionDto(
            SessionId: session.SessionId,
            ShopCode: shop.ShopCode,
            ShopName: shop.Name,
            CreatedAt: session.CreatedAt,
            ExpiresAt: session.ExpiresAt,
            Status: session.Status);
    }

    public async Task<CloudUploadSessionDto?> GetSessionAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return null;

        var session = await _db.UploadSessions
            .Include(s => s.Shop)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId.Trim());

        if (session == null)
            return null;

        if (session.ExpiresAt <= DateTime.UtcNow && session.Status != CloudUploadStatus.Expired)
        {
            session.Status = CloudUploadStatus.Expired;
            await _db.SaveChangesAsync();
        }

        return new CloudUploadSessionDto(
            SessionId: session.SessionId,
            ShopCode: session.Shop.ShopCode,
            ShopName: session.Shop.Name,
            CreatedAt: session.CreatedAt,
            ExpiresAt: session.ExpiresAt,
            Status: session.Status);
    }

    public async Task<CloudUploadResultDto> ProcessFileUploadAsync(
        string sessionId,
        string rawFilename,
        string contentType,
        long size,
        Stream fileStream,
        string? customUploadId = null)
    {
        // 1. Validate Session
        var session = await _db.UploadSessions
            .Include(s => s.Shop)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId.Trim());

        if (session == null)
        {
            return FailureResult("Invalid session token.");
        }

        if (session.ExpiresAt <= DateTime.UtcNow || session.Status == CloudUploadStatus.Expired)
        {
            return FailureResult("Upload session has expired. Please re-scan the shop QR code.");
        }

        // 2. Idempotency Check on Custom UploadId
        if (!string.IsNullOrWhiteSpace(customUploadId))
        {
            var existing = await _db.Uploads.FirstOrDefaultAsync(u => u.UploadId == customUploadId.Trim());
            if (existing != null)
            {
                return new CloudUploadResultDto(
                    Success: true,
                    UploadId: existing.UploadId,
                    OriginalFilename: existing.OriginalFilename,
                    FileSizeBytes: existing.FileSizeBytes,
                    Status: existing.Status,
                    Message: "File already received.",
                    TimestampUtc: existing.CreatedAt);
            }
        }

        // 3. Validate Size
        if (size <= 0)
        {
            return FailureResult("File is empty.");
        }

        if (size > MaxFileSizeBytes)
        {
            return FailureResult($"File size exceeds the maximum limit of {MaxFileSizeBytes / (1024 * 1024)} MB.");
        }

        // 4. Sanitize and Validate Filename & Extension
        var sanitizedFilename = FilenameSanitizer.Sanitize(rawFilename);
        if (string.IsNullOrWhiteSpace(sanitizedFilename) || !PathGuard.IsSafeFilename(sanitizedFilename))
        {
            sanitizedFilename = $"upload_{DateTime.UtcNow.Ticks}.dat";
        }

        if (!ExtensionValidator.IsAllowed(sanitizedFilename, AllowedExtensions))
        {
            return FailureResult($"Unsupported file type. Allowed extensions: {string.Join(", ", AllowedExtensions)}");
        }

        // 5. Save to Cloud Temporary Storage
        var uploadId = !string.IsNullOrWhiteSpace(customUploadId) ? customUploadId.Trim() : GenerateId("UPL-FILE", 10);
        var downloadToken = GenerateToken();

        var storedFilename = await _storageRelay.SaveTemporaryFileAsync(uploadId, fileStream);

        // 6. Create Cloud Upload Entity
        var uploadEntity = new CloudUploadEntity
        {
            UploadId = uploadId,
            SessionId = session.Id,
            ShopId = session.ShopId,
            OriginalFilename = sanitizedFilename,
            StoredFilename = storedFilename,
            FileSizeBytes = size,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            DownloadToken = downloadToken,
            Status = CloudUploadStatus.Uploaded,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24) // 24h retention window for delivery
        };

        _db.Uploads.Add(uploadEntity);
        await _db.SaveChangesAsync();

        // 7. Notify Connected Desktop PC via SignalR
        var notification = new CloudUploadNotificationDto(
            UploadId: uploadEntity.UploadId,
            SessionId: session.SessionId,
            ShopCode: session.Shop.ShopCode,
            OriginalFilename: uploadEntity.OriginalFilename,
            FileSizeBytes: uploadEntity.FileSizeBytes,
            ContentType: uploadEntity.ContentType,
            DownloadToken: uploadEntity.DownloadToken,
            CreatedAt: uploadEntity.CreatedAt);

        await _hubContext.Clients.Group($"shop_{session.Shop.ShopCode}")
            .SendAsync("OnNewUploadAvailable", notification);

        return new CloudUploadResultDto(
            Success: true,
            UploadId: uploadEntity.UploadId,
            OriginalFilename: uploadEntity.OriginalFilename,
            FileSizeBytes: uploadEntity.FileSizeBytes,
            Status: CloudUploadStatus.Uploaded,
            Message: "File successfully uploaded to cloud relay.",
            TimestampUtc: DateTime.UtcNow);
    }

    public async Task<(CloudUploadEntity? Entity, Stream? FileStream)> GetDownloadAsync(string uploadId, string downloadToken)
    {
        if (string.IsNullOrWhiteSpace(uploadId) || string.IsNullOrWhiteSpace(downloadToken))
            return (null, null);

        var upload = await _db.Uploads
            .Include(u => u.Shop)
            .FirstOrDefaultAsync(u => u.UploadId == uploadId.Trim());

        if (upload == null || upload.Status == CloudUploadStatus.Delivered || upload.Status == CloudUploadStatus.Expired)
            return (null, null);

        if (upload.DownloadToken != downloadToken.Trim())
            return (null, null);

        var stream = await _storageRelay.GetTemporaryFileStreamAsync(upload.UploadId);
        if (stream == null)
            return (null, null);

        upload.Status = CloudUploadStatus.Delivering;
        await _db.SaveChangesAsync();

        return (upload, stream);
    }

    public async Task<bool> AcknowledgeDeliveryAsync(string uploadId, string deviceId)
    {
        if (string.IsNullOrWhiteSpace(uploadId))
            return false;

        var upload = await _db.Uploads.FirstOrDefaultAsync(u => u.UploadId == uploadId.Trim());
        if (upload == null)
            return false;

        upload.Status = CloudUploadStatus.Delivered;
        upload.DeliveredAt = DateTime.UtcNow;
        upload.DeliveredToDeviceId = deviceId;

        // Immediate cleanup of temporary customer file from Cloud storage
        _storageRelay.DeleteTemporaryFile(upload.UploadId);

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<IReadOnlyList<CloudUploadNotificationDto>> GetPendingUploadsForShopAsync(string shopCode)
    {
        if (string.IsNullOrWhiteSpace(shopCode))
            return Array.Empty<CloudUploadNotificationDto>();

        var pendingUploads = await _db.Uploads
            .Include(u => u.Session)
            .Include(u => u.Shop)
            .Where(u => u.Shop.ShopCode == shopCode.Trim().ToUpperInvariant() &&
                        (u.Status == CloudUploadStatus.Uploaded || u.Status == CloudUploadStatus.Delivering) &&
                        u.ExpiresAt > DateTime.UtcNow)
            .OrderBy(u => u.CreatedAt)
            .ToListAsync();

        return pendingUploads.Select(u => new CloudUploadNotificationDto(
            UploadId: u.UploadId,
            SessionId: u.Session?.SessionId ?? string.Empty,
            ShopCode: u.Shop.ShopCode,
            OriginalFilename: u.OriginalFilename,
            FileSizeBytes: u.FileSizeBytes,
            ContentType: u.ContentType,
            DownloadToken: u.DownloadToken,
            CreatedAt: u.CreatedAt)).ToList();
    }

    public async Task<int> PurgeExpiredUploadsAsync()
    {
        var expiredList = await _db.Uploads
            .Where(u => u.Status != CloudUploadStatus.Delivered && u.ExpiresAt <= DateTime.UtcNow)
            .ToListAsync();

        foreach (var item in expiredList)
        {
            item.Status = CloudUploadStatus.Expired;
            _storageRelay.DeleteTemporaryFile(item.UploadId);
        }

        await _db.SaveChangesAsync();
        return expiredList.Count;
    }

    private static CloudUploadResultDto FailureResult(string message) => new(
        Success: false,
        UploadId: string.Empty,
        OriginalFilename: string.Empty,
        FileSizeBytes: 0,
        Status: CloudUploadStatus.Failed,
        Message: message,
        TimestampUtc: DateTime.UtcNow);

    private static string GenerateId(string prefix, int length)
    {
        const string chars = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
        Span<byte> randomBytes = stackalloc byte[length];
        RandomNumberGenerator.Fill(randomBytes);

        var result = new char[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = chars[randomBytes[i] % chars.Length];
        }

        return $"{prefix}-{new string(result)}";
    }

    private static string GenerateToken()
    {
        byte[] bytes = new byte[24];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
