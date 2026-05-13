using DeviceDesk.Infrastructure.Data.Enums;
using DeviceDesk.Modules.Phase0.Models;
using Microsoft.EntityFrameworkCore;
 

namespace DeviceDesk.Infrastructure.Data
{
    public class DeviceDeskDbContext : DbContext
    {
        public DeviceDeskDbContext(DbContextOptions<DeviceDeskDbContext> options) : base(options) { }

        public DbSet<School> Schools => Set<School>();
        public DbSet<Device> Devices => Set<Device>();
        public DbSet<DeviceImportBatch> Batches => Set<DeviceImportBatch>();
        public DbSet<Document> Documents => Set<Document>();
        public DbSet<DispatchPod> DispatchPods => Set<DispatchPod>();
        public DbSet<DispatchTrip> DispatchTrips => Set<DispatchTrip>();
        public DbSet<ReadinessReport> ReadinessReports => Set<ReadinessReport>();
        public DbSet<ReadinessRoom> ReadinessRooms => Set<ReadinessRoom>();
        public DbSet<ReadinessRoomItem> ReadinessRoomItems => Set<ReadinessRoomItem>();
        public DbSet<ReadinessEvidence> ReadinessEvidence => Set<ReadinessEvidence>();
        public DbSet<StorageLocation> StorageLocations => Set<StorageLocation>();
        public DbSet<DeviceLocation> DeviceLocations => Set<DeviceLocation>();
        public DbSet<DeviceLocationHistory> DeviceLocationHistory => Set<DeviceLocationHistory>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        // R&R (Retention & Retrieval) workflow tables
        public DbSet<RnrBatch> RnrBatches => Set<RnrBatch>();
        public DbSet<RnrBatchItem> RnrBatchItems => Set<RnrBatchItem>();
        
        // New Stock Batch tables
        public DbSet<NewStockBatch> NewStockBatches => Set<NewStockBatch>();
        public DbSet<NewStockBatchItem> NewStockBatchItems => Set<NewStockBatchItem>();
        public DbSet<NewStockScannedDevice> NewStockScannedDevices => Set<NewStockScannedDevice>();
        // Model-driven scanning tables (Phase 1 uses these)
        public DbSet<OrderModelList> OrderModelLists => Set<OrderModelList>();
        public DbSet<ScannedSerial> ScannedSerials => Set<ScannedSerial>();
        public DbSet<ProcurementOrder> ProcurementOrders => Set<ProcurementOrder>();
        public DbSet<ProcurementOrderSchool> ProcurementOrderSchools => Set<ProcurementOrderSchool>();
        public DbSet<ProcurementOrderItem> ProcurementOrderItems => Set<ProcurementOrderItem>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<School>(entity =>
            {
                entity.HasIndex(x => x.EmisCode).IsUnique();
                entity.Property(x => x.EmisCode).HasMaxLength(50);
                entity.Property(x => x.Name).HasMaxLength(256);
                entity.Property(x => x.District).HasMaxLength(100);
                entity.Property(x => x.Cmc).HasMaxLength(100);
                entity.Property(x => x.Circuit).HasMaxLength(100);
                entity.Property(x => x.NatEmis).HasMaxLength(50);
            });

            // Enforce uniqueness for non-null keys (filtered unique indexes)
            b.Entity<Device>()
                .HasIndex(x => x.SerialNumber)
                .IsUnique()
                .HasFilter("[SerialNumber] IS NOT NULL");
            b.Entity<Device>()
                .HasIndex(x => x.IMEI)
                .IsUnique()
                .HasFilter("[IMEI] IS NOT NULL");
            b.Entity<Device>().HasIndex(x => new { x.Source, x.SchoolId });
            b.Entity<Device>().Property(x => x.SchoolName).HasMaxLength(256);
            b.Entity<Device>().Property(x => x.Category).HasConversion<int>();

            b.Entity<DeviceImportBatch>().HasKey(x => x.BatchId);
            b.Entity<DeviceImportBatch>().HasIndex(x => new { x.Source, x.SchoolId, x.CreatedAt });
            // Explicitly map to existing Phase 0 table name to avoid 'Batches' mismatch
            b.Entity<DeviceImportBatch>().ToTable("DeviceImportBatch");

