using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MRPrintHub.Cloud.Data;
using MRPrintHub.Cloud.Hubs;
using MRPrintHub.Cloud.Services;
using MRPrintHub.Core.DTOs.Cloud;

var builder = WebApplication.CreateBuilder(args);

// Dynamic PORT binding for Koyeb / Container hosting (fallback to default for local dev)
var envPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(envPort) && int.TryParse(envPort, out var port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// Database configuration (PostgreSQL with InMemory fallback for testing/dev)
var rawConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                         ?? Environment.GetEnvironmentVariable("DATABASE_URL");
var connectionString = ResolvePostgreSqlConnectionString(rawConnectionString);

builder.Services.AddDbContext<CloudDbContext>(options =>
{
    if (!string.IsNullOrEmpty(connectionString) && !builder.Environment.IsEnvironment("Testing"))
    {
        options.UseNpgsql(connectionString);
    }
    else
    {
        options.UseInMemoryDatabase("MRPrintHub_Cloud_Db");
    }
});

// Register Cloud Services
builder.Services.AddSingleton<IShopCodeGenerator, ShopCodeGenerator>();
builder.Services.AddSingleton<ICloudStorageRelayService, CloudStorageRelayService>();
builder.Services.AddScoped<IShopService, ShopService>();
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<ICloudUploadService, CloudUploadService>();

// Retention cleanup worker
builder.Services.AddHostedService<CloudRetentionCleanupService>();

// SignalR for persistent real-time device connection with proxy keep-alive
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
});

// CORS for web clients
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowed(_ => true)
              .AllowCredentials();
    });
});

var app = builder.Build();

// Ensure DB schema created in development and seed demo shop
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CloudDbContext>();
    db.Database.EnsureCreated();

    if (!db.Shops.Any(s => s.ShopCode == "MRPH-TEST"))
    {
        db.Shops.Add(new MRPrintHub.Cloud.Data.Entities.ShopEntity
        {
            ShopCode = "MRPH-TEST",
            Name = "MR Print Hub Demo Shop",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }
}

app.UseCors();

// -------------------------------------------------------------------------
// Customer Mobile Web Endpoints
// -------------------------------------------------------------------------

// Customer scans online QR -> creates/gets session & serves mobile upload SPA with local-first hybrid awareness
app.MapGet("/u/{shopToken}", async (string shopToken, HttpContext httpContext, ICloudUploadService uploadService) =>
{
    var session = await uploadService.CreateSessionAsync(shopToken);
    if (session == null)
    {
        return Results.NotFound("Invalid or inactive Shop QR Code.");
    }

    string? localEndpoint = httpContext.Request.Query["local"].FirstOrDefault();
    string? localToken = httpContext.Request.Query["token"].FirstOrDefault();

    var html = GetOnlineUploadPageHtml(session.SessionId, session.ShopName ?? session.ShopCode, localEndpoint, localToken);
    return Results.Content(html, "text/html; charset=utf-8");
});

// Customer mobile uploads file to Cloud
app.MapPost("/api/v1/uploads/{sessionId}", async (HttpRequest request, string sessionId, ICloudUploadService uploadService) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Expected multipart/form-data content" });
    }

    var form = await request.ReadFormAsync();
    var files = form.Files;

    if (files == null || files.Count == 0)
    {
        return Results.BadRequest(new { error = "No files found in upload request" });
    }

    string? uploadId = request.Headers["X-Upload-Id"].FirstOrDefault()
                      ?? form["uploadId"].FirstOrDefault();

    var results = new List<CloudUploadResultDto>();
    foreach (var file in files)
    {
        if (file.Length > 0)
        {
            await using var stream = file.OpenReadStream();
            var res = await uploadService.ProcessFileUploadAsync(sessionId, file.FileName, file.ContentType, file.Length, stream, uploadId);
            results.Add(res);
        }
    }

    bool allSuccess = results.All(r => r.Success);
    if (!allSuccess)
    {
        return Results.BadRequest(new { success = false, results });
    }

    return Results.Ok(new { success = true, uploadedCount = results.Count, results });
}).DisableAntiforgery();

