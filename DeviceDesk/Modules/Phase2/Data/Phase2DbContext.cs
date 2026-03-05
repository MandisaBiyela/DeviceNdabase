using DeviceDesk.Modules.Phase2.Models;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase2.Data;

public class Phase2DbContext : DbContext
{
    public Phase2DbContext(DbContextOptions<Phase2DbContext> options) : base(options) {}

    public DbSet<Phase2Device> Devices => Set<Phase2Device>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<AssessmentRecord> Assessments => Set<AssessmentRecord>();
    public DbSet<QualityRecord> Quality => Set<QualityRecord>();
    public DbSet<DisposalRecord> Disposals => Set<DisposalRecord>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<DeviceStorageLocation> DeviceStorageLocations => Set<DeviceStorageLocation>();
    public DbSet<SchoolStorageTemplate> SchoolStorageTemplates => Set<SchoolStorageTemplate>();
    public DbSet<StorageSlotOccupancy> StorageSlotOccupancies => Set<StorageSlotOccupancy>();
    public DbSet<BulkAllocationSession> BulkAllocationSessions => Set<BulkAllocationSession>();
    public DbSet<DeviceScan> DeviceScans => Set<DeviceScan>();
    public DbSet<PickingSlip> PickingSlips => Set<PickingSlip>();
    public DbSet<PickingSlipItem> PickingSlipItems => Set<PickingSlipItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Use unique table names to avoid conflicts with other modules
        modelBuilder.Entity<Phase2Device>().ToTable("Phase2Devices");
        modelBuilder.Entity<Receipt>().ToTable("Phase2Receipts");
        modelBuilder.Entity<AssessmentRecord>().ToTable("Phase2Assessments");
        modelBuilder.Entity<QualityRecord>().ToTable("Phase2Quality");
        modelBuilder.Entity<DisposalRecord>().ToTable("Phase2Disposals");
        modelBuilder.Entity<AuditLog>().ToTable("Phase2AuditLogs");
        modelBuilder.Entity<Phase2Device>()
            .HasIndex(d => d.Serial);
        modelBuilder.Entity<Receipt>()
            .HasMany(r => r.Devices)
            .WithOne(d => d.Receipt)
            .HasForeignKey(d => d.ReceiptId)
            .OnDelete(DeleteBehavior.Cascade);

        // DeviceStorageLocation configuration
        modelBuilder.Entity<DeviceStorageLocation>(b =>
        {
            b.ToTable("Phase2DeviceStorageLocations");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.Phase2DeviceId);
            b.HasIndex(x => new { x.Phase2DeviceId, x.Status })
                .HasFilter("[Status] = 'Active'")
                .IsUnique();

            b.HasOne(x => x.Phase2Device)
                .WithMany()
                .HasForeignKey(x => x.Phase2DeviceId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Property(x => x.Building).HasMaxLength(128);
            b.Property(x => x.Room).HasMaxLength(128);
            b.Property(x => x.Rack).HasMaxLength(64);
            b.Property(x => x.Shelf).HasMaxLength(64);
            b.Property(x => x.Bin).HasMaxLength(64);
            b.Property(x => x.Status).HasMaxLength(64);
            b.Property(x => x.CreatedByUserId).HasMaxLength(128);

            // Bulk session relationship
            b.HasOne(x => x.BulkSession)
                .WithMany(s => s.Allocations)
                .HasForeignKey(x => x.BulkSessionId)
                .OnDelete(DeleteBehavior.SetNull);
            
            b.HasIndex(x => x.BulkSessionId);
        });

