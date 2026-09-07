using Microsoft.EntityFrameworkCore;
using MRPrintHub.Database.Entities;

namespace MRPrintHub.Database.Repositories;

public class SessionsRepository : ISessionsRepository
{
    private readonly AppDbContext _db;

    public SessionsRepository(AppDbContext db) => _db = db;

    public async Task<SessionEntity?> GetByTokenHashAsync(string tokenHash)
    {
        return await _db.Sessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TokenHash == tokenHash);
    }

    public async Task<SessionEntity?> GetActiveByTokenHashAsync(string tokenHash)
    {
        return await _db.Sessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TokenHash == tokenHash && !s.Revoked && s.ExpiresAt > DateTime.UtcNow);
    }

    public async Task<SessionEntity> CreateAsync(string tokenHash, string tokenPrefix, string ipAddress, string interfaceName, DateTime expiresAt)
    {
        var session = new SessionEntity
        {
            TokenHash = tokenHash,
            TokenPrefix = tokenPrefix,
            IpAddress = ipAddress,
            InterfaceName = interfaceName,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            Revoked = false
        };

        _db.Sessions.Add(session);
        await _db.SaveChangesAsync();
        return session;
    }

    public async Task RevokeAsync(int sessionId)
    {
        var session = await _db.Sessions.FindAsync(sessionId);
        if (session is not null)
        {
            session.Revoked = true;
            await _db.SaveChangesAsync();
        }
    }

    public async Task RevokeAllAsync()
    {
        var sessions = await _db.Sessions.Where(s => !s.Revoked).ToListAsync();
        foreach (var session in sessions)
            session.Revoked = true;
        await _db.SaveChangesAsync();
    }

    public async Task<int> RevokeExpiredAsync(DateTime utcNow)
    {
        var sessions = await _db.Sessions
            .Where(s => !s.Revoked && s.ExpiresAt <= utcNow)
            .ToListAsync();
        foreach (var session in sessions)
            session.Revoked = true;
        await _db.SaveChangesAsync();
        return sessions.Count;
    }
}
