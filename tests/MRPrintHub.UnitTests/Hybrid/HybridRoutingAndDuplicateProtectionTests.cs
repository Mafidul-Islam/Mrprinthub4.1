using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MRPrintHub.Cloud.Data;
using MRPrintHub.Cloud.Services;
using MRPrintHub.Storage;
using MRPrintHub.UnitTests.Cloud.Fakes;
using Xunit;

namespace MRPrintHub.UnitTests.Hybrid;

public class HybridRoutingAndDuplicateProtectionTests
{
    private static CloudDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<CloudDbContext>()
            .UseInMemoryDatabase(databaseName: $"HybridDb_Test_{Guid.NewGuid():N}")
            .Options;
        return new CloudDbContext(options);
    }

    [Fact]
    public async Task StorageService_SaveAsync_WithDuplicateUploadId_PreventsDuplicateFile()
    {
        var storage = new StorageService();
        var uploadId = $"UPL-20260905-{Guid.NewGuid():N}";
        var data = "Unique file content for duplicate test"u8.ToArray();

        // 1st Save
        var firstResult = await storage.SaveAsync("token-123", "invoice.pdf", data, uploadId);
        Assert.NotNull(firstResult);
        Assert.Equal(uploadId, firstResult.Id);
        Assert.True(storage.IsUploadProcessed(uploadId));

        // 2nd Save with same uploadId
        var secondResult = await storage.SaveAsync("token-123", "invoice.pdf", data, uploadId);
        Assert.NotNull(secondResult);
        Assert.Equal(firstResult.Id, secondResult.Id);
        Assert.Equal(firstResult.StoredFilename, secondResult.StoredFilename);
    }

    [Fact]
    public async Task CloudUploadService_ProcessFileUploadAsync_WithDuplicateUploadId_ReturnsExistingResult()
    {
        using var db = CreateInMemoryDbContext();
        var codeGen = new ShopCodeGenerator();
        var shopService = new ShopService(db, codeGen);
        var tempFolder = Path.Combine(Path.GetTempPath(), $"Relay_Dup_{Guid.NewGuid():N}");
        var storageRelay = new CloudStorageRelayService(tempFolder);
        var hubContext = new FakeDeviceHubContext();
        var uploadService = new CloudUploadService(db, storageRelay, shopService, hubContext);

        var shop = await shopService.CreateShopAsync(null, "Hybrid Shop");
        var session = await uploadService.CreateSessionAsync(shop.ShopCode);

        var customUploadId = $"UPL-20260905-{Guid.NewGuid():N}";
        var content = "PDF bytes"u8.ToArray();

        using var ms1 = new MemoryStream(content);
        var res1 = await uploadService.ProcessFileUploadAsync(session!.SessionId, "doc.pdf", "application/pdf", content.Length, ms1, customUploadId);
        Assert.True(res1.Success);
        Assert.Equal(customUploadId, res1.UploadId);

        using var ms2 = new MemoryStream(content);
        var res2 = await uploadService.ProcessFileUploadAsync(session.SessionId, "doc.pdf", "application/pdf", content.Length, ms2, customUploadId);
        Assert.True(res2.Success);
        Assert.Equal(customUploadId, res2.UploadId);
        Assert.Equal("File already received.", res2.Message);
    }

    [Fact]
    public void StorageService_IsUploadProcessed_ReturnsFalseForUnseenId()
    {
        var storage = new StorageService();
        Assert.False(storage.IsUploadProcessed("UPL-NONEXISTENT"));
        Assert.False(storage.IsUploadProcessed(""));
    }
}
