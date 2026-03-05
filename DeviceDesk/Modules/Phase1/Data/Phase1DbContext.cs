using DeviceDesk.Modules.Phase1.Models;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Infrastructure.Data
{
    public class Phase1DbContext : DbContext
    {
        public Phase1DbContext(DbContextOptions<Phase1DbContext> options) : base(options) { }

        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderLine> OrderLines => Set<OrderLine>();
        public DbSet<CollectionSlip> CollectionSlips => Set<CollectionSlip>();
        public DbSet<ReceivingBatch> ReceivingBatches => Set<ReceivingBatch>();
        public DbSet<ReceivingBatchItem> ReceivingBatchItems => Set<ReceivingBatchItem>();
        public DbSet<GoodsReceivedNote> GoodsReceivedNotes => Set<GoodsReceivedNote>();
        public DbSet<RnrExpectedItem> RnrExpectedItems => Set<RnrExpectedItem>();
        public DbSet<ReceivingBatchScan> ReceivingBatchScans => Set<ReceivingBatchScan>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            // Orders
            b.Entity<Order>(e =>
            {
                e.HasKey(x => x.OrderId);
                e.HasIndex(x => x.OrderNumber).IsUnique();
                e.Property(x => x.Status).HasConversion<int>();
                e.Property(x => x.CreatedAt).HasColumnType("datetimeoffset(7)").HasDefaultValueSql("SYSUTCDATETIME()");
                e.Property(x => x.UpdatedAt).HasColumnType("datetimeoffset(7)").HasDefaultValueSql("SYSUTCDATETIME()");
                e.HasMany(x => x.Lines).WithOne(x => x.Order).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
                e.ToTable("Orders");
            });

            b.Entity<OrderLine>(e =>
            {
                e.HasKey(x => x.OrderLineId);
                e.HasIndex(x => x.OrderId);
                e.ToTable("OrderLines");
            });

            // Collection Slips
            b.Entity<CollectionSlip>(e =>
            {
                e.HasKey(x => x.CollectionSlipId);
                e.HasIndex(x => x.SlipNumber).IsUnique();
                e.HasIndex(x => x.SchoolId);
                e.Property(x => x.SourceType).HasConversion<int>();
                e.Property(x => x.CreatedAt).HasColumnType("datetimeoffset(7)").HasDefaultValueSql("SYSUTCDATETIME()");
                e.ToTable("CollectionSlips");
            });

            // Receiving Batches
            b.Entity<ReceivingBatch>(e =>
            {
                e.HasKey(x => x.ReceivingBatchId);
                e.HasIndex(x => x.OrderId);
                e.HasIndex(x => x.NewStockBatchId);
                e.HasIndex(x => x.CollectionSlipId);
                e.HasIndex(x => x.SchoolId);
                e.Property(x => x.SourceType).HasConversion<int>();
                e.Property(x => x.Status).HasConversion<int>();
                e.Property(x => x.CreatedAt).HasColumnType("datetimeoffset(7)").HasDefaultValueSql("SYSUTCDATETIME()");
                e.Property(x => x.UpdatedAt).HasColumnType("datetimeoffset(7)").HasDefaultValueSql("SYSUTCDATETIME()");
                e.HasOne(x => x.Order).WithMany(x => x.ReceivingBatches).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.CollectionSlip).WithMany(x => x.ReceivingBatches).HasForeignKey(x => x.CollectionSlipId).OnDelete(DeleteBehavior.Restrict);
                e.HasMany(x => x.Items).WithOne(x => x.ReceivingBatch).HasForeignKey(x => x.ReceivingBatchId).OnDelete(DeleteBehavior.Cascade);
                e.ToTable("ReceivingBatches");
            });

            // Receiving Batch Items
            b.Entity<ReceivingBatchItem>(e =>
            {
                e.HasKey(x => x.ReceivingBatchItemId);
                e.HasIndex(x => x.ReceivingBatchId);
                e.HasIndex(x => x.SerialNumber).HasFilter("[SerialNumber] IS NOT NULL");
                e.HasIndex(x => x.IMEI).HasFilter("[IMEI] IS NOT NULL");
                e.Property(x => x.CreatedAt).HasColumnType("datetimeoffset(7)").HasDefaultValueSql("SYSUTCDATETIME()");
                e.ToTable("ReceivingBatchItems");
            });

            // Goods Received Notes
            b.Entity<GoodsReceivedNote>(e =>
            {
                e.HasKey(x => x.GRVId);
                e.HasIndex(x => x.GRVNumber).IsUnique();
                e.HasIndex(x => x.ReceivingBatchId).IsUnique();
                e.Property(x => x.CreatedAt).HasColumnType("datetimeoffset(7)").HasDefaultValueSql("SYSUTCDATETIME()");
                e.HasOne(x => x.ReceivingBatch).WithOne(x => x.GRV).HasForeignKey<GoodsReceivedNote>(x => x.ReceivingBatchId);
                e.ToTable("GoodsReceivedNotes");
            });

            // RnR Expected Items
            b.Entity<RnrExpectedItem>(e =>
            {
                e.HasKey(x => x.RnrExpectedItemId);
                e.HasIndex(x => new { x.BatchId, x.Serial }).IsUnique();
                e.ToTable("RnrExpectedItems");
            });

            // Receiving Batch Scans
            b.Entity<ReceivingBatchScan>(e =>
            {
                e.HasKey(x => x.ReceivingBatchScanId);
                e.HasIndex(x => new { x.BatchId, x.Serial }).IsUnique();
                e.Property(x => x.Status).HasConversion<int>();
                e.Property(x => x.ScannedAt).HasColumnType("datetimeoffset(7)").HasDefaultValueSql("SYSUTCDATETIME()");
                e.ToTable("ReceivingBatchScans");
            });
        }
    }
}