            // Store timestamps as UTC datetimeoffset with DB defaults
            b.Entity<Device>()
                .Property(x => x.ImportedAt)
                .HasColumnType("datetimeoffset(7)")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            b.Entity<DeviceImportBatch>()
                .Property(x => x.CreatedAt)
                .HasColumnType("datetimeoffset(7)")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            b.Entity<Document>()
                .Property(x => x.UploadedAt)
                .HasColumnType("datetimeoffset(7)")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            

            // Dispatch Pods
            b.Entity<DispatchPod>(e =>
            {
                e.ToTable("DispatchPods");
                e.HasIndex(x => x.PodNumber).IsUnique();
                e.Property(x => x.Status).HasConversion<int>();
                e.Property(x => x.CreatedAt).HasColumnType("datetimeoffset(7)").HasDefaultValueSql("SYSUTCDATETIME()");
            });

            b.Entity<DispatchTrip>(e =>
            {
                e.ToTable("DispatchTrips");
                e.HasKey(x => x.TripId);
                e.HasIndex(x => x.TripRef).IsUnique();
                e.Property(x => x.CreatedAt).HasColumnType("datetimeoffset(7)").HasDefaultValueSql("SYSUTCDATETIME()");
            });

            // Readiness entities
            b.Entity<ReadinessReport>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.EmisCode).HasMaxLength(32).IsRequired();
                e.Property(x => x.SchoolName).HasMaxLength(256).IsRequired();
                e.Property(x => x.District).HasMaxLength(128);
                e.Property(x => x.SubmittedByUserId).HasMaxLength(128);
                e.Property(x => x.State).HasConversion<int>();
                e.Property(x => x.CreatedAt).HasColumnType("datetimeoffset(7)").HasDefaultValueSql("SYSUTCDATETIME()");
                e.Property(x => x.UpdatedAt).HasColumnType("datetimeoffset(7)").HasDefaultValueSql("SYSUTCDATETIME()");
                e.Property(x => x.SubmittedAt).HasColumnType("datetimeoffset(7)");
                e.Property(x => x.ReviewedAt).HasColumnType("datetimeoffset(7)");
                e.HasMany(x => x.Rooms).WithOne(x => x.Report).HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(x => x.Evidence).WithOne(x => x.Report).HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(x => new { x.EmisCode, x.State });
                e.ToTable("ReadinessReports");
            });

            b.Entity<ReadinessRoom>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.RoomCode).HasMaxLength(64).IsRequired();
                e.Property(x => x.RoomName).HasMaxLength(128).IsRequired();
                e.Property(x => x.CreatedAt).HasColumnType("datetimeoffset(7)").HasDefaultValueSql("SYSUTCDATETIME()");
                e.Property(x => x.UpdatedAt).HasColumnType("datetimeoffset(7)").HasDefaultValueSql("SYSUTCDATETIME()");
                e.HasMany(x => x.Items).WithOne(x => x.Room).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(x => x.Evidence).WithOne(x => x.Room).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => x.ReportId);
                e.HasIndex(x => new { x.ReportId, x.RoomCode }).IsUnique();
                e.ToTable("ReadinessRooms");
            });

            b.Entity<ReadinessRoomItem>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.ChecklistKey).HasMaxLength(64).IsRequired();
                e.Property(x => x.Notes).HasMaxLength(1024);
                e.Property(x => x.Severity).HasConversion<int>();
                e.Property(x => x.CreatedAt).HasColumnType("datetimeoffset(7)").HasDefaultValueSql("SYSUTCDATETIME()");
                e.Property(x => x.UpdatedAt).HasColumnType("datetimeoffset(7)").HasDefaultValueSql("SYSUTCDATETIME()");
                e.HasIndex(x => x.RoomId);
                e.HasIndex(x => new { x.RoomId, x.ChecklistKey }).IsUnique();
                e.ToTable("ReadinessRoomItems");
            });

            b.Entity<ReadinessEvidence>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Kind).HasConversion<int>();
                e.Property(x => x.StoragePath).HasMaxLength(512).IsRequired();
                e.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
                e.Property(x => x.Caption).HasMaxLength(512);
                e.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
                e.Property(x => x.TakenAt).HasColumnType("datetimeoffset(7)");
                e.Property(x => x.CreatedAt).HasColumnType("datetimeoffset(7)").HasDefaultValueSql("SYSUTCDATETIME()");
                e.Property(x => x.UpdatedAt).HasColumnType("datetimeoffset(7)").HasDefaultValueSql("SYSUTCDATETIME()");
                e.HasOne(x => x.Report).WithMany(x => x.Evidence).HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Room).WithMany(x => x.Evidence).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.RoomItem).WithMany().HasForeignKey(x => x.RoomItemId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => x.ReportId);
                e.HasIndex(x => x.RoomId);
                e.HasIndex(x => x.RoomItemId);
                e.HasIndex(x => new { x.ReportId, x.Sha256 }).IsUnique();
                e.ToTable("ReadinessEvidence");
            });

            // New Stock Batch configuration
            b.Entity<NewStockBatch>(e =>
            {
                e.HasKey(x => x.BatchId);
                e.HasIndex(x => x.BatchNumber).IsUnique();
                e.HasIndex(x => x.Status);
                e.HasIndex(x => x.CreatedAt);
                e.HasIndex(x => x.ProcurementOrderId);
                e.HasIndex(x => x.PoNumber);
                e.Property(x => x.Status).HasConversion<int>();
                e.Property(x => x.PoNumber).HasMaxLength(100);
                e.Property(x => x.ProjectName).HasMaxLength(200);
                e.Property(x => x.FinancialYear).HasMaxLength(20);
                e.ToTable("NewStockBatches");
            });

            b.Entity<NewStockBatchItem>(e =>
            {
                e.HasKey(x => x.ItemId);
                e.HasIndex(x => x.BatchId);
                e.Property(x => x.UnitPrice).HasColumnType("decimal(18,2)");
                e.Property(x => x.SchoolBreakdownJson).HasColumnType("nvarchar(max)");
                e.ToTable("NewStockBatchItems");
            });

            b.Entity<NewStockScannedDevice>(e =>
            {
                e.HasKey(x => x.ScanId);
                e.HasIndex(x => x.BatchId);
                e.HasIndex(x => x.SerialNumber).IsUnique();
                e.HasIndex(x => new { x.BatchId, x.SerialNumber });
                e.ToTable("NewStockScannedDevices");
            });

            // Model-driven scanning configuration
            b.Entity<OrderModelList>(e =>
            {
                e.HasKey(x => x.ModelID);
                e.HasIndex(x => x.OrderID);
                e.Property(x => x.ModelName).HasMaxLength(200);
                e.Property(x => x.Status).HasMaxLength(20);
                // Bind navigation "Order" to FK OrderID so EF does not create a shadow FK "OrderBatchId"
                // (principal key on NewStockBatch is BatchId, not Id).
                e.HasOne(x => x.Order)
                    .WithMany()
                    .HasForeignKey(x => x.OrderID)
                    .OnDelete(DeleteBehavior.Cascade);
                e.ToTable("OrderModelLists");
            });

            b.Entity<ScannedSerial>(e =>
            {
                e.HasKey(x => x.SerialID);
                e.HasIndex(x => x.OrderID);
                e.HasIndex(x => x.ModelID);
                e.HasIndex(x => x.DeviceSerial).IsUnique();
                e.Property(x => x.DeviceSerial).HasMaxLength(200);
                e.HasOne(x => x.Order)
                    .WithMany()
                    .HasForeignKey(x => x.OrderID)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Model)
                    .WithMany(x => x.ScannedSerials)
                    .HasForeignKey(x => x.ModelID)
                    .OnDelete(DeleteBehavior.Cascade);
                e.ToTable("ScannedSerials");
            });

            b.Entity<ProcurementOrder>(e =>
            {
                e.HasKey(x => x.ProcurementOrderId);
                e.HasIndex(x => x.PoNumber).IsUnique();
                e.Property(x => x.PoNumber).HasMaxLength(100).IsRequired();
                e.Property(x => x.ProjectName).HasMaxLength(200).IsRequired();
                e.Property(x => x.FinancialYear).HasMaxLength(20).IsRequired();
                e.Property(x => x.SupplierName).HasMaxLength(200);
                e.Property(x => x.ExpectedDeliveryDate).HasColumnType("datetimeoffset(7)");
                e.Property(x => x.TotalOrderValue).HasColumnType("decimal(18,2)");
                e.Property(x => x.TotalInvoicedToDepartment).HasColumnType("decimal(18,2)");
                e.Property(x => x.TotalPaidByDepartment).HasColumnType("decimal(18,2)");
                e.Property(x => x.TotalPaidToSuppliers).HasColumnType("decimal(18,2)");
                e.Property(x => x.TimelineNotes).HasColumnType("nvarchar(max)");
                e.Property(x => x.ScopeNotes).HasColumnType("nvarchar(max)");
                e.Property(x => x.CreatedAt).HasColumnType("datetimeoffset(7)").HasDefaultValueSql("SYSUTCDATETIME()");
                e.Property(x => x.UpdatedAt).HasColumnType("datetimeoffset(7)").HasDefaultValueSql("SYSUTCDATETIME()");
                e.HasIndex(x => x.NewStockBatchId);
                e.HasMany(x => x.Schools)
                    .WithOne(x => x.ProcurementOrder)
                    .HasForeignKey(x => x.ProcurementOrderId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.ToTable("ProcurementOrders");
            });

            b.Entity<ProcurementOrderSchool>(e =>
            {
                e.HasKey(x => x.ProcurementOrderSchoolId);
                e.HasIndex(x => x.ProcurementOrderId);
                e.Property(x => x.SchoolName).HasMaxLength(256).IsRequired();
                e.Property(x => x.SchoolSubTotal).HasColumnType("decimal(18,2)");
                e.HasMany(x => x.Items)
                    .WithOne(x => x.ProcurementOrderSchool)
                    .HasForeignKey(x => x.ProcurementOrderSchoolId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.ToTable("ProcurementOrderSchools");
            });

            b.Entity<ProcurementOrderItem>(e =>
            {
                e.HasKey(x => x.ProcurementOrderItemId);
                e.HasIndex(x => x.ProcurementOrderSchoolId);
                e.Property(x => x.Description).HasMaxLength(300).IsRequired();
                e.Property(x => x.Brand).HasMaxLength(100);
                e.Property(x => x.Model).HasMaxLength(100);
                e.Property(x => x.DeviceType).HasMaxLength(50);
                e.Property(x => x.UnitPrice).HasColumnType("decimal(18,2)");
                e.Property(x => x.TotalPrice).HasColumnType("decimal(18,2)");
                e.Property(x => x.DeliveryStatus).HasConversion<int>();
                e.ToTable("ProcurementOrderItems");
            });

            b.Entity<StorageLocation>(e =>
            {
                e.ToTable("StorageLocations");
                e.HasIndex(x => x.LocationCode).IsUnique();
                e.HasIndex(x => new { x.SchoolId, x.Category, x.Area });
                e.Property(x => x.Name).HasMaxLength(256).IsRequired();
                e.Property(x => x.LocationCode).HasMaxLength(128).IsRequired();
                e.Property(x => x.Category).HasConversion<int>();
                e.Property(x => x.Area).HasConversion<int>();
            });

            b.Entity<DeviceLocation>(e =>
            {
                e.ToTable("DeviceLocations");
                e.HasIndex(x => new { x.DeviceId, x.IsCurrent });
                e.Property(x => x.MovedAt).HasColumnType("datetimeoffset(7)").HasDefaultValueSql("SYSUTCDATETIME()");
                e.HasOne(x => x.StorageLocation)
                    .WithMany()
                    .HasForeignKey(x => x.StorageLocationId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Device)
                    .WithMany()
                    .HasForeignKey(x => x.DeviceId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            b.Entity<DeviceLocationHistory>(e =>
            {
                e.ToTable("DeviceLocationHistory");
                e.HasIndex(x => x.DeviceId);
                e.Property(x => x.Timestamp).HasColumnType("datetimeoffset(7)").HasDefaultValueSql("SYSUTCDATETIME()");
                e.HasOne(x => x.FromLocation)
                    .WithMany()
                    .HasForeignKey(x => x.FromLocationId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.ToLocation)
                    .WithMany()
                    .HasForeignKey(x => x.ToLocationId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Device)
                    .WithMany()
                    .HasForeignKey(x => x.DeviceId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // R&R configuration
            b.Entity<RnrBatch>(e =>
            {
                e.HasKey(x => x.BatchId);
                e.HasIndex(x => x.BatchNumber).IsUnique();
                e.HasIndex(x => x.Status);
                e.HasIndex(x => x.CreatedAt);
                e.Property(x => x.Status).HasConversion<int>();
                e.Property(x => x.CreatedAt).HasColumnType("datetimeoffset(7)").HasDefaultValueSql("SYSUTCDATETIME()");
                e.Property(x => x.ConfirmedAt).HasColumnType("datetimeoffset(7)");
                e.ToTable("RnrBatches");
            });

            b.Entity<RnrBatchItem>(e =>
            {
                e.HasKey(x => x.ItemId);
                e.HasIndex(x => x.BatchId);
                e.HasOne<RnrBatch>()
                    .WithMany(x => x.Items)
                    .HasForeignKey(x => x.BatchId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.ToTable("RnrBatchItems");
            });
        }
    }

    public class School
    {
        public long SchoolId { get; set; }
        public string EmisCode { get; set; } = "";
        public string Name { get; set; } = "";
        public string? District { get; set; }
            public string? Cmc { get; set; }
            public string? Circuit { get; set; }
            public string? NatEmis { get; set; }
        public string? Address { get; set; }
    }

    public class Device
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string? SerialNumber { get; set; }
        public string? IMEI { get; set; }

        public string? Brand { get; set; }
        public string? Model { get; set; }
        public string? DeviceType { get; set; } // Laptop, Desktop, Tablet, Chromebook, Other
        public string? Description { get; set; }
        public string? OrderNumber { get; set; } // Links to order from Phase 0

        public string Source { get; set; } = "RNR"; // RNR | NEW

        public long? SchoolId { get; set; }
        public string? SchoolName { get; set; }
            public DeviceCategory Category { get; set; } = DeviceCategory.Unknown;

        public DateTimeOffset ImportedAt { get; set; } = DateTimeOffset.UtcNow;

        public Guid? BatchId { get; set; }
    }

    public class DeviceImportBatch
    {
        public Guid BatchId { get; set; } = Guid.NewGuid();
        public string Source { get; set; } = "RNR"; // RNR | NEW
        public long? SchoolId { get; set; }
        public string? FileName { get; set; }
        public string? OrderNumber { get; set; } // Links to order from Phase 0
        public int Total { get; set; }
        public int Added { get; set; }
        public int Duplicates { get; set; }
        public int Invalid { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public class DispatchTrip
    {
        public Guid TripId { get; set; } = Guid.NewGuid();
        public string TripRef { get; set; } = string.Empty;
        public string DriverName { get; set; } = string.Empty;
        public string VehicleReg { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? ScheduledAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public string Status { get; set; } = "Scheduled";
    }

    public class Document
    {
        public long DocumentId { get; set; }
        public Guid? BatchId { get; set; }
        public long? SchoolId { get; set; }

        public string DocType { get; set; } = ""; // e.g., RNR_HANDOVER, PO, DELIVERY_NOTE
        public string FileName { get; set; } = "";
        public string ContentType { get; set; } = "application/octet-stream";
        public byte[] FileData { get; set; } = Array.Empty<byte>();
        public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    // R&R workflow models
    public enum RnrBatchStatus
    {
        PendingScan = 0,
        ScanningInProgress = 1,
        Verified = 2,
        VarianceDetected = 3,
        GRVIssued = 4,
        Completed = 5,
        Cancelled = 6
    }

    public class RnrBatch
    {
        public Guid BatchId { get; set; } = Guid.NewGuid();
        public string BatchNumber { get; set; } = string.Empty;
        public string CollectionSlipNumber { get; set; } = string.Empty;
        public long? SchoolId { get; set; }
        public string? SchoolName { get; set; }
        public int TotalQuantityExpected { get; set; }
        public int TotalQuantityScanned { get; set; }
        public RnrBatchStatus Status { get; set; } = RnrBatchStatus.PendingScan;
        public string CreatedBy { get; set; } = "system";
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public string? ConfirmedBy { get; set; }
        public DateTimeOffset? ConfirmedAt { get; set; }
        public string? GRVNumber { get; set; }

        public List<RnrBatchItem> Items { get; set; } = new();
    }

    public class RnrBatchItem
    {
        public Guid ItemId { get; set; } = Guid.NewGuid();
        public Guid BatchId { get; set; }
        public string? Brand { get; set; }
        public string? Model { get; set; }
        public string? DeviceType { get; set; }
        public string? Description { get; set; }
        public int QuantityExpected { get; set; }
        public int QuantityScanned { get; set; }
    }

    public class AuditLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string UserId { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public string Action { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string? EntityId { get; set; }
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public string? MetaJson { get; set; }
    }
}