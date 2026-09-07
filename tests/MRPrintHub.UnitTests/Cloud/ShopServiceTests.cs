using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MRPrintHub.Cloud.Data;
using MRPrintHub.Cloud.Services;
using Xunit;

namespace MRPrintHub.UnitTests.Cloud;

public class ShopServiceTests
{
    private static CloudDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<CloudDbContext>()
            .UseInMemoryDatabase(databaseName: $"CloudDb_Test_{Guid.NewGuid():N}")
            .Options;
        return new CloudDbContext(options);
    }

    [Fact]
    public async Task CreateShopAsync_CreatesShopWithUniqueCode()
    {
        using var db = CreateInMemoryDbContext();
        var generator = new ShopCodeGenerator();
        var shopService = new ShopService(db, generator);

        var shop = await shopService.CreateShopAsync(null, "Test Cafe Print");

        Assert.NotNull(shop);
        Assert.StartsWith("MRPH-", shop.ShopCode);
        Assert.Equal("Test Cafe Print", shop.Name);
        Assert.True(shop.IsActive);

        var retrieved = await shopService.GetByCodeAsync(shop.ShopCode);
        Assert.NotNull(retrieved);
        Assert.Equal(shop.Id, retrieved.Id);
    }

    [Fact]
    public async Task GetByCodeAsync_CaseInsensitiveLookup()
    {
        using var db = CreateInMemoryDbContext();
        var generator = new ShopCodeGenerator();
        var shopService = new ShopService(db, generator);

        var shop = await shopService.CreateShopAsync("MRPH-ABCDEF12", "Upper Shop");

        var lowerLookup = await shopService.GetByCodeAsync("mrph-abcdef12");
        Assert.NotNull(lowerLookup);
        Assert.Equal(shop.Id, lowerLookup.Id);
    }
}
