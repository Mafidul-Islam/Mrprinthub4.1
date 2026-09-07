using System.IO.Pipes;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using MRPrintHub.Core.Network;
using MRPrintHub.QR;
using MRPrintHub.Server;
using MRPrintHub.Storage;

namespace MRPrintHub.Service;

public class ServiceHost : BackgroundService
{
    private readonly IServerFactory _serverFactory;
    private readonly INetworkStatusProvider _networkProvider;
    private readonly IStorageService _storage;
    private readonly IQrCodeGenerator _qrGenerator;
    private WebApplication? _server;
    private NamedPipeServerStream? _pipe;
    private CancellationTokenSource? _cts;

    public ServiceHost(
        IServerFactory serverFactory,
        INetworkStatusProvider networkProvider,
        IStorageService storage,
        IQrCodeGenerator qrGenerator)
    {
        _serverFactory = serverFactory;
        _networkProvider = networkProvider;
        _storage = storage;
        _qrGenerator = qrGenerator;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

        try
        {
            await SetupServerAsync(_cts.Token);
            SetupPipeServer();

            while (!_cts.IsCancellationRequested)
            {
                if (_pipe != null && !_pipe.IsConnected)
                {
                    await _pipe.WaitForConnectionAsync(_cts.Token);
                }

                var message = await ReadPipeMessageAsync(_cts.Token);
                if (message == null)
                {
                    try
                    {
                        _pipe?.Disconnect();
                    }
                    catch
                    {
                        // Ignore disconnect exceptions
                    }
                    continue;
                }

                await HandlePipeMessageAsync(message, _cts.Token);
            }
        }
        catch (OperationCanceledException) when (_cts?.IsCancellationRequested == true)
        {
            // Graceful shutdown
        }
    }

    private async Task SetupServerAsync(CancellationToken token)
    {
        _networkProvider.Start();
        _server = _serverFactory.CreateServer(_networkProvider, _storage, _qrGenerator);
        await _server.StartAsync(token);

        // Wait for network status to be ready
        await Task.Delay(2000, token);
    }

    private void SetupPipeServer()
    {
        const string pipeName = "MRPrintHub_IPC";
        _pipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
    }

    private async Task<string?> ReadPipeMessageAsync(CancellationToken token)
    {
        if (_pipe == null || !_pipe.IsConnected)
            return null;

        try
        {
            using var reader = new StreamReader(_pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            var message = await reader.ReadLineAsync(token);
            return message;
        }
        catch
        {
            return null;
        }
    }

    private async Task HandlePipeMessageAsync(string message, CancellationToken token)
    {
        if (string.IsNullOrEmpty(message)) return;

        var parts = message.Split('|');
        var command = parts[0];

        switch (command)
        {
            case "STATUS":
                var tokenValue = parts.Length > 1 ? parts[1] : "";
                var status = _storage.GetUploadStatus(tokenValue);
                var response = $"STATUS|{status}";
                await WritePipeResponseAsync(response, token);
                break;

            case "UPLOADS":
                var uploadsResponse = $"UPLOADS|{_storage.GetAllTokenStatuses()}";
                await WritePipeResponseAsync(uploadsResponse, token);
                break;

            default:
                await WritePipeResponseAsync($"UNKNOWN|{command}", token);
                break;
        }
    }

    private async Task WritePipeResponseAsync(string message, CancellationToken token)
    {
        if (_pipe == null || !_pipe.IsConnected)
            return;

        var data = Encoding.UTF8.GetBytes(message + "\n");
        await _pipe.WriteAsync(data, 0, data.Length, token);
        await _pipe.FlushAsync(token);
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _cts?.Cancel();
        _networkProvider.Stop();
        if (_server != null)
        {
            await _server.StopAsync(stoppingToken);
            await _server.DisposeAsync();
        }
        _pipe?.Dispose();
        await base.StopAsync(stoppingToken);
    }
}
