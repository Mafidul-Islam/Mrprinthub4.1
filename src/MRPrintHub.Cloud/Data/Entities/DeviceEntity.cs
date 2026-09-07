using System;

namespace MRPrintHub.Cloud.Data.Entities;

/// <summary>
/// Represents a registered shop PC / desktop device.
/// </summary>
public class DeviceEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ShopId { get; set; }

    public ShopEntity Shop { get; set; } = null!;

    /// <summary>
    /// Unique public device identifier, e.g. DEV-XXXXXXXX.
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 hash of the device authentication secret token.
    /// Raw token is NEVER stored in database.
    /// </summary>
    public string DeviceTokenHash { get; set; } = string.Empty;

    public string? HardwareFingerprint { get; set; }

    public string? ClientVersion { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastSeenAt { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsConnected { get; set; } = false;

    public string? LastConnectionId { get; set; }
}
