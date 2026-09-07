using System;
using System.Collections.Generic;

namespace MRPrintHub.Cloud.Data.Entities;

/// <summary>
/// Represents a registered shop / print center in the MR Print Hub Cloud.
/// </summary>
public class ShopEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Unique public Shop ID formatted as MRPH-XXXXXXXX.
    /// </summary>
    public string ShopCode { get; set; } = string.Empty;

    /// <summary>
    /// Optional display name or description for the shop.
    /// </summary>
    public string? Name { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Devices registered to this shop.
    /// </summary>
    public ICollection<DeviceEntity> Devices { get; set; } = new List<DeviceEntity>();
}
