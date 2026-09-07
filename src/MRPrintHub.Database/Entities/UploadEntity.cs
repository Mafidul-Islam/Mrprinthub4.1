using MRPrintHub.Core.Enums;

namespace MRPrintHub.Database.Entities;

public class UploadEntity
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public required string OriginalFilename { get; set; }
    public required string StoredFilename { get; set; }
    public long Size { get; set; }
    public UploadStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorReason { get; set; }

    public SessionEntity? Session { get; set; }
}
