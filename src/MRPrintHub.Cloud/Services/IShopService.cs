using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MRPrintHub.Cloud.Data;
using MRPrintHub.Cloud.Data.Entities;

namespace MRPrintHub.Cloud.Services;

public interface IShopService
{
    Task<ShopEntity?> GetByCodeAsync(string shopCode);
    Task<ShopEntity> CreateShopAsync(string? requestedCode = null, string? name = null);
    Task<ShopEntity> GetOrCreateShopAsync(string? shopCode, string? name = null);
}

public class ShopService : IShopService
{
    private readonly CloudDbContext _db;
    private readonly IShopCodeGenerator _codeGenerator;

    public ShopService(CloudDbContext db, IShopCodeGenerator codeGenerator)
    {
        _db = db;
        _codeGenerator = codeGenerator;
    }

    public async Task<ShopEntity?> GetByCodeAsync(string shopCode)
    {
        if (string.IsNullOrWhiteSpace(shopCode))
            return null;

        return await _db.Shops
            .Include(s => s.Devices)
            .FirstOrDefaultAsync(s => s.ShopCode == shopCode.Trim().ToUpperInvariant());
    }

    public async Task<ShopEntity> CreateShopAsync(string? requestedCode = null, string? name = null)
    {
        string code = requestedCode?.Trim().ToUpperInvariant() ?? _codeGenerator.GenerateShopCode();

        // Ensure uniqueness
        while (await _db.Shops.AnyAsync(s => s.ShopCode == code))
        {
            code = _codeGenerator.GenerateShopCode();
        }

        var shop = new ShopEntity
        {
            ShopCode = code,
            Name = name ?? $"Shop {code}",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _db.Shops.Add(shop);
        await _db.SaveChangesAsync();
        return shop;
    }

    public async Task<ShopEntity> GetOrCreateShopAsync(string? shopCode, string? name = null)
    {
        if (!string.IsNullOrWhiteSpace(shopCode))
        {
            var existing = await GetByCodeAsync(shopCode);
            if (existing != null)
                return existing;
        }

        return await CreateShopAsync(shopCode, name);
    }
}
