using MRPrintHub.Database.Entities;

namespace MRPrintHub.Database.Repositories;

public interface ISessionsRepository
{
    Task<SessionEntity?> GetByTokenHashAsync(string tokenHash);
    Task<SessionEntity?> GetActiveByTokenHashAsync(string tokenHash);
    Task<SessionEntity> CreateAsync(string tokenHash, string tokenPrefix, string ipAddress, string interfaceName, DateTime expiresAt);
    Task RevokeAsync(int sessionId);
    Task RevokeAllAsync();
    Task<int> RevokeExpiredAsync(DateTime utcNow);
}
