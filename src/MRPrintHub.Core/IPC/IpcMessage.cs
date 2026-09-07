namespace MRPrintHub.Core.IPC;

public abstract record IpcMessage(string Type);

public record StatusRequest(IpcMessage? Payload = null) : IpcMessage("StatusRequest");
public record StatusResponse(IpcMessage? Payload = null) : IpcMessage("StatusResponse");
public record UploadEvent(IpcMessage? Payload = null) : IpcMessage("UploadEvent");
public record CommandRequest(string Command, IpcMessage? Payload = null) : IpcMessage("CommandRequest");
public record CommandResponse(string Command, bool Success, IpcMessage? Payload = null) : IpcMessage("CommandResponse");