        // SchoolStorageTemplate configuration
        modelBuilder.Entity<SchoolStorageTemplate>(b =>
        {
            b.ToTable("Phase2SchoolStorageTemplates");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.SchoolId, x.Category }).IsUnique();
            b.Property(x => x.Building).HasMaxLength(128);
            b.Property(x => x.Room).HasMaxLength(128);
            b.Property(x => x.RackPattern).HasMaxLength(64);
            b.Property(x => x.ShelfPattern).HasMaxLength(64);
            b.Property(x => x.BinPattern).HasMaxLength(64);
            b.Property(x => x.Category).HasConversion<int>();
        });

        // StorageSlotOccupancy configuration
        modelBuilder.Entity<StorageSlotOccupancy>(b =>
        {
            b.ToTable("Phase2StorageSlotOccupancies");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.SchoolId, x.Category, x.Building, x.Room, x.Rack, x.Shelf, x.Bin });
            b.HasIndex(x => x.Phase2DeviceId);
            b.HasIndex(x => new { x.SchoolId, x.Category, x.IsOccupied })
                .HasFilter("[IsOccupied] = 1");
            
            b.HasOne(x => x.Phase2Device)
                .WithMany()
                .HasForeignKey(x => x.Phase2DeviceId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Property(x => x.Building).HasMaxLength(128);
            b.Property(x => x.Room).HasMaxLength(128);
            b.Property(x => x.Rack).HasMaxLength(64);
            b.Property(x => x.Shelf).HasMaxLength(64);
            b.Property(x => x.Bin).HasMaxLength(64);
            b.Property(x => x.Category).HasConversion<int>();
        });

        // BulkAllocationSession configuration
        modelBuilder.Entity<BulkAllocationSession>(b =>
        {
            b.ToTable("Phase2BulkAllocationSessions");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.SchoolId);
            b.HasIndex(x => x.CreatedAt);
            
            b.Property(x => x.SchoolName).HasMaxLength(256);
            b.Property(x => x.CreatedBy).HasMaxLength(128);
            b.Property(x => x.Status).HasConversion<int>();
        });

        // DeviceScan configuration
        modelBuilder.Entity<DeviceScan>(b =>
        {
            b.ToTable("Phase2DeviceScans");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.DeviceSerial);
            b.HasIndex(x => x.ScanTime);
            
            b.Property(x => x.DeviceSerial).HasMaxLength(100);
            b.Property(x => x.ScannedBy).HasMaxLength(128);
            b.Property(x => x.Location).HasMaxLength(255);
            b.Property(x => x.Purpose).HasMaxLength(255);
        });

        // PickingSlip configuration
        modelBuilder.Entity<PickingSlip>(b =>
        {
            b.ToTable("Phase2PickingSlips");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.SlipNumber).IsUnique();
            b.HasIndex(x => x.SchoolId);
            b.HasIndex(x => x.CreatedAt);
            b.HasIndex(x => x.Status);
            
            b.Property(x => x.SlipNumber).HasMaxLength(64).IsRequired();
            b.Property(x => x.Reference).HasMaxLength(256);
            b.Property(x => x.SchoolName).HasMaxLength(256);
            b.Property(x => x.District).HasMaxLength(128);
            b.Property(x => x.CreatedByUserId).HasMaxLength(128).IsRequired();
            b.Property(x => x.Status).HasConversion<int>();

            b.HasMany(x => x.Items)
                .WithOne(i => i.PickingSlip)
                .HasForeignKey(i => i.PickingSlipId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PickingSlipItem configuration
        modelBuilder.Entity<PickingSlipItem>(b =>
        {
            b.ToTable("Phase2PickingSlipItems");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.PickingSlipId);
            b.HasIndex(x => x.Phase2DeviceId);
            
            // Prevent device from appearing on multiple active slips
            // This is enforced via query logic, but we add an index for performance
            b.HasIndex(x => new { x.Phase2DeviceId, x.PickingSlipId });

            b.Property(x => x.Serial).HasMaxLength(100).IsRequired();
            b.Property(x => x.SchoolName).HasMaxLength(256);
            b.Property(x => x.District).HasMaxLength(128);
            b.Property(x => x.Building).HasMaxLength(128);
            b.Property(x => x.Room).HasMaxLength(128);
            b.Property(x => x.Rack).HasMaxLength(64);
            b.Property(x => x.Shelf).HasMaxLength(64);
            b.Property(x => x.Bin).HasMaxLength(64);
            b.Property(x => x.PickedByUserId).HasMaxLength(128);
            b.Property(x => x.StageAtCreation).HasConversion<int>();

            b.HasOne(x => x.PickingSlip)
                .WithMany(s => s.Items)
                .HasForeignKey(x => x.PickingSlipId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Phase2Device)
                .WithMany()
                .HasForeignKey(x => x.Phase2DeviceId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent deletion of device if on picking slip
        });
    }
}
