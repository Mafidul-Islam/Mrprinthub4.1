using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MRPrintHub.Cloud.Services;
using MRPrintHub.Core.DTOs.Cloud;

namespace MRPrintHub.Cloud.Hubs;

public class DeviceHub : Hub
{
    private readonly IDeviceService _deviceService;
    private readonly ICloudUploadService _uploadService;
    private readonly ILogger<DeviceHub> _logger;

    public DeviceHub(
        IDeviceService deviceService,
        ICloudUploadService uploadService,
        ILogger<DeviceHub> logger)
    {
        _deviceService = deviceService;
        _uploadService = uploadService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        if (httpContext == null)
        {
            Context.Abort();
            return;
        }

        // Read credentials from headers or query parameters
        string? deviceId = httpContext.Request.Headers["X-Device-Id"].ToString();
        if (string.IsNullOrEmpty(deviceId))
        {
            deviceId = httpContext.Request.Query["deviceId"].ToString();
        }

        string? deviceToken = httpContext.Request.Headers["X-Device-Token"].ToString();
        if (string.IsNullOrEmpty(deviceToken))
        {
            deviceToken = httpContext.Request.Query["deviceToken"].ToString();
        }

        if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(deviceToken))
        {
            _logger.LogWarning("Connection rejected: missing device credentials. ConnectionId: {ConnectionId}", Context.ConnectionId);
            Context.Abort();
            return;
        }

        var device = await _deviceService.ValidateDeviceCredentialsAsync(deviceId, deviceToken);
        if (device == null)
        {
            _logger.LogWarning("Connection rejected: invalid device credentials for DeviceId: {DeviceId}", deviceId);
            Context.Abort();
            return;
        }

        // Store device identity in Connection items
        Context.Items["DeviceId"] = device.DeviceId;
        Context.Items["ShopCode"] = device.Shop?.ShopCode;

        await _deviceService.UpdateConnectionStateAsync(device.DeviceId, Context.ConnectionId, isConnected: true);

        if (device.Shop != null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"shop_{device.Shop.ShopCode}");

            // Check if any uploads arrived while this Desktop was offline
            var pendingUploads = await _uploadService.GetPendingUploadsForShopAsync(device.Shop.ShopCode);
            if (pendingUploads.Count > 0)
            {
                _logger.LogInformation("Delivering {Count} pending offline upload(s) to device {DeviceId}", pendingUploads.Count, device.DeviceId);
                await Clients.Caller.SendAsync("OnPendingUploadsAvailable", pendingUploads);
            }
        }

        _logger.LogInformation("Device connected: {DeviceId} (Shop: {ShopCode})", device.DeviceId, device.Shop?.ShopCode);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue("DeviceId", out var deviceIdObj) && deviceIdObj is string deviceId)
        {
            await _deviceService.UpdateConnectionStateAsync(deviceId, Context.ConnectionId, isConnected: false);
            _logger.LogInformation("Device disconnected: {DeviceId}", deviceId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Periodic heartbeat / ping from Desktop client.
    /// </summary>
    public async Task<bool> SendHeartbeat(DeviceHeartbeatDto heartbeat)
    {
        if (Context.Items.TryGetValue("DeviceId", out var deviceIdObj) && deviceIdObj is string deviceId)
        {
            if (deviceId == heartbeat.DeviceId)
            {
                await _deviceService.UpdateHeartbeatAsync(deviceId);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Called by Desktop over SignalR to acknowledge successful file receipt and trigger Cloud deletion.
    /// </summary>
    public async Task<bool> AcknowledgeUpload(string uploadId)
    {
        if (Context.Items.TryGetValue("DeviceId", out var deviceIdObj) && deviceIdObj is string deviceId)
        {
            var success = await _uploadService.AcknowledgeDeliveryAsync(uploadId, deviceId);
            if (success)
            {
                _logger.LogInformation("Upload {UploadId} acknowledged and deleted from cloud storage by device {DeviceId}", uploadId, deviceId);
            }
            return success;
        }
        return false;
    }
}
