using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DeviceDesk.Modules.Phase0.Models
{
    /// <summary>
    /// Represents a batch of new stock uploaded in Phase 0
    /// Contains only descriptions and quantities - no serial numbers yet
    /// Serial numbers are added during Phase 1 scanning
    /// </summary>
    public class NewStockBatch
    {
        [Key]
        public Guid BatchId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Unique batch number (e.g., NB-2025-001)
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string BatchNumber { get; set; } = string.Empty;

        /// <summary>
        /// Supplier or source of the stock
        /// </summary>
        [MaxLength(200)]
        public string? SupplierName { get; set; }

        /// <summary>
        /// Invoice or reference number
        /// </summary>
        [MaxLength(100)]
        public string? InvoiceNumber { get; set; }

        /// <summary>
        /// Expected delivery date
        /// </summary>
        public DateTime? ExpectedDeliveryDate { get; set; }

        /// <summary>
        /// Total quantity expected across all items
        /// </summary>
        public int TotalQuantityExpected { get; set; }

        /// <summary>
        /// Total quantity scanned in Phase 1
        /// </summary>
        public int TotalQuantityScanned { get; set; }

        /// <summary>
        /// Batch status
        /// </summary>
        public NewStockBatchStatus Status { get; set; } = NewStockBatchStatus.PendingScan;

        /// <summary>
        /// User who created the batch (Phase 0)
        /// </summary>
        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        /// <summary>
        /// User who confirmed the batch (Phase 1)
        /// </summary>
        [MaxLength(100)]
        public string? ConfirmedBy { get; set; }

        /// <summary>
        /// When the batch was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the batch was confirmed
        /// </summary>
        public DateTime? ConfirmedAt { get; set; }

        /// <summary>
        /// Notes or comments
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// GRV number generated after confirmation
        /// </summary>
        [MaxLength(50)]
        public string? GRVNumber { get; set; }

        /// <summary>
        /// Procurement order (Phase 0) that produced this batch, when the batch was
        /// auto-generated from a procurement order. Null for legacy/standalone batches.
        /// </summary>
        public Guid? ProcurementOrderId { get; set; }

        /// <summary>PO Number copied from the procurement order, surfaced in receiving UIs.</summary>
        [MaxLength(100)]
        public string? PoNumber { get; set; }

        /// <summary>Project name copied from the procurement order.</summary>
        [MaxLength(200)]
        public string? ProjectName { get; set; }

        /// <summary>Financial year copied from the procurement order.</summary>
        [MaxLength(20)]
        public string? FinancialYear { get; set; }

        /// <summary>
        /// Item lines in this batch (descriptions only)
        /// </summary>
        public virtual ICollection<NewStockBatchItem> Items { get; set; } = new List<NewStockBatchItem>();

        /// <summary>
        /// Scanned devices linked to this batch (added in Phase 1)
        /// </summary>
        public virtual ICollection<NewStockScannedDevice> ScannedDevices { get; set; } = new List<NewStockScannedDevice>();
    }

    /// <summary>
    /// Status of a new stock batch
    /// </summary>
    public enum NewStockBatchStatus
    {
        /// <summary>
        /// Batch created in Phase 0, waiting to be scanned in Phase 1
        /// </summary>
        PendingScan = 0,

        /// <summary>
        /// Scanning in progress in Phase 1
        /// </summary>
        Scanning = 1,

        /// <summary>
        /// Scanning complete, quantities match
        /// </summary>
        ReadyToConfirm = 2,

        /// <summary>
        /// Quantity mismatch (over/under)
        /// </summary>
        Mismatch = 3,

        /// <summary>
        /// Batch confirmed and GRV generated
        /// </summary>
        Completed = 4,

        /// <summary>
        /// Batch cancelled
        /// </summary>
        Cancelled = 5
    }

    /// <summary>
    /// Individual item line in a new stock batch
    /// Contains only descriptions - no serial numbers
    /// </summary>
    public class NewStockBatchItem
    {
        [Key]
        public Guid ItemId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Parent batch
        /// </summary>
        [Required]
        public Guid BatchId { get; set; }

        [ForeignKey(nameof(BatchId))]
        public virtual NewStockBatch Batch { get; set; } = null!;

        /// <summary>
        /// Device brand (e.g., HP, Dell, Lenovo)
        /// </summary>
        [MaxLength(100)]
        public string? Brand { get; set; }

        /// <summary>
        /// Device model (e.g., EliteBook 840)
        /// </summary>
        [MaxLength(100)]
        public string? Model { get; set; }

        /// <summary>
        /// Device type (e.g., Laptop, Tablet, Desktop)
        /// </summary>
        [MaxLength(50)]
        public string? DeviceType { get; set; }

        /// <summary>
        /// Description
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Quantity expected for this item line
        /// </summary>
        public int QuantityExpected { get; set; }

        /// <summary>
        /// Quantity scanned for this item line
        /// </summary>
        public int QuantityScanned { get; set; }

        /// <summary>
        /// Target zone (always New Stock for Phase 0)
        /// </summary>
        [MaxLength(50)]
        public string Zone { get; set; } = "New Stock";

        /// <summary>
        /// Unit price copied from the procurement order line. Stored on the item so
        /// the receiving side can show pricing without joining back to the order.
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        /// <summary>
        /// JSON array of per-school allocations for this (Brand/Model/DeviceType) line.
        /// Each element is { schoolName, qtyOrdered, deliveryStatus }.
        /// Used by the receiving and model-scanning UIs to show the school breakdown.
        /// </summary>
        public string? SchoolBreakdownJson { get; set; }
    }

    /// <summary>
    /// Scanned device linked to a new stock batch
    /// Created during Phase 1 scanning
    /// </summary>
    public class NewStockScannedDevice
    {
        [Key]
        public Guid ScanId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Parent batch
        /// </summary>
        [Required]
        public Guid BatchId { get; set; }

        [ForeignKey(nameof(BatchId))]
        public virtual NewStockBatch Batch { get; set; } = null!;

        /// <summary>
        /// Scanned serial number (unique)
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string SerialNumber { get; set; } = string.Empty;

        /// <summary>
        /// IMEI (optional)
        /// </summary>
        [MaxLength(50)]
        public string? IMEI { get; set; }

        /// <summary>
        /// Brand (can be auto-filled or manually entered)
        /// </summary>
        [MaxLength(100)]
        public string? Brand { get; set; }

        /// <summary>
        /// Model (can be auto-filled or manually entered)
        /// </summary>
        [MaxLength(100)]
        public string? Model { get; set; }

        /// <summary>
        /// When this device was scanned
        /// </summary>
        public DateTime ScannedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Who scanned this device
        /// </summary>
        [MaxLength(100)]
        public string ScannedBy { get; set; } = string.Empty;

        /// <summary>
        /// Is this a duplicate within the batch?
        /// </summary>
        public bool IsDuplicate { get; set; }

        /// <summary>
        /// Notes about this scan
        /// </summary>
        public string? Notes { get; set; }
    }
}
