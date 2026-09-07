using System;
using System.Collections.Generic;
using MRPrintHub.Core.Enums;

namespace MRPrintHub.Cloud.Data.Entities;

/// <summary>
/// Represents a temporary customer upload session initiated via QR code.
/// </summary>
public class UploadSessionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Unique public session identifier (e.g. UPL-92KX7P3M).
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    public Guid ShopId { get; set; }

    public ShopEntity Shop { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; }

    public CloudUploadStatus Status { get; set; } = CloudUploadStatus.Created;

    public ICollection<CloudUploadEntity> Uploads { get; set; } = new List<CloudUploadEntity>();
}
