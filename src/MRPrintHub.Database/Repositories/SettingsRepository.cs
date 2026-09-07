using Microsoft.EntityFrameworkCore;
using MRPrintHub.Database.Entities;

namespace MRPrintHub.Database.Repositories;

public class SettingsRepository : ISettingsRepository
{
    private readonly AppDbContext _db;

    public SettingsRepository(AppDbContext db) => _db = db;

    public async Task<string?> GetAsync(string key)
    {
        var entity = await _db.Settings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key);
        return entity?.Value;
    }

    public async Task<string> GetOrThrowAsync(string key)
    {
        var value = await GetAsync(key)
            ?? throw new KeyNotFoundException($"Setting '{key}' not found.");
        return value;
    }

    public async Task SetAsync(string key, string value)
    {
        var now = DateTime.UtcNow;
        var entity = await _db.Settings.FirstOrDefaultAsync(s => s.Key == key);

        if (entity is null)
        {
            _db.Settings.Add(new SettingsEntity
            {
                Key = key,
                Value = value,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            entity.Value = value;
            entity.UpdatedAt = now;
        }

        await _db.SaveChangesAsync();
    }

    public async Task SetManyAsync(IEnumerable<KeyValuePair<string, string>> settings)
    {
        var now = DateTime.UtcNow;
        foreach (var kv in settings)
        {
            var entity = await _db.Settings.FirstOrDefaultAsync(s => s.Key == kv.Key);
            if (entity is null)
            {
                _db.Settings.Add(new SettingsEntity
                {
                    Key = kv.Key,
                    Value = kv.Value,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            else
            {
                entity.Value = kv.Value;
                entity.UpdatedAt = now;
            }
        }

        await _db.SaveChangesAsync();
    }

    public async Task<Dictionary<string, string>> GetAllAsync()
    {
        return await _db.Settings
            .AsNoTracking()
            .ToDictionaryAsync(s => s.Key, s => s.Value);
    }

    public async Task RemoveAsync(string key)
    {
        var entity = await _db.Settings.FirstOrDefaultAsync(s => s.Key == key);
        if (entity is not null)
        {
            _db.Settings.Remove(entity);
            await _db.SaveChangesAsync();
        }
    }
}
