using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MRPrintHub.Cloud.Data;
using MRPrintHub.Cloud.Services;
using MRPrintHub.Core.Enums;
using MRPrintHub.UnitTests.Cloud.Fakes;
using Xunit;

namespace MRPrintHub.UnitTests.Cloud;

public class CloudUploadServiceTests
{
    private static CloudDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<CloudDbContext>()
            .UseInMemoryDatabase(databaseName: $"CloudUploadDb_Test_{Guid.NewGuid():N}")
            .Options;
        return new CloudDbContext(options);
    }

    [Fact]
    public async Task CreateSessionAsync_GeneratesActiveSessionWithExpiry()
    {
        using var db = CreateInMemoryDbContext();
        var codeGen = new ShopCodeGenerator();
        var shopService = new ShopService(db, codeGen);
        var storageRelay = new CloudStorageRelayService(Path.Combine(Path.GetTempPath(), $"Relay_{Guid.NewGuid():N}"));
        var hubContext = new FakeDeviceHubContext();
        var uploadService = new CloudUploadService(db, storageRelay, shopService, hubContext);

        var shop = await shopService.CreateShopAsync(null, "Test Print Shop");

        var session = await uploadService.CreateSessionAsync(shop.ShopCode, TimeSpan.FromMinutes(15));

        Assert.NotNull(session);
        Assert.StartsWith("UPL-", session.SessionId);
        Assert.Equal(shop.ShopCode, session.ShopCode);
        Assert.Equal(CloudUploadStatus.Created, session.Status);
        Assert.True(session.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task ProcessFileUploadAsync_SavesFile_AndGeneratesRelayRecord()
    {
        using var db = CreateInMemoryDbContext();
        var codeGen = new ShopCodeGenerator();
        var shopService = new ShopService(db, codeGen);
        var tempFolder = Path.Combine(Path.GetTempPath(), $"Relay_{Guid.NewGuid():N}");
        var storageRelay = new CloudStorageRelayService(tempFolder);
        var hubContext = new FakeDeviceHubContext();
        var uploadService = new CloudUploadService(db, storageRelay, shopService, hubContext);

        var shop = await shopService.CreateShopAsync(null, "Test Shop");
        var session = await uploadService.CreateSessionAsync(shop.ShopCode);

        var fileContent = "This is a test PDF document content."u8.ToArray();
        using var ms = new MemoryStream(fileContent);

        var result = await uploadService.ProcessFileUploadAsync(
            session!.SessionId,
            "invoice_august.pdf",
            "application/pdf",
            fileContent.Length,
            ms);

        Assert.True(result.Success);
        Assert.NotEmpty(result.UploadId);
        Assert.Equal("invoice_august.pdf", result.OriginalFilename);
        Assert.Equal(fileContent.Length, result.FileSizeBytes);
        Assert.Equal(CloudUploadStatus.Uploaded, result.Status);

        // Verify file exists in temporary storage relay
        var (entity, downloadStream) = await uploadService.GetDownloadAsync(
            result.UploadId,
            (await db.Uploads.FirstAsync(u => u.UploadId == result.UploadId)).DownloadToken);

        Assert.NotNull(entity);
        Assert.NotNull(downloadStream);

        using var reader = new StreamReader(downloadStream);
        var downloadedText = await reader.ReadToEndAsync();
        Assert.Equal("This is a test PDF document content.", downloadedText);
    }

    [Fact]
    public async Task ProcessFileUploadAsync_RejectsInvalidExtension()
    {
        using var db = CreateInMemoryDbContext();
        var codeGen = new ShopCodeGenerator();
        var shopService = new ShopService(db, codeGen);
        var storageRelay = new CloudStorageRelayService(Path.Combine(Path.GetTempPath(), $"Relay_{Guid.NewGuid():N}"));
        var hubContext = new FakeDeviceHubContext();
        var uploadService = new CloudUploadService(db, storageRelay, shopService, hubContext);

        var shop = await shopService.CreateShopAsync(null, "Test Shop");
        var session = await uploadService.CreateSessionAsync(shop.ShopCode);

        var fileContent = "binary evil"u8.ToArray();
        using var ms = new MemoryStream(fileContent);

        var result = await uploadService.ProcessFileUploadAsync(
            session!.SessionId,
            "malware.exe",
            "application/x-msdownload",
            fileContent.Length,
            ms);

        Assert.False(result.Success);
        Assert.Contains("Unsupported file type", result.Message);
    }

    [Fact]
    public async Task ProcessFileUploadAsync_RejectsExpiredSession()
    {
        using var db = CreateInMemoryDbContext();
        var codeGen = new ShopCodeGenerator();
        var shopService = new ShopService(db, codeGen);
        var storageRelay = new CloudStorageRelayService(Path.Combine(Path.GetTempPath(), $"Relay_{Guid.NewGuid():N}"));
        var hubContext = new FakeDeviceHubContext();
        var uploadService = new CloudUploadService(db, storageRelay, shopService, hubContext);

        var shop = await shopService.CreateShopAsync(null, "Test Shop");
        var session = await uploadService.CreateSessionAsync(shop.ShopCode, TimeSpan.FromSeconds(-5)); // expired

        var fileContent = "data"u8.ToArray();
        using var ms = new MemoryStream(fileContent);

        var result = await uploadService.ProcessFileUploadAsync(
            session!.SessionId,
            "doc.pdf",
            "application/pdf",
            fileContent.Length,
            ms);

        Assert.False(result.Success);
        Assert.Contains("expired", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AcknowledgeDeliveryAsync_DeletesTemporaryFile_AndMarksDelivered()
    {
        using var db = CreateInMemoryDbContext();
        var codeGen = new ShopCodeGenerator();
        var shopService = new ShopService(db, codeGen);
        var tempFolder = Path.Combine(Path.GetTempPath(), $"Relay_{Guid.NewGuid():N}");
        var storageRelay = new CloudStorageRelayService(tempFolder);
        var hubContext = new FakeDeviceHubContext();
        var uploadService = new CloudUploadService(db, storageRelay, shopService, hubContext);

        var shop = await shopService.CreateShopAsync(null, "Test Shop");
        var session = await uploadService.CreateSessionAsync(shop.ShopCode);

        var fileContent = "print me please"u8.ToArray();
        using var ms = new MemoryStream(fileContent);

        var upload = await uploadService.ProcessFileUploadAsync(session!.SessionId, "sheet.xlsx", "application/vnd.ms-excel", fileContent.Length, ms);

        // Acknowledge delivery
        var ackResult = await uploadService.AcknowledgeDeliveryAsync(upload.UploadId, "DEV-SHOPPC1");
        Assert.True(ackResult);

        var dbEntity = await db.Uploads.FirstAsync(u => u.UploadId == upload.UploadId);
        Assert.Equal(CloudUploadStatus.Delivered, dbEntity.Status);
        Assert.NotNull(dbEntity.DeliveredAt);
        Assert.Equal("DEV-SHOPPC1", dbEntity.DeliveredToDeviceId);

        // Verify temporary file on cloud disk is deleted immediately
        var stream = await storageRelay.GetTemporaryFileStreamAsync(upload.UploadId);
        Assert.Null(stream);
    }

    [Fact]
    public async Task GetPendingUploadsForShopAsync_ReturnsUndeliveredFiles()
    {
        using var db = CreateInMemoryDbContext();
        var codeGen = new ShopCodeGenerator();
        var shopService = new ShopService(db, codeGen);
        var storageRelay = new CloudStorageRelayService(Path.Combine(Path.GetTempPath(), $"Relay_{Guid.NewGuid():N}"));
        var hubContext = new FakeDeviceHubContext();
        var uploadService = new CloudUploadService(db, storageRelay, shopService, hubContext);

        var shop = await shopService.CreateShopAsync(null, "Test Shop");
        var session = await uploadService.CreateSessionAsync(shop.ShopCode);

        var content = "data"u8.ToArray();
        using var ms1 = new MemoryStream(content);
        using var ms2 = new MemoryStream(content);

        await uploadService.ProcessFileUploadAsync(session!.SessionId, "file1.pdf", "application/pdf", content.Length, ms1);
        await uploadService.ProcessFileUploadAsync(session.SessionId, "file2.png", "image/png", content.Length, ms2);

        var pending = await uploadService.GetPendingUploadsForShopAsync(shop.ShopCode);
        Assert.Equal(2, pending.Count);
    }
}
