using MRPrintHub.Database.Entities;

namespace MRPrintHub.Database.Repositories;

public interface IApplicationLogsRepository
{
    Task LogAsync(string eventTitle, string message, string level, string? details = null);
    Task<List<ApplicationLogEntity>> GetAllAsync(int maxCount = 200);
    Task PruneAsync(DateTime olderThan);
}
