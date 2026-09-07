namespace MRPrintHub.Database.Repositories;

public interface ISettingsRepository
{
    Task<string?> GetAsync(string key);
    Task<string> GetOrThrowAsync(string key);
    Task SetAsync(string key, string value);
    Task SetManyAsync(IEnumerable<KeyValuePair<string, string>> settings);
    Task<Dictionary<string, string>> GetAllAsync();
    Task RemoveAsync(string key);
}
