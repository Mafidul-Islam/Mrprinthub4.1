namespace MRPrintHub.Core.Enums;

/// <summary>
/// State of the persistent connection between Desktop and Cloud Server.
/// </summary>
public enum CloudConnectionState
{
    /// <summary>
    /// Not connected to the cloud server.
    /// </summary>
    Disconnected,

    /// <summary>
    /// Actively establishing handshake/authentication with the cloud server.
    /// </summary>
    Connecting,

    /// <summary>
    /// Successfully connected, authenticated, and maintaining heartbeat.
    /// </summary>
    Connected,

    /// <summary>
    /// Disconnected unexpectedly; attempting exponential backoff reconnection.
    /// </summary>
    Reconnecting,

    /// <summary>
    /// Connection failed due to invalid credentials, network failure, or fatal error.
    /// </summary>
    Failed
}