// Get Upload Session info
app.MapGet("/api/v1/uploads/session/{sessionId}", async (string sessionId, ICloudUploadService uploadService) =>
{
    var session = await uploadService.GetSessionAsync(sessionId);
    if (session == null)
    {
        return Results.NotFound(new { error = "Session not found or expired" });
    }

    return Results.Ok(session);
});

// -------------------------------------------------------------------------
// Desktop Relay Endpoints (Secure Outbound Downloads)
// -------------------------------------------------------------------------

// Desktop downloads file from Cloud relay
app.MapGet("/api/v1/relay/download/{uploadId}", async (
    string uploadId,
    HttpContext httpContext,
    ICloudUploadService uploadService,
    IDeviceService deviceService) =>
{
    string? deviceId = httpContext.Request.Headers["X-Device-Id"].ToString();
    string? deviceToken = httpContext.Request.Headers["X-Device-Token"].ToString();
    string? downloadToken = httpContext.Request.Headers["X-Download-Token"].ToString();

    if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(deviceToken) || string.IsNullOrEmpty(downloadToken))
    {
        return Results.Unauthorized();
    }

    var device = await deviceService.ValidateDeviceCredentialsAsync(deviceId, deviceToken);
    if (device == null)
    {
        return Results.Unauthorized();
    }

    var (uploadEntity, fileStream) = await uploadService.GetDownloadAsync(uploadId, downloadToken);
    if (uploadEntity == null || fileStream == null)
    {
        return Results.NotFound(new { error = "File not found, expired, or already delivered" });
    }

    return Results.File(fileStream, uploadEntity.ContentType, uploadEntity.OriginalFilename);
});

// Desktop sends delivery ACK -> Cloud immediately deletes temporary file
app.MapPost("/api/v1/relay/ack/{uploadId}", async (
    string uploadId,
    HttpContext httpContext,
    ICloudUploadService uploadService,
    IDeviceService deviceService) =>
{
    string? deviceId = httpContext.Request.Headers["X-Device-Id"].ToString();
    string? deviceToken = httpContext.Request.Headers["X-Device-Token"].ToString();

    if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(deviceToken))
    {
        return Results.Unauthorized();
    }

    var device = await deviceService.ValidateDeviceCredentialsAsync(deviceId, deviceToken);
    if (device == null)
    {
        return Results.Unauthorized();
    }

    var success = await uploadService.AcknowledgeDeliveryAsync(uploadId, device.DeviceId);
    if (!success)
    {
        return Results.NotFound(new { error = "Upload not found or already acknowledged" });
    }

    return Results.Ok(new { success = true, uploadId, status = "Delivered" });
});

// Desktop queries pending uploads (e.g. after reconnecting from offline state)
app.MapGet("/api/v1/relay/pending/{shopCode}", async (
    string shopCode,
    HttpContext httpContext,
    ICloudUploadService uploadService,
    IDeviceService deviceService) =>
{
    string? deviceId = httpContext.Request.Headers["X-Device-Id"].ToString();
    string? deviceToken = httpContext.Request.Headers["X-Device-Token"].ToString();

    if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(deviceToken))
    {
        return Results.Unauthorized();
    }

    var device = await deviceService.ValidateDeviceCredentialsAsync(deviceId, deviceToken);
    if (device == null || device.Shop?.ShopCode != shopCode.Trim().ToUpperInvariant())
    {
        return Results.Unauthorized();
    }

    var pending = await uploadService.GetPendingUploadsForShopAsync(shopCode);
    return Results.Ok(pending);
});

// -------------------------------------------------------------------------
// Device & Shop Management Endpoints
// -------------------------------------------------------------------------

// Health Checks (Compatible with both /api/health and /api/v1/health probes)
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "MR Print Hub Cloud",
    version = "1.0.0",
    timestampUtc = DateTime.UtcNow
}));

