namespace MRPrintHub.Database.Entities;

public class ApplicationLogEntity
{
    public int Id { get; set; }
    public required string Event { get; set; }
    public required string Message { get; set; }
    public required string Level { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; }
}
