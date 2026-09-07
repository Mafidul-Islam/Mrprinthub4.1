namespace MRPrintHub.Core.IPC;

public interface IpcContract
{
    IpcMessage Request(IpcMessage message);
    IpcMessage Invoke(string command, IpcMessage? payload = null);
}
