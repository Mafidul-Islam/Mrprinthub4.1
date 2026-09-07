using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using MRPrintHub.Core.DTOs.Cloud;
using MRPrintHub.Core.Enums;

namespace MRPrintHub.Desktop.Cloud;

public interface ICloudConnectionManager : IAsyncDisposable
{
    CloudConnectionState State { get; }
    CloudStatusDto CurrentStatus { get; }
    string CloudBaseUrl { get; }
    event EventHandler<CloudStatusDto>? StatusChanged;
    event EventHandler<CloudUploadNotificationDto>? UploadReceived;
    Task StartAsync(string? cloudBaseUrl = null, CancellationToken cancellationToken = default);
    Task StopAsync();
}

public class CloudConnectionManager : ICloudConnectionManager
{
    private readonly HttpClient _httpClient;
    private HubConnection? _hubConnection;
    private CancellationTokenSource? _cts;
    private Task? _heartbeatTask;
    private readonly string _credentialsPath;
    private readonly string _receivedFolder;
    private readonly ConcurrentDictionary<string, bool> _processedUploadIds = new();

    public CloudConnectionState State { get; private set; } = CloudConnectionState.Disconnected;
    public CloudStatusDto CurrentStatus => new(State, _shopCode, _deviceId, _lastConnectedAt, _lastError);
    public string CloudBaseUrl => _cloudBaseUrl;

    public event EventHandler<CloudStatusDto>? StatusChanged;
    public event EventHandler<CloudUploadNotificationDto>? UploadReceived;

    private string? _shopCode;
    private string? _deviceId;
    private string? _deviceToken;
    private string _cloudBaseUrl = "http://localhost:5000";
    private DateTime? _lastConnectedAt;
    private string? _lastError;

    public CloudConnectionManager(HttpClient? httpClient = null, string? customReceivedFolder = null)
    {
        _httpClient = httpClient ?? new HttpClient();

        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        _credentialsPath = Path.Combine(programData, "MRPrintHub", "cloud_credentials.json");
        _receivedFolder = customReceivedFolder ?? Path.Combine(programData, "MRPrintHub", "Received");

        if (!Directory.Exists(_receivedFolder))
        {
            try { Directory.CreateDirectory(_receivedFolder); } catch { }
        }

        LoadSavedCredentials();
    }

    public async Task StartAsync(string? cloudBaseUrl = null, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(cloudBaseUrl))
        {
            _cloudBaseUrl = cloudBaseUrl.TrimEnd('/');
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Run connection in background so it NEVER blocks desktop startup or local Wi-Fi mode
        _ = Task.Run(async () =>
        {
            try
            {
                await ConnectInternalAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                SetState(CloudConnectionState.Failed, ex.Message);
            }
        }, _cts.Token);
    }

