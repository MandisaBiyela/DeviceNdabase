using DeviceDesk.Modules.Phase3.Models;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase3.Data;

public class Phase3DbContext : DbContext
{
    public Phase3DbContext(DbContextOptions<Phase3DbContext> options) : base(options) { }

    public DbSet<DispatchTrip> DispatchTrips { get; set; }
    public DbSet<DispatchPOD> DispatchPODs { get; set; }
    
    // New Batch Workflow Tables
    public DbSet<DispatchBatch> DispatchBatches { get; set; }
    public DbSet<BatchDevice> BatchDevices { get; set; }
    public DbSet<LoadingAuditScan> LoadingAuditScans { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // DispatchTrip configuration
        modelBuilder.Entity<DispatchTrip>(entity =>
        {
            entity.ToTable("Phase3_DispatchTrips");
            entity.HasKey(e => e.TripId);
            entity.Property(e => e.TripRef).IsRequired().HasMaxLength(64);
            entity.Property(e => e.DriverName).IsRequired().HasMaxLength(128);
            entity.Property(e => e.VehicleReg).IsRequired().HasMaxLength(32);
            entity.Property(e => e.DriverUserId).HasMaxLength(450);
            entity.Property(e => e.CreatedByUserId).HasMaxLength(450);
            entity.Property(e => e.CompletedByUserId).HasMaxLength(450);
            entity.Property(e => e.DebriefingByUserId).HasMaxLength(450);
            entity.Property(e => e.FinalSignOffByUserId).HasMaxLength(450);
            
            entity.HasIndex(e => e.TripRef).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.DriverUserId);
        });

        // DispatchPOD configuration
        modelBuilder.Entity<DispatchPOD>(entity =>
        {
            entity.ToTable("Phase3_DispatchPODs");
            entity.HasKey(e => e.PODId);
            entity.Property(e => e.PODNumber).IsRequired().HasMaxLength(64);
            entity.Property(e => e.DeliveryNoteNumber).HasMaxLength(64);
            entity.Property(e => e.SchoolName).IsRequired().HasMaxLength(256);
            entity.Property(e => e.District).HasMaxLength(128);
            entity.Property(e => e.EmisCode).HasMaxLength(32);
            entity.Property(e => e.StockType).IsRequired().HasMaxLength(32);
            entity.Property(e => e.SourceReference).HasMaxLength(128);
            entity.Property(e => e.CreatedByUserId).HasMaxLength(450);
            entity.Property(e => e.SignedPODUploadedByUserId).HasMaxLength(450);
            entity.Property(e => e.SchoolSignatoryName).HasMaxLength(256);
            
            entity.HasIndex(e => e.PODNumber).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.TripId);
            
            entity.HasOne(e => e.Trip)
                .WithMany(t => t.PODs)
                .HasForeignKey(e => e.TripId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // DispatchBatch configuration
        modelBuilder.Entity<DispatchBatch>(entity =>
        {
            entity.ToTable("Phase3_DispatchBatches");
            entity.HasKey(e => e.BatchId);
            entity.Property(e => e.SchoolName).IsRequired().HasMaxLength(256);
            entity.Property(e => e.District).HasMaxLength(128);
            entity.Property(e => e.EmisCode).HasMaxLength(32);
            entity.Property(e => e.StockType).IsRequired().HasMaxLength(32);
            entity.Property(e => e.SourceReference).HasMaxLength(128);
            entity.Property(e => e.PODNumber).HasMaxLength(64);
            entity.Property(e => e.DeliveryNoteNumber).HasMaxLength(64);
            entity.Property(e => e.TripReference).HasMaxLength(64);
            entity.Property(e => e.DriverName).HasMaxLength(128);
            entity.Property(e => e.DriverUserId).HasMaxLength(450);
            entity.Property(e => e.VehicleReg).HasMaxLength(32);
            entity.Property(e => e.CreatedByUserId).HasMaxLength(450);
            entity.Property(e => e.AuditCompletedByUserId).HasMaxLength(450);
            entity.Property(e => e.DebriefCompletedByUserId).HasMaxLength(450);
            entity.Property(e => e.SchoolSignatoryName).HasMaxLength(256);
            
            entity.HasIndex(e => e.PODNumber).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.SchoolName);
        });

        // BatchDevice configuration
        modelBuilder.Entity<BatchDevice>(entity =>
        {
            entity.ToTable("Phase3_BatchDevices");
            entity.HasKey(e => e.BatchDeviceId);
            entity.Property(e => e.Serial).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Model).HasMaxLength(128);
            entity.Property(e => e.Condition).HasMaxLength(32);
            entity.Property(e => e.AddedByUserId).HasMaxLength(450);
            entity.Property(e => e.ScannedByUserId).HasMaxLength(450);
            
            entity.HasIndex(e => e.BatchId);
            entity.HasIndex(e => e.DeviceId);
            entity.HasIndex(e => e.Serial);
            
            entity.HasOne(e => e.Batch)
                .WithMany(b => b.Devices)
                .HasForeignKey(e => e.BatchId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // LoadingAuditScan configuration
        modelBuilder.Entity<LoadingAuditScan>(entity =>
        {
            entity.ToTable("Phase3_LoadingAuditScans");
            entity.HasKey(e => e.AuditId);
            entity.Property(e => e.ScannedSerials).IsRequired();
            entity.Property(e => e.AuditedByUserId).HasMaxLength(450);
            
            entity.HasIndex(e => e.BatchId);
            entity.HasIndex(e => e.StartedAt);
            
            entity.HasOne(e => e.Batch)
                .WithMany(b => b.AuditScans)
                .HasForeignKey(e => e.BatchId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