app.MapGet("/api/v1/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "MR Print Hub Cloud",
    version = "1.0.0",
    timestampUtc = DateTime.UtcNow
}));

// Device Registration
app.MapPost("/api/v1/devices/register", async (DeviceRegistrationRequest request, IDeviceService deviceService) =>
{
    if (request == null)
    {
        return Results.BadRequest(new { error = "Invalid request payload" });
    }

    var response = await deviceService.RegisterDeviceAsync(request);
    if (!response.Success)
    {
        return Results.BadRequest(response);
    }

    return Results.Ok(response);
});

// Device Status Query
app.MapGet("/api/v1/devices/{deviceId}/status", async (string deviceId, HttpContext httpContext, IDeviceService deviceService) =>
{
    string? token = httpContext.Request.Headers["X-Device-Token"].ToString();
    if (string.IsNullOrEmpty(token))
    {
        return Results.Unauthorized();
    }

    var device = await deviceService.ValidateDeviceCredentialsAsync(deviceId, token);
    if (device == null)
    {
        return Results.Unauthorized();
    }

    return Results.Ok(new
    {
        deviceId = device.DeviceId,
        deviceName = device.DeviceName,
        shopCode = device.Shop?.ShopCode,
        isConnected = device.IsConnected,
        lastSeenAt = device.LastSeenAt,
        createdAt = device.CreatedAt,
        isActive = device.IsActive
    });
});

// Shop Info Query
app.MapGet("/api/v1/shops/{shopCode}", async (string shopCode, IShopService shopService) =>
{
    var shop = await shopService.GetByCodeAsync(shopCode);
    if (shop == null)
    {
        return Results.NotFound(new { error = $"Shop code '{shopCode}' not found" });
    }

    return Results.Ok(new
    {
        shopCode = shop.ShopCode,
        name = shop.Name,
        createdAt = shop.CreatedAt,
        isActive = shop.IsActive,
        registeredDeviceCount = shop.Devices?.Count ?? 0
    });
});

// SignalR Hub Endpoint
app.MapHub<DeviceHub>("/hubs/device");

app.Run();

