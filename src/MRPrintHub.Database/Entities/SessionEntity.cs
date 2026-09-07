namespace MRPrintHub.Database.Entities;

public class SessionEntity
{
    public int Id { get; set; }
    public required string TokenHash { get; set; }
    public required string TokenPrefix { get; set; }
    public required string IpAddress { get; set; }
    public required string InterfaceName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool Revoked { get; set; }
}
