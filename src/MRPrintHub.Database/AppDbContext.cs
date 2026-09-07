using Microsoft.EntityFrameworkCore;
using MRPrintHub.Database.Entities;

namespace MRPrintHub.Database;

public class AppDbContext : DbContext
{
    public DbSet<SettingsEntity> Settings => Set<SettingsEntity>();
    public DbSet<SessionEntity> Sessions => Set<SessionEntity>();
    public DbSet<UploadEntity> Uploads => Set<UploadEntity>();
    public DbSet<ApplicationLogEntity> ApplicationLogs => Set<ApplicationLogEntity>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SettingsEntity>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.Key).IsUnique();
            e.Property(s => s.Key).HasMaxLength(256);
            e.Property(s => s.Value).HasMaxLength(4096);
        });

        modelBuilder.Entity<SessionEntity>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.TokenHash).IsUnique();
            e.Property(s => s.TokenHash).HasMaxLength(128);
            e.Property(s => s.TokenPrefix).HasMaxLength(32);
            e.Property(s => s.IpAddress).HasMaxLength(45);
            e.Property(s => s.InterfaceName).HasMaxLength(256);
        });

        modelBuilder.Entity<UploadEntity>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.SessionId);
            e.Property(u => u.OriginalFilename).HasMaxLength(260);
            e.Property(u => u.StoredFilename).HasMaxLength(260);
            e.Property(u => u.ErrorReason).HasMaxLength(1024);
            e.HasOne(u => u.Session)
                .WithMany()
                .HasForeignKey(u => u.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApplicationLogEntity>(e =>
        {
            e.HasKey(l => l.Id);
            e.HasIndex(l => l.CreatedAt);
            e.Property(l => l.Event).HasMaxLength(128);
            e.Property(l => l.Message).HasMaxLength(4096);
            e.Property(l => l.Level).HasMaxLength(32);
            e.Property(l => l.Details).HasMaxLength(4096);
        });
    }
}
