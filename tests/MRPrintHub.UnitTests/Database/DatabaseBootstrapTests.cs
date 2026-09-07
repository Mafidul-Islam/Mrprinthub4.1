using Microsoft.EntityFrameworkCore;
using MRPrintHub.Database;
using MRPrintHub.Database.Entities;
using Xunit;

namespace MRPrintHub.UnitTests.Database;

public class DatabaseBootstrapTests
{
    private static string CreateTempDbPath()
    {
        return Path.Combine(Path.GetTempPath(), $"mrprinthub_test_{Guid.NewGuid():N}.db");
    }

    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task InitializeAsync_CreatesDatabase()
    {
        var dbPath = CreateTempDbPath();
        try
        {
            await DatabaseBootstrapper.InitializeAsync(dbPath);

            Assert.True(File.Exists(dbPath));

            // Verify we can open the database
            await using var ctx = DatabaseBootstrapper.CreateDbContext(dbPath);
            Assert.True(await ctx.Database.CanConnectAsync());
        }
        finally
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            try { File.Delete(dbPath); } catch { }
            try { File.Delete(dbPath + "-wal"); } catch { }
            try { File.Delete(dbPath + "-shm"); } catch { }
        }
    }

    [Fact]
    public async Task SettingsRepository_CRUD()
    {
        using var db = CreateInMemoryContext();
        var repo = new MRPrintHub.Database.Repositories.SettingsRepository(db);

        await repo.SetAsync("key1", "value1");
        var val = await repo.GetAsync("key1");
        Assert.Equal("value1", val);

        await repo.SetAsync("key1", "updated");
        val = await repo.GetAsync("key1");
        Assert.Equal("updated", val);

        var all = await repo.GetAllAsync();
        Assert.Single(all);
        Assert.Equal("updated", all["key1"]);

        await repo.RemoveAsync("key1");
        val = await repo.GetAsync("key1");
        Assert.Null(val);
    }

    [Fact]
    public async Task SettingsRepository_GetOrThrow_ThrowsWhenMissing()
    {
        using var db = CreateInMemoryContext();
        var repo = new MRPrintHub.Database.Repositories.SettingsRepository(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => repo.GetOrThrowAsync("missing"));
    }

    [Fact]
    public async Task SettingsRepository_SetMany_BatchSetsMultiple()
    {
        using var db = CreateInMemoryContext();
        var repo = new MRPrintHub.Database.Repositories.SettingsRepository(db);

        var settings = new Dictionary<string, string>
        {
            ["a"] = "1",
            ["b"] = "2",
            ["c"] = "3"
        };

        await repo.SetManyAsync(settings);
        var all = await repo.GetAllAsync();

        Assert.Equal(3, all.Count);
        Assert.Equal("1", all["a"]);
        Assert.Equal("2", all["b"]);
        Assert.Equal("3", all["c"]);
    }

    [Fact]
    public async Task SessionsRepository_CreateAndRetrieve()
    {
        using var db = CreateInMemoryContext();
        var repo = new MRPrintHub.Database.Repositories.SessionsRepository(db);

        var session = await repo.CreateAsync("hash1", "token1", "192.168.1.1", "Wi-Fi", DateTime.UtcNow.AddMinutes(30));

        Assert.Equal("hash1", session.TokenHash);
        Assert.Equal("192.168.1.1", session.IpAddress);
        Assert.False(session.Revoked);

        var retrieved = await repo.GetActiveByTokenHashAsync("hash1");
        Assert.NotNull(retrieved);
        Assert.Equal(session.Id, retrieved.Id);
    }

    [Fact]
    public async Task SessionsRepository_RevokeWorks()
    {
        using var db = CreateInMemoryContext();
        var repo = new MRPrintHub.Database.Repositories.SessionsRepository(db);

        var session = await repo.CreateAsync("hash1", "token1", "192.168.1.1", "Wi-Fi", DateTime.UtcNow.AddMinutes(30));
        await repo.RevokeAsync(session.Id);

        var active = await repo.GetActiveByTokenHashAsync("hash1");
        Assert.Null(active);

        var revoked = await repo.GetByTokenHashAsync("hash1");
        Assert.NotNull(revoked);
        Assert.True(revoked.Revoked);
    }

    [Fact]
    public async Task SessionsRepository_ExpiredSessionsRevoked()
    {
        using var db = CreateInMemoryContext();
        var repo = new MRPrintHub.Database.Repositories.SessionsRepository(db);

        await repo.CreateAsync("expired1", "tok1", "10.0.0.1", "Eth", DateTime.UtcNow.AddMinutes(-10));
        await repo.CreateAsync("active1", "tok2", "10.0.0.1", "Eth", DateTime.UtcNow.AddMinutes(30));

        var revokedCount = await repo.RevokeExpiredAsync(DateTime.UtcNow);

        Assert.Equal(1, revokedCount);

        var active = await repo.GetActiveByTokenHashAsync("active1");
        Assert.NotNull(active);
    }

    [Fact]
    public async Task UploadsRepository_CreateAndComplete()
    {
        using var db = CreateInMemoryContext();
        var sessionRepo = new MRPrintHub.Database.Repositories.SessionsRepository(db);
        var uploadRepo = new MRPrintHub.Database.Repositories.UploadsRepository(db);

        var session = await sessionRepo.CreateAsync("hash", "tok", "10.0.0.1", "Wi-Fi", DateTime.UtcNow.AddMinutes(30));
        var upload = await uploadRepo.CreateAsync(session.Id, "photo.jpg", "abc123.jpg", 1024);

        Assert.Equal("photo.jpg", upload.OriginalFilename);
        Assert.Equal(MRPrintHub.Core.Enums.UploadStatus.Pending, upload.Status);

        await uploadRepo.MarkCompleteAsync(upload.Id);
        var completed = await uploadRepo.GetByIdAsync(upload.Id);
        Assert.NotNull(completed);
        Assert.Equal(MRPrintHub.Core.Enums.UploadStatus.Complete, completed.Status);
        Assert.NotNull(completed.CompletedAt);
    }

    [Fact]
    public async Task ApplicationLogsRepository_LogAndRetrieve()
    {
        using var db = CreateInMemoryContext();
        var repo = new MRPrintHub.Database.Repositories.ApplicationLogsRepository(db);

        await repo.LogAsync("upload", "File received", "Info");
        await repo.LogAsync("error", "Something failed", "Error", "stack trace");

        var logs = await repo.GetAllAsync();
        Assert.Equal(2, logs.Count);
        Assert.Equal("error", logs[0].Event);
    }

    [Fact]
    public async Task ApplicationLogsRepository_PruneOldLogs()
    {
        using var db = CreateInMemoryContext();
        var repo = new MRPrintHub.Database.Repositories.ApplicationLogsRepository(db);

        // Insert an old log directly with a past timestamp
        db.ApplicationLogs.Add(new ApplicationLogEntity
        {
            Event = "old",
            Message = "old message",
            Level = "Info",
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        });
        await db.SaveChangesAsync();

        await repo.LogAsync("new", "new message", "Info");

        var all = await repo.GetAllAsync();
        Assert.Equal(2, all.Count);

        await repo.PruneAsync(DateTime.UtcNow.AddMinutes(-5));

        var remaining = await repo.GetAllAsync();
        Assert.Single(remaining);
        Assert.Equal("new", remaining[0].Event);
    }
}
