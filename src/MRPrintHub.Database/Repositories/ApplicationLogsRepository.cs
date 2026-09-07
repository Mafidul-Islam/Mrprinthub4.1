using Microsoft.EntityFrameworkCore;
using MRPrintHub.Database.Entities;

namespace MRPrintHub.Database.Repositories;

public class ApplicationLogsRepository : IApplicationLogsRepository
{
    private readonly AppDbContext _db;

    public ApplicationLogsRepository(AppDbContext db) => _db = db;

    public async Task LogAsync(string eventTitle, string message, string level, string? details = null)
    {
        _db.ApplicationLogs.Add(new ApplicationLogEntity
        {
            Event = eventTitle,
            Message = message,
            Level = level,
            Details = details,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    public async Task<List<ApplicationLogEntity>> GetAllAsync(int maxCount = 200)
    {
        return await _db.ApplicationLogs
            .AsNoTracking()
            .OrderByDescending(l => l.CreatedAt)
            .Take(maxCount)
            .ToListAsync();
    }

    public async Task PruneAsync(DateTime olderThan)
    {
        var stale = await _db.ApplicationLogs
            .Where(l => l.CreatedAt < olderThan)
            .ToListAsync();
        _db.ApplicationLogs.RemoveRange(stale);
        await _db.SaveChangesAsync();
    }
}