    private async Task ConnectInternalAsync(CancellationToken cancellationToken)
    {
        SetState(CloudConnectionState.Connecting);

        // 1. Ensure Device is Registered
        if (string.IsNullOrEmpty(_deviceId) || string.IsNullOrEmpty(_deviceToken))
        {
            bool registered = await TryRegisterDeviceAsync(cancellationToken);
            if (!registered)
            {
                SetState(CloudConnectionState.Failed, "Device registration failed");
                return;
            }
        }

        // 2. Build and Configure SignalR Connection
        var hubUrl = $"{_cloudBaseUrl}/hubs/device?deviceId={Uri.EscapeDataString(_deviceId!)}&deviceToken={Uri.EscapeDataString(_deviceToken!)}";

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.Headers.Add("X-Device-Id", _deviceId!);
                options.Headers.Add("X-Device-Token", _deviceToken!);
            })
            .WithAutomaticReconnect(new ExponentialBackoffRetryPolicy())
            .Build();

        // 3. Register Real-time Cloud Upload Handlers
        _hubConnection.On<CloudUploadNotificationDto>("OnNewUploadAvailable", async notification =>
        {
            await HandleIncomingUploadAsync(notification);
        });

        _hubConnection.On<List<CloudUploadNotificationDto>>("OnPendingUploadsAvailable", async pendingList =>
        {
            foreach (var notif in pendingList)
            {
                await HandleIncomingUploadAsync(notif);
            }
        });

        _hubConnection.Reconnecting += error =>
        {
            SetState(CloudConnectionState.Reconnecting, error?.Message);
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += connectionId =>
        {
            _lastConnectedAt = DateTime.UtcNow;
            SetState(CloudConnectionState.Connected);
            return Task.CompletedTask;
        };

        _hubConnection.Closed += error =>
        {
            SetState(CloudConnectionState.Disconnected, error?.Message);
            return Task.CompletedTask;
        };

        // 4. Connect to SignalR Hub
        try
        {
            await _hubConnection.StartAsync(cancellationToken);
            _lastConnectedAt = DateTime.UtcNow;
            SetState(CloudConnectionState.Connected);

            // Start Heartbeat loop
            _heartbeatTask = RunHeartbeatLoopAsync(_cts!.Token);
        }
        catch (Exception ex)
        {
            SetState(CloudConnectionState.Failed, $"Connection to cloud failed: {ex.Message}");
        }
    }

    private async Task HandleIncomingUploadAsync(CloudUploadNotificationDto notification)
    {
        if (notification == null || string.IsNullOrWhiteSpace(notification.UploadId))
            return;

        // Idempotency: Prevent duplicate download if already processed
        if (_processedUploadIds.ContainsKey(notification.UploadId))
        {
            await SendAcknowledgementAsync(notification.UploadId);
            return;
        }

        try
        {
            // Download file stream from Cloud relay
            var downloadUrl = $"{_cloudBaseUrl}/api/v1/relay/download/{notification.UploadId}";
            using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            request.Headers.Add("X-Device-Id", _deviceId);
            request.Headers.Add("X-Device-Token", _deviceToken);
            request.Headers.Add("X-Download-Token", notification.DownloadToken);

            using var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                if (!Directory.Exists(_receivedFolder))
                {
                    Directory.CreateDirectory(_receivedFolder);
                }

                // Resolve target file path with collision avoidance (1), (2)...
                var safeName = Path.GetFileName(notification.OriginalFilename);
                if (string.IsNullOrWhiteSpace(safeName)) safeName = $"online_upload_{DateTime.UtcNow.Ticks}.dat";

                var targetPath = Path.Combine(_receivedFolder, safeName);
                var nameWithoutExt = Path.GetFileNameWithoutExtension(safeName);
                var ext = Path.GetExtension(safeName);

                int counter = 1;
                while (File.Exists(targetPath))
                {
                    targetPath = Path.Combine(_receivedFolder, $"{nameWithoutExt} ({counter++}){ext}");
                }

                // Save to local disk
                await using (var fs = File.Create(targetPath))
                {
                    await response.Content.CopyToAsync(fs);
                }

                _processedUploadIds[notification.UploadId] = true;

                // Send Delivery ACK to Cloud so Cloud deletes its temporary copy
                await SendAcknowledgementAsync(notification.UploadId);

                UploadReceived?.Invoke(this, notification);
            }
        }
        catch
        {
            // Ignore transient download errors - will retry on reconnect
        }
    }

    private async Task SendAcknowledgementAsync(string uploadId)
    {
        try
        {
            if (_hubConnection?.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("AcknowledgeUpload", uploadId);
                return;
            }

            // Fallback REST ACK
            var ackUrl = $"{_cloudBaseUrl}/api/v1/relay/ack/{uploadId}";
            using var req = new HttpRequestMessage(HttpMethod.Post, ackUrl);
            req.Headers.Add("X-Device-Id", _deviceId);
            req.Headers.Add("X-Device-Token", _deviceToken);
            await _httpClient.SendAsync(req);
        }
        catch
        {
            // Ignore ACK delivery failure
        }
    }

    private async Task<bool> TryRegisterDeviceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var registerUrl = $"{_cloudBaseUrl}/api/v1/devices/register";
            var request = new DeviceRegistrationRequest(
                ShopCode: _shopCode,
                DeviceName: Environment.MachineName,
                ClientVersion: "1.0.0",
                HardwareFingerprint: null);

            var response = await _httpClient.PostAsJsonAsync(registerUrl, request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<DeviceRegistrationResponse>(cancellationToken: cancellationToken);
            if (result == null || !result.Success || string.IsNullOrEmpty(result.DeviceId) || string.IsNullOrEmpty(result.DeviceToken))
            {
                return false;
            }

            _shopCode = result.ShopCode;
            _deviceId = result.DeviceId;
            _deviceToken = result.DeviceToken;

            SaveCredentials();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task RunHeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _hubConnection != null)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

                if (_hubConnection.State == HubConnectionState.Connected && !string.IsNullOrEmpty(_deviceId))
                {
                    var heartbeat = new DeviceHeartbeatDto(_deviceId, DateTime.UtcNow, "Active");
                    await _hubConnection.InvokeAsync("SendHeartbeat", heartbeat, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Ignore transient heartbeat failures
            }
        }
    }

    private void SetState(CloudConnectionState newState, string? error = null)
    {
        State = newState;
        _lastError = error;
        StatusChanged?.Invoke(this, CurrentStatus);
    }

    private void LoadSavedCredentials()
    {
        try
        {
            var envCloudUrl = Environment.GetEnvironmentVariable("MRPRINTHUB_CLOUD_URL");
            if (!string.IsNullOrWhiteSpace(envCloudUrl))
            {
                _cloudBaseUrl = envCloudUrl.TrimEnd('/');
            }

            if (File.Exists(_credentialsPath))
            {
                var json = File.ReadAllText(_credentialsPath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("ShopCode", out var sc)) _shopCode = sc.GetString();
                if (doc.RootElement.TryGetProperty("DeviceId", out var did)) _deviceId = did.GetString();
                if (doc.RootElement.TryGetProperty("DeviceToken", out var tok)) _deviceToken = tok.GetString();
                if (doc.RootElement.TryGetProperty("CloudBaseUrl", out var url) && !string.IsNullOrWhiteSpace(url.GetString()))
                {
                    _cloudBaseUrl = url.GetString()!.TrimEnd('/');
                }
            }
        }
        catch
        {
            // Ignore load errors
        }
    }

    private void SaveCredentials()
    {
        try
        {
            var dir = Path.GetDirectoryName(_credentialsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var data = new
            {
                ShopCode = _shopCode,
                DeviceId = _deviceId,
                DeviceToken = _deviceToken,
                CloudBaseUrl = _cloudBaseUrl,
                SavedAt = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_credentialsPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();

        if (_hubConnection != null)
        {
            try
            {
                await _hubConnection.StopAsync();
                await _hubConnection.DisposeAsync();
            }
            catch
            {
                // Ignore shutdown errors
            }
            _hubConnection = null;
        }

        SetState(CloudConnectionState.Disconnected);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts?.Dispose();
    }
}
