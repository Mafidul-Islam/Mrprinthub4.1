using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MRPrintHub.Desktop;

public class IPCClient : IDisposable
{
    private readonly NamedPipeClientStream _pipe;
    private readonly CancellationTokenSource _cts;
    private bool _connected;

    public IPCClient(string pipeName = "MRPrintHub_IPC")
    {
        _pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        _cts = new CancellationTokenSource();
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_connected) return;
        await _pipe.ConnectAsync(cancellationToken);
        _connected = true;
    }

    public async Task DisconnectAsync()
    {
        if (!_connected) return;
        _connected = false;
        await _pipe.DisposeAsync();
    }

    public async Task<string> SendRequestAsync(string message, CancellationToken cancellationToken = default)
    {
        if (!_connected) await ConnectAsync(cancellationToken);

        var data = Encoding.UTF8.GetBytes(message + "\n");
        await _pipe.WriteAsync(data, 0, data.Length, cancellationToken);
        await _pipe.FlushAsync(cancellationToken);

        using var reader = new StreamReader(_pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
        var response = await reader.ReadLineAsync(cancellationToken);
        return response ?? string.Empty;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _pipe.Dispose();
    }
}
