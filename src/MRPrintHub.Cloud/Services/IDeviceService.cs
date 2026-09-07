using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MRPrintHub.Cloud.Data;
using MRPrintHub.Cloud.Data.Entities;
using MRPrintHub.Core.DTOs.Cloud;

namespace MRPrintHub.Cloud.Services;

public interface IDeviceService
{
    Task<DeviceRegistrationResponse> RegisterDeviceAsync(DeviceRegistrationRequest request);
    Task<DeviceEntity?> ValidateDeviceCredentialsAsync(string deviceId, string rawToken);
    Task<bool> UpdateConnectionStateAsync(string deviceId, string? connectionId, bool isConnected);
    Task<bool> UpdateHeartbeatAsync(string deviceId);
    Task<DeviceEntity?> GetByDeviceIdAsync(string deviceId);
}

public class DeviceService : IDeviceService
{
    private readonly CloudDbContext _db;
    private readonly IShopService _shopService;
    private readonly IShopCodeGenerator _codeGenerator;

    public DeviceService(CloudDbContext db, IShopService shopService, IShopCodeGenerator codeGenerator)
    {
        _db = db;
        _shopService = shopService;
        _codeGenerator = codeGenerator;
    }

    public async Task<DeviceRegistrationResponse> RegisterDeviceAsync(DeviceRegistrationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceName))
        {
            return new DeviceRegistrationResponse(
                Success: false,
                ShopCode: string.Empty,
                DeviceId: string.Empty,
                DeviceToken: null,
                Message: "Device name is required",
                CreatedAt: DateTime.UtcNow);
        }

        // Get or create shop
        ShopEntity shop;
        if (!string.IsNullOrWhiteSpace(request.ShopCode))
        {
            var existingShop = await _shopService.GetByCodeAsync(request.ShopCode);
            if (existingShop == null)
            {
                return new DeviceRegistrationResponse(
                    Success: false,
                    ShopCode: request.ShopCode,
                    DeviceId: string.Empty,
                    DeviceToken: null,
                    Message: $"Shop code '{request.ShopCode}' not found",
                    CreatedAt: DateTime.UtcNow);
            }
            shop = existingShop;
        }
        else
        {
            shop = await _shopService.CreateShopAsync(null, $"{request.DeviceName}'s Shop");
        }

        // Generate unique Device ID & Device Secret Token
        string deviceId = _codeGenerator.GenerateDeviceId();
        while (await _db.Devices.AnyAsync(d => d.DeviceId == deviceId))
        {
            deviceId = _codeGenerator.GenerateDeviceId();
        }

        string rawToken = _codeGenerator.GenerateDeviceToken();
        string tokenHash = HashToken(rawToken);

        var device = new DeviceEntity
        {
            ShopId = shop.Id,
            DeviceId = deviceId,
            DeviceName = request.DeviceName.Trim(),
            DeviceTokenHash = tokenHash,
            HardwareFingerprint = request.HardwareFingerprint,
            ClientVersion = request.ClientVersion,
            CreatedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
            IsActive = true,
            IsConnected = false
        };

        _db.Devices.Add(device);
        await _db.SaveChangesAsync();

        return new DeviceRegistrationResponse(
            Success: true,
            ShopCode: shop.ShopCode,
            DeviceId: device.DeviceId,
            DeviceToken: rawToken, // Returned ONLY ONCE on registration
            Message: "Device successfully registered",
            CreatedAt: device.CreatedAt);
    }

    public async Task<DeviceEntity?> ValidateDeviceCredentialsAsync(string deviceId, string rawToken)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(rawToken))
            return null;

        var device = await _db.Devices
            .Include(d => d.Shop)
            .FirstOrDefaultAsync(d => d.DeviceId == deviceId.Trim());

        if (device == null || !device.IsActive || (device.Shop != null && !device.Shop.IsActive))
            return null;

        string expectedHash = HashToken(rawToken);
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(device.DeviceTokenHash),
            Encoding.UTF8.GetBytes(expectedHash)))
        {
            return null;
        }

        return device;
    }

    public async Task<bool> UpdateConnectionStateAsync(string deviceId, string? connectionId, bool isConnected)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.DeviceId == deviceId);
        if (device == null) return false;

        device.IsConnected = isConnected;
        device.LastConnectionId = isConnected ? connectionId : null;
        device.LastSeenAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateHeartbeatAsync(string deviceId)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.DeviceId == deviceId);
        if (device == null) return false;

        device.LastSeenAt = DateTime.UtcNow;
        device.IsConnected = true;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<DeviceEntity?> GetByDeviceIdAsync(string deviceId)
    {
        return await _db.Devices
            .Include(d => d.Shop)
            .FirstOrDefaultAsync(d => d.DeviceId == deviceId);
    }

    private static string HashToken(string rawToken)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
