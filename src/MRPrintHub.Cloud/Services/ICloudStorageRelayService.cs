using System;
using System.IO;
using System.Threading.Tasks;

namespace MRPrintHub.Cloud.Services;

public interface ICloudStorageRelayService
{
    Task<string> SaveTemporaryFileAsync(string uploadId, Stream fileStream);
    Task<Stream?> GetTemporaryFileStreamAsync(string uploadId);
    bool DeleteTemporaryFile(string uploadId);
    int PurgeExpiredFiles(Func<string, bool> isExpiredPredicate);
}

public class CloudStorageRelayService : ICloudStorageRelayService
{
    private readonly string _storageRoot;

    public CloudStorageRelayService(string? customRoot = null)
    {
        var envStorage = Environment.GetEnvironmentVariable("TEMP_STORAGE_PATH");
        _storageRoot = customRoot ?? envStorage ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TempCloudStorage");
        if (!Directory.Exists(_storageRoot))
        {
            Directory.CreateDirectory(_storageRoot);
        }
    }

    public async Task<string> SaveTemporaryFileAsync(string uploadId, Stream fileStream)
    {
        // Use sanitized uploadId as the physical filename to completely eliminate path traversal
        var safeFileName = $"{uploadId}.dat";
        var targetPath = Path.Combine(_storageRoot, safeFileName);

        await using var outputStream = File.Create(targetPath);
        await fileStream.CopyToAsync(outputStream);

        return safeFileName;
    }

    public Task<Stream?> GetTemporaryFileStreamAsync(string uploadId)
    {
        var safeFileName = $"{uploadId}.dat";
        var targetPath = Path.Combine(_storageRoot, safeFileName);

        if (!File.Exists(targetPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream fileStream = new FileStream(targetPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(fileStream);
    }

    public bool DeleteTemporaryFile(string uploadId)
    {
        try
        {
            var safeFileName = $"{uploadId}.dat";
            var targetPath = Path.Combine(_storageRoot, safeFileName);

            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
                return true;
            }
        }
        catch
        {
            // Ignore delete error
        }

        return false;
    }

    public int PurgeExpiredFiles(Func<string, bool> isExpiredPredicate)
    {
        int count = 0;
        try
        {
            if (Directory.Exists(_storageRoot))
            {
                foreach (var file in Directory.GetFiles(_storageRoot, "*.dat"))
                {
                    var uploadId = Path.GetFileNameWithoutExtension(file);
                    if (isExpiredPredicate(uploadId))
                    {
                        try
                        {
                            File.Delete(file);
                            count++;
                        }
                        catch { }
                    }
                }
            }
        }
        catch { }

        return count;
    }
}
