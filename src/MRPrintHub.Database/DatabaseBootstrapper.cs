using Microsoft.EntityFrameworkCore;
using MRPrintHub.Database.Repositories;

namespace MRPrintHub.Database;

public static class DatabaseBootstrapper
{
    public static async Task InitializeAsync(string dbPath)
    {
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var connectionString = $"Data Source={dbPath}";
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite(connectionString);

        await using var db = new AppDbContext(optionsBuilder.Options);
        await db.Database.EnsureCreatedAsync();
        await db.Database.CloseConnectionAsync();
    }

    public static AppDbContext CreateDbContext(string dbPath)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
        return new AppDbContext(optionsBuilder.Options);
    }
}
