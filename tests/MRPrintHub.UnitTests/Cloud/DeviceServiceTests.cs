using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MRPrintHub.Cloud.Data;
using MRPrintHub.Cloud.Services;
using MRPrintHub.Core.DTOs.Cloud;
using Xunit;

namespace MRPrintHub.UnitTests.Cloud;

public class DeviceServiceTests
{
    private static CloudDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<CloudDbContext>()
            .UseInMemoryDatabase(databaseName: $"CloudDb_DeviceTest_{Guid.NewGuid():N}")
            .Options;
        return new CloudDbContext(options);
    }

    [Fact]
    public async Task RegisterDeviceAsync_AutoCreatesShop_WhenShopCodeNotProvided()
    {
        using var db = CreateInMemoryDbContext();
        var generator = new ShopCodeGenerator();
        var shopService = new ShopService(db, generator);
        var deviceService = new DeviceService(db, shopService, generator);

        var request = new DeviceRegistrationRequest(
            ShopCode: null,
            DeviceName: "Shop-Front-PC",
            ClientVersion: "1.0.0",
            HardwareFingerprint: "HW-12345");

        var response = await deviceService.RegisterDeviceAsync(request);

        Assert.True(response.Success);
        Assert.NotEmpty(response.ShopCode);
        Assert.NotEmpty(response.DeviceId);
        Assert.NotEmpty(response.DeviceToken!);

        // Verify stored in DB with hashed token
        var savedDevice = await deviceService.GetByDeviceIdAsync(response.DeviceId);
        Assert.NotNull(savedDevice);
        Assert.Equal("Shop-Front-PC", savedDevice.DeviceName);
        Assert.NotEqual(response.DeviceToken, savedDevice.DeviceTokenHash); // Hashed, not plain
    }

    [Fact]
    public async Task RegisterDeviceAsync_AttachesToExistingShop_WhenValidShopCodeProvided()
    {
        using var db = CreateInMemoryDbContext();
        var generator = new ShopCodeGenerator();
        var shopService = new ShopService(db, generator);
        var deviceService = new DeviceService(db, shopService, generator);

        var shop = await shopService.CreateShopAsync(null, "Main Cyber Cafe");

        var request = new DeviceRegistrationRequest(
            ShopCode: shop.ShopCode,
            DeviceName: "Secondary-PC",
            ClientVersion: "1.0.0",
            HardwareFingerprint: null);

        var response = await deviceService.RegisterDeviceAsync(request);

        Assert.True(response.Success);
        Assert.Equal(shop.ShopCode, response.ShopCode);
    }

    [Fact]
    public async Task RegisterDeviceAsync_Fails_WhenInvalidShopCodeProvided()
    {
        using var db = CreateInMemoryDbContext();
        var generator = new ShopCodeGenerator();
        var shopService = new ShopService(db, generator);
        var deviceService = new DeviceService(db, shopService, generator);

        var request = new DeviceRegistrationRequest(
            ShopCode: "MRPH-NONEXISTENT",
            DeviceName: "Secondary-PC",
            ClientVersion: "1.0.0",
            HardwareFingerprint: null);

        var response = await deviceService.RegisterDeviceAsync(request);

        Assert.False(response.Success);
        Assert.Null(response.DeviceToken);
    }

    [Fact]
    public async Task ValidateDeviceCredentialsAsync_SucceedsWithValidToken_FailsWithInvalidToken()
    {
        using var db = CreateInMemoryDbContext();
        var generator = new ShopCodeGenerator();
        var shopService = new ShopService(db, generator);
        var deviceService = new DeviceService(db, shopService, generator);

        var reg = await deviceService.RegisterDeviceAsync(new DeviceRegistrationRequest(
            ShopCode: null,
            DeviceName: "Billing-PC",
            ClientVersion: "1.0.0",
            HardwareFingerprint: null));

        // Valid credential test
        var validDevice = await deviceService.ValidateDeviceCredentialsAsync(reg.DeviceId, reg.DeviceToken!);
        Assert.NotNull(validDevice);
        Assert.Equal("Billing-PC", validDevice.DeviceName);

        // Invalid token test
        var invalidDevice = await deviceService.ValidateDeviceCredentialsAsync(reg.DeviceId, "wrong-secret-token");
        Assert.Null(invalidDevice);
    }

    [Fact]
    public async Task UpdateConnectionStateAndHeartbeat_UpdatesLastSeenAndConnectedStatus()
    {
        using var db = CreateInMemoryDbContext();
        var generator = new ShopCodeGenerator();
        var shopService = new ShopService(db, generator);
        var deviceService = new DeviceService(db, shopService, generator);

        var reg = await deviceService.RegisterDeviceAsync(new DeviceRegistrationRequest(
            ShopCode: null,
            DeviceName: "Print-Station-1",
            ClientVersion: "1.0.0",
            HardwareFingerprint: null));

        // Connect
        await deviceService.UpdateConnectionStateAsync(reg.DeviceId, "conn-id-1234", isConnected: true);
        var device = await deviceService.GetByDeviceIdAsync(reg.DeviceId);
        Assert.NotNull(device);
        Assert.True(device.IsConnected);
        Assert.Equal("conn-id-1234", device.LastConnectionId);

        // Heartbeat
        await deviceService.UpdateHeartbeatAsync(reg.DeviceId);
        var deviceAfterHb = await deviceService.GetByDeviceIdAsync(reg.DeviceId);
        Assert.NotNull(deviceAfterHb);
        Assert.True(deviceAfterHb.IsConnected);

        // Disconnect
        await deviceService.UpdateConnectionStateAsync(reg.DeviceId, null, isConnected: false);
        var deviceAfterDisconnect = await deviceService.GetByDeviceIdAsync(reg.DeviceId);
        Assert.NotNull(deviceAfterDisconnect);
        Assert.False(deviceAfterDisconnect.IsConnected);
    }
}
