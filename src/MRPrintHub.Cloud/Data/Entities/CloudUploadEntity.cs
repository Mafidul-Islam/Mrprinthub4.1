using System;
using MRPrintHub.Core.Enums;

namespace MRPrintHub.Cloud.Data.Entities;

/// <summary>
/// Represents a file uploaded to Cloud temporary storage awaiting relay to Desktop.
/// </summary>
public class CloudUploadEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Unique public upload identifier (e.g. UPL-FILE-XXXXXXXX).
    /// </summary>
    public string UploadId { get; set; } = string.Empty;

    public Guid SessionId { get; set; }

    public UploadSessionEntity Session { get; set; } = null!;

    public Guid ShopId { get; set; }

    public ShopEntity Shop { get; set; } = null!;

    public string OriginalFilename { get; set; } = string.Empty;

    public string StoredFilename { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// One-time secure download authorization token.
    /// </summary>
    public string DownloadToken { get; set; } = string.Empty;

    public CloudUploadStatus Status { get; set; } = CloudUploadStatus.Uploaded;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; }

    public DateTime? DeliveredAt { get; set; }

    public string? DeliveredToDeviceId { get; set; }
}