static string GetOnlineUploadPageHtml(string sessionId, string shopName, string? localEndpoint = null, string? localToken = null) => $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no">
    <title>MR Print Hub - Online Print Upload</title>
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
            background: #f0f4f8;
            color: #1e293b;
            padding: 16px;
            display: flex;
            justify-content: center;
            align-items: center;
            min-height: 100vh;
            -webkit-font-smoothing: antialiased;
        }
        .card {
            background: #ffffff;
            max-width: 440px;
            width: 100%;
            border-radius: 20px;
            box-shadow: 0 10px 30px rgba(0,0,0,0.06);
            padding: 28px 22px;
            position: relative;
            overflow: hidden;
        }
        .header {
            text-align: center;
            margin-bottom: 20px;
        }
        .logo-badge {
            width: 52px;
            height: 52px;
            background: linear-gradient(135deg, #2563eb, #1e40af);
            border-radius: 14px;
            display: flex;
            align-items: center;
            justify-content: center;
            margin: 0 auto 10px;
            box-shadow: 0 4px 12px rgba(37,99,235,0.25);
        }
        .logo-title {
            font-size: 21px;
            font-weight: 800;
            color: #0f172a;
            letter-spacing: -0.4px;
        }
        .subtitle {
            font-size: 13px;
            color: #64748b;
            margin-top: 3px;
        }
        .mode-badge {
            display: inline-block;
            background: #eff6ff;
            color: #2563eb;
            font-size: 11px;
            font-weight: 700;
            padding: 4px 10px;
            border-radius: 999px;
            margin-top: 6px;
            border: 1px solid #bfdbfe;
        }

        /* Dropzone */
        .dropzone {
            border: 2px dashed #93c5fd;
            border-radius: 14px;
            padding: 28px 16px;
            text-align: center;
            background: #eff6ff;
            cursor: pointer;
            transition: all 0.2s ease;
            margin-bottom: 16px;
        }
        .dropzone:active {
            transform: scale(0.99);
            background: #dbeafe;
        }
        .dropzone-icon {
            font-size: 34px;
            margin-bottom: 6px;
        }
        .dropzone-text {
            font-size: 15px;
            font-weight: 600;
            color: #1e40af;
        }
        .dropzone-hint {
            font-size: 12px;
            color: #64748b;
            margin-top: 4px;
        }
        input[type="file"] { display: none; }

        /* File List */
        .file-list {
            margin-bottom: 16px;
            max-height: 180px;
            overflow-y: auto;
        }
        .file-item {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 10px 14px;
            background: #f8fafc;
            border-radius: 10px;
            margin-bottom: 8px;
            font-size: 13px;
            border: 1px solid #e2e8f0;
        }
        .file-name {
            font-weight: 500;
            color: #334155;
            overflow: hidden;
            text-overflow: ellipsis;
            white-space: nowrap;
            max-width: 220px;
        }
        .file-size {
            color: #94a3b8;
            font-size: 11px;
            font-weight: 600;
        }

        /* Buttons */
        .btn {
            background: linear-gradient(135deg, #2563eb, #1d4ed8);
            color: #ffffff;
            border: none;
            padding: 14px 20px;
            border-radius: 12px;
            font-size: 15px;
            font-weight: 600;
            width: 100%;
            cursor: pointer;
            transition: all 0.2s ease;
            display: flex;
            align-items: center;
            justify-content: center;
            gap: 8px;
            box-shadow: 0 4px 12px rgba(37,99,235,0.2);
            text-decoration: none;
        }
        .btn:disabled {
            background: #cbd5e1;
            color: #94a3b8;
            box-shadow: none;
            cursor: not-allowed;
            transform: none !important;
        }
        .btn:not(:disabled):active {
            transform: scale(0.98);
        }
        .btn-outline {
            background: #ffffff;
            color: #2563eb;
            border: 1.5px solid #2563eb;
            box-shadow: none;
            margin-top: 12px;
        }
        .btn-outline:active {
            background: #eff6ff;
        }

        /* Progress Bar Section */
        .progress-card {
            background: #f8fafc;
            border: 1px solid #e2e8f0;
            border-radius: 14px;
            padding: 20px 16px;
            margin-top: 16px;
            display: none;
        }
        .progress-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 10px;
        }
        .progress-label {
            font-size: 14px;
            font-weight: 600;
            color: #1e293b;
        }
        .progress-percent {
            font-size: 14px;
            font-weight: 700;
            color: #2563eb;
        }
        .progress-track {
            background: #e2e8f0;
            border-radius: 999px;
            height: 10px;
            width: 100%;
            overflow: hidden;
            position: relative;
        }
        .progress-fill {
            background: linear-gradient(90deg, #2563eb, #38bdf8);
            height: 100%;
            width: 0%;
            border-radius: 999px;
            transition: width 0.2s ease-out;
        }
        .progress-hint {
            font-size: 12px;
            color: #64748b;
            text-align: center;
            margin-top: 10px;
        }

        /* State Cards (Success & Error) */
        .state-view {
            text-align: center;
            padding: 16px 8px;
            display: none;
            animation: fadeIn 0.3s ease;
        }
        .state-icon-circle {
            width: 64px;
            height: 64px;
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            margin: 0 auto 16px;
        }
        .success-circle {
            background: #ecfdf5;
            box-shadow: 0 0 0 8px #f0fdf4;
        }
        .error-circle {
            background: #fef2f2;
            box-shadow: 0 0 0 8px #fff1f2;
        }
        .state-title {
            font-size: 18px;
            font-weight: 700;
            color: #0f172a;
            margin-bottom: 6px;
        }
        .state-desc {
            font-size: 13px;
            color: #64748b;
            line-height: 1.5;
            margin-bottom: 20px;
        }

        @keyframes fadeIn {
            from { opacity: 0; transform: translateY(6px); }
            to { opacity: 1; transform: translateY(0); }
        }
    </style>
</head>
<body>
    <div class="card">
        <!-- Brand Header -->
        <div class="header">
            <div class="logo-badge">
                <svg width="28" height="28" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
                    <path d="M19 8H5C3.34 8 2 9.34 2 11V17H6V21H18V17H22V11C22 9.34 20.66 8 19 8ZM16 19H8V14H16V19ZM19 12C18.45 12 18 11.55 18 11C18 10.45 18.45 10 19 10C19.55 10 20 10.45 20 11C20 11.55 19.55 12 19 12ZM18 3H6V7H18V3Z" fill="white"/>
                    <path d="M12 1.5C14.2 1.5 16 3.3 16 5.5H14.5C14.5 4.12 13.38 3 12 3V1.5Z" fill="#93c5fd"/>
                </svg>
            </div>
            <div class="logo-title">MR Print Hub</div>
            <div class="subtitle">{{shopName}}</div>
            <div class="mode-badge">⚡ Cloud Relay Mode</div>
        </div>

        <!-- 1. Idle / Uploading Form View -->
        <div id="uploadFormView">
            <div id="dropzoneBox" class="dropzone" onclick="selectFiles()">
                <div class="dropzone-icon">📁</div>
                <div class="dropzone-text">Tap to select files</div>
                <div class="dropzone-hint">PDF, Word DOCX, Photos, Images</div>
            </div>

            <input type="file" id="fileInput" multiple onchange="onFilesSelected(this.files)" />

            <div id="fileList" class="file-list"></div>

            <button id="uploadBtn" class="btn" disabled onclick="startUpload()">
                <span>Upload to Print PC</span>
            </button>

            <!-- Real-time Progress Bar Card -->
            <div id="progressCard" class="progress-card">
                <div class="progress-header">
                    <span id="progressLabel" class="progress-label">Uploading to Cloud...</span>
                    <span id="progressPercent" class="progress-percent">0%</span>
                </div>
                <div class="progress-track">
                    <div id="progressBar" class="progress-fill"></div>
                </div>
                <div class="progress-hint">Transferring file securely to shop print queue...</div>
            </div>
        </div>

        <!-- 2. Success State View -->
        <div id="successView" class="state-view">
            <div class="state-icon-circle success-circle">
                <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="#059669" stroke-width="3" stroke-linecap="round" stroke-linejoin="round">
                    <polyline points="20 6 9 17 4 12"></polyline>
                </svg>
            </div>
            <div class="state-title">Upload Successful!</div>
            <div class="state-desc">Your file has been transferred securely. The shop PC is receiving your print job.</div>
            <button class="btn btn-outline" onclick="resetToIdle()">Upload More Files</button>
        </div>

        <!-- 3. Error State View -->
        <div id="errorView" class="state-view">
            <div class="state-icon-circle error-circle">
                <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="#dc2626" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
                    <circle cx="12" cy="12" r="10"></circle>
                    <line x1="12" y1="8" x2="12" y2="12"></line>
                    <line x1="12" y1="16" x2="12.01" y2="16"></line>
                </svg>
            </div>
            <div class="state-title">Upload Failed</div>
            <div id="errorDesc" class="state-desc">Your file could not be uploaded. Please check your internet connection and try again.</div>
            <button class="btn btn-primary" onclick="retryUpload()">Retry Upload</button>
            <button class="btn btn-outline" onclick="resetToIdle()">Select Different Files</button>
        </div>
    </div>

    <script>
        const sessionId = "{{sessionId}}";
        const localEndpoint = "{{localEndpoint}}";
        const localToken = "{{localToken}}";
        let selectedFiles = [];
        let isUploading = false;

        function generateUploadId() {
            const now = new Date();
            const dateStr = now.toISOString().slice(0, 10).replace(/-/g, '');
            const rand = Math.random().toString(36).substring(2, 8).toUpperCase();
            return 'UPL-' + dateStr + '-' + rand;
        }

        function selectFiles() {
            if (isUploading) return;
            document.getElementById('fileInput').click();
        }

        function onFilesSelected(files) {
            if (isUploading) return;
            selectedFiles = Array.from(files);
            renderFileList();
        }

        function renderFileList() {
            const listContainer = document.getElementById('fileList');
            listContainer.innerHTML = '';

            selectedFiles.forEach(f => {
                const item = document.createElement('div');
                item.className = 'file-item';

                const nameSpan = document.createElement('span');
                nameSpan.className = 'file-name';
                nameSpan.textContent = f.name;

                const sizeSpan = document.createElement('span');
                sizeSpan.className = 'file-size';
                sizeSpan.textContent = (f.size / (1024 * 1024) >= 1)
                    ? (f.size / (1024 * 1024)).toFixed(1) + ' MB'
                    : (f.size / 1024).toFixed(0) + ' KB';

                item.appendChild(nameSpan);
                item.appendChild(sizeSpan);
                listContainer.appendChild(item);
            });

            const uploadBtn = document.getElementById('uploadBtn');
            uploadBtn.disabled = selectedFiles.length === 0 || isUploading;
        }

        function startUpload() {
            if (selectedFiles.length === 0 || isUploading) return;

            isUploading = true;
            document.getElementById('uploadBtn').disabled = true;
            document.getElementById('dropzoneBox').style.pointerEvents = 'none';
            document.getElementById('dropzoneBox').style.opacity = '0.6';

            // Show Progress Bar
            const progressCard = document.getElementById('progressCard');
            progressCard.style.display = 'block';
            updateProgress(0);

            const uploadId = generateUploadId();
            const formData = new FormData();
            formData.append('uploadId', uploadId);
            for (const file of selectedFiles) {
                formData.append('files', file);
            }

            // Local-First Intelligent Routing
            if (localEndpoint && localToken && localEndpoint.startsWith('http')) {
                probeAndUploadLocal(formData, uploadId);
            } else {
                uploadViaCloud(formData, uploadId);
            }
        }

        function probeAndUploadLocal(formData, uploadId) {
            document.getElementById('progressLabel').textContent = 'Connecting...';

            const controller = new AbortController();
            const timeoutId = setTimeout(() => controller.abort(), 1200);

            const pingUrl = localEndpoint.replace(/\/$/, '') + '/api/health';
            fetch(pingUrl, { method: 'GET', signal: controller.signal, mode: 'cors' })
                .then(res => {
                    clearTimeout(timeoutId);
                    if (res.ok) {
                        uploadViaLocal(formData, uploadId);
                    } else {
                        uploadViaCloud(formData, uploadId);
                    }
                })
                .catch(() => {
                    clearTimeout(timeoutId);
                    // Local Wi-Fi not reachable (e.g. mobile 4G data) -> seamless Cloud fallback
                    uploadViaCloud(formData, uploadId);
                });
        }

        function uploadViaLocal(formData, uploadId) {
            document.getElementById('progressLabel').textContent = 'Uploading directly via Wi-Fi...';
            const xhr = new XMLHttpRequest();
            xhr.open('POST', localEndpoint.replace(/\/$/, '') + '/api/upload/' + localToken, true);
            xhr.setRequestHeader('X-Upload-Id', uploadId);
            xhr.timeout = 30000;

            xhr.upload.addEventListener('progress', function (e) {
                if (e.lengthComputable) {
                    const percent = Math.min(Math.round((e.loaded / e.total) * 100), 98);
                    updateProgress(percent);
                }
            });

            xhr.onload = function () {
                isUploading = false;
                if (xhr.status >= 200 && xhr.status < 300) {
                    updateProgress(100);
                    setTimeout(() => { showSuccessState(); }, 350);
                } else {
                    uploadViaCloud(formData, uploadId);
                }
            };

            xhr.onerror = function () {
                uploadViaCloud(formData, uploadId);
            };

            xhr.ontimeout = function () {
                uploadViaCloud(formData, uploadId);
            };

            xhr.send(formData);
        }

        function uploadViaCloud(formData, uploadId) {
            document.getElementById('progressLabel').textContent = 'Uploading to Cloud Relay...';
            const xhr = new XMLHttpRequest();
            xhr.open('POST', '/api/v1/uploads/' + sessionId, true);
            xhr.setRequestHeader('X-Upload-Id', uploadId);

            xhr.upload.addEventListener('progress', function (e) {
                if (e.lengthComputable) {
                    const percent = Math.min(Math.round((e.loaded / e.total) * 100), 98);
                    updateProgress(percent);
                }
            });

            xhr.onload = function () {
                isUploading = false;
                if (xhr.status >= 200 && xhr.status < 300) {
                    updateProgress(100);
                    setTimeout(() => {
                        showSuccessState();
                    }, 350);
                } else {
                    let errMsg = 'Your file could not be uploaded. Please try again.';
                    try {
                        const res = JSON.parse(xhr.responseText);
                        if (res.error) errMsg = res.error;
                        else if (res.results && res.results[0] && res.results[0].message) errMsg = res.results[0].message;
                    } catch {}
                    showErrorState(errMsg);
                }
            };

            xhr.onerror = function () {
                isUploading = false;
                showErrorState('Network connection error. Please check your internet connection.');
            };

            xhr.ontimeout = function () {
                isUploading = false;
                showErrorState('Upload timed out. Please try again.');
            };

            xhr.send(formData);
        }

        function updateProgress(percent) {
            document.getElementById('progressBar').style.width = percent + '%';
            document.getElementById('progressPercent').textContent = percent + '%';
            document.getElementById('progressLabel').textContent = percent === 100 ? 'Finishing...' : 'Transferring file...';
        }

        function showSuccessState() {
            document.getElementById('uploadFormView').style.display = 'none';
            document.getElementById('errorView').style.display = 'none';
            document.getElementById('successView').style.display = 'block';
            selectedFiles = [];
        }

        function showErrorState(msg) {
            document.getElementById('uploadFormView').style.display = 'none';
            document.getElementById('successView').style.display = 'none';
            if (msg) document.getElementById('errorDesc').textContent = msg;
            document.getElementById('errorView').style.display = 'block';
        }

        function retryUpload() {
            document.getElementById('errorView').style.display = 'none';
            document.getElementById('uploadFormView').style.display = 'block';
            startUpload();
        }

        function resetToIdle() {
            isUploading = false;
            selectedFiles = [];
            document.getElementById('fileInput').value = '';
            document.getElementById('fileList').innerHTML = '';
            document.getElementById('uploadBtn').disabled = true;
            document.getElementById('dropzoneBox').style.pointerEvents = 'auto';
            document.getElementById('dropzoneBox').style.opacity = '1';
            document.getElementById('progressCard').style.display = 'none';
            updateProgress(0);

            document.getElementById('successView').style.display = 'none';
            document.getElementById('errorView').style.display = 'none';
            document.getElementById('uploadFormView').style.display = 'block';
        }
    </script>
</body>
</html>
""";

static string? ResolvePostgreSqlConnectionString(string? raw)
{
    if (string.IsNullOrWhiteSpace(raw)) return null;
    raw = raw.Trim();

    if (raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            var uri = new Uri(raw);
            var userInfo = uri.UserInfo.Split(':');
            var username = Uri.UnescapeDataString(userInfo[0]);
            var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : 5432;
            var database = uri.AbsolutePath.TrimStart('/');

            var npgsqlBuilder = new Npgsql.NpgsqlConnectionStringBuilder
            {
                Host = host,
                Port = port,
                Database = string.IsNullOrWhiteSpace(database) ? "postgres" : database,
                Username = username,
                Password = password,
                SslMode = Npgsql.SslMode.Require,
                TrustServerCertificate = true
            };

            return npgsqlBuilder.ConnectionString;
        }
        catch
        {
            return raw;
        }
    }

    return raw;
}

public partial class Program { }
