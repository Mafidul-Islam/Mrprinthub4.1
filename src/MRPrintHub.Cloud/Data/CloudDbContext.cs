using Microsoft.EntityFrameworkCore;
using MRPrintHub.Cloud.Data.Entities;

namespace MRPrintHub.Cloud.Data;

public class CloudDbContext : DbContext
{
    public DbSet<ShopEntity> Shops => Set<ShopEntity>();
    public DbSet<DeviceEntity> Devices => Set<DeviceEntity>();
    public DbSet<UploadSessionEntity> UploadSessions => Set<UploadSessionEntity>();
    public DbSet<CloudUploadEntity> Uploads => Set<CloudUploadEntity>();

    public CloudDbContext(DbContextOptions<CloudDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Shop configuration
        modelBuilder.Entity<ShopEntity>(b =>
        {
            b.HasKey(s => s.Id);
            b.Property(s => s.ShopCode).HasMaxLength(32).IsRequired();
            b.Property(s => s.Name).HasMaxLength(128);
            b.HasIndex(s => s.ShopCode).IsUnique();
        });

        // Device configuration
        modelBuilder.Entity<DeviceEntity>(b =>
        {
            b.HasKey(d => d.Id);
            b.Property(d => d.DeviceId).HasMaxLength(64).IsRequired();
            b.Property(d => d.DeviceName).HasMaxLength(128).IsRequired();
            b.Property(d => d.DeviceTokenHash).HasMaxLength(128).IsRequired();
            b.Property(d => d.HardwareFingerprint).HasMaxLength(128);
            b.Property(d => d.ClientVersion).HasMaxLength(32);
            b.Property(d => d.LastConnectionId).HasMaxLength(128);

            b.HasIndex(d => d.DeviceId).IsUnique();
            b.HasIndex(d => d.ShopId);

            b.HasOne(d => d.Shop)
             .WithMany(s => s.Devices)
             .HasForeignKey(d => d.ShopId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Upload Session configuration
        modelBuilder.Entity<UploadSessionEntity>(b =>
        {
            b.HasKey(us => us.Id);
            b.Property(us => us.SessionId).HasMaxLength(64).IsRequired();
            b.HasIndex(us => us.SessionId).IsUnique();
            b.HasIndex(us => us.ShopId);

            b.HasOne(us => us.Shop)
             .WithMany()
             .HasForeignKey(us => us.ShopId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Cloud Upload configuration
        modelBuilder.Entity<CloudUploadEntity>(b =>
        {
            b.HasKey(u => u.Id);
            b.Property(u => u.UploadId).HasMaxLength(64).IsRequired();
            b.Property(u => u.OriginalFilename).HasMaxLength(255).IsRequired();
            b.Property(u => u.StoredFilename).HasMaxLength(255).IsRequired();
            b.Property(u => u.ContentType).HasMaxLength(128);
            b.Property(u => u.DownloadToken).HasMaxLength(128).IsRequired();
            b.Property(u => u.DeliveredToDeviceId).HasMaxLength(64);

            b.HasIndex(u => u.UploadId).IsUnique();
            b.HasIndex(u => u.SessionId);
            b.HasIndex(u => u.ShopId);
            b.HasIndex(u => u.Status);

            b.HasOne(u => u.Session)
             .WithMany(s => s.Uploads)
             .HasForeignKey(u => u.SessionId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(u => u.Shop)
             .WithMany()
             .HasForeignKey(u => u.ShopId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
