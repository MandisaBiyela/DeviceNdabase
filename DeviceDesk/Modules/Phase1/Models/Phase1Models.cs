using System.ComponentModel.DataAnnotations;

namespace DeviceDesk.Modules.Phase1.Models
{
    // ===== Enums =====
    public enum ReceivingSourceType
    {
        NewStock = 1,      // Linked to Order
        RnrNormal = 2,     // School Collection Slip
        RnrEmergency = 3   // School Collection Slip
    }

    public enum ReceivingBatchStatus
    {
        Draft = 0,
        ScanningInProgress = 1,
        PendingVerification = 2,
        VarianceDetected = 3,
        Verified = 4,
        GRVIssued = 5,
        Completed = 6,
        Cancelled = 99
    }

    public enum VarianceResolution
    {
        None = 0,
        Recount = 1,
        SupervisorApproval = 2,
        SupplierError = 3,
        NCRIssued = 4
    }

    public enum OrderStatus
    {
        Draft = 0,
        Submitted = 1,
        Approved = 2,
        PartiallyReceived = 3,
        FullyReceived = 4,
        Cancelled = 5
    }

    // ===== Core Models =====
    
    /// <summary>
    /// Purchase Order / Invoice for New Stock
    /// </summary>
    public class Order
    {
        public Guid OrderId { get; set; } = Guid.NewGuid();
        
        [MaxLength(64)]
        public string OrderNumber { get; set; } = "";
        
        [MaxLength(64)]
        public string? InvoiceNumber { get; set; }
        
        [MaxLength(256)]
        public string? SupplierName { get; set; }
        
        public DateTimeOffset OrderDate { get; set; }
        public DateTimeOffset? DeliveryDate { get; set; }
        
        public OrderStatus Status { get; set; } = OrderStatus.Draft;
        
        [MaxLength(512)]
        public string? Notes { get; set; }
        
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        
        // Navigation
        public List<OrderLine> Lines { get; set; } = new();
        public List<ReceivingBatch> ReceivingBatches { get; set; } = new();
    }

    /// <summary>
    /// Expected items in an Order
    /// </summary>
    public class OrderLine
    {
        public Guid OrderLineId { get; set; } = Guid.NewGuid();
        public Guid OrderId { get; set; }
        
        [MaxLength(128)]
        public string? Brand { get; set; }
        
        [MaxLength(128)]
        public string? Model { get; set; }
        
        public int QuantityOrdered { get; set; }
        public int QuantityReceived { get; set; }
        
        [MaxLength(512)]
        public string? Description { get; set; }
        
        // Navigation
        public Order Order { get; set; } = null!;
    }

    /// <summary>
    /// School Collection Slip for RnR
    /// </summary>
    public class CollectionSlip
    {
        public Guid CollectionSlipId { get; set; } = Guid.NewGuid();
        
        [MaxLength(64)]
        public string SlipNumber { get; set; } = "";
        
        public long SchoolId { get; set; }
        
        [MaxLength(32)]
        public string EmisCode { get; set; } = "";
        
        [MaxLength(256)]
        public string SchoolName { get; set; } = "";
        
        public ReceivingSourceType SourceType { get; set; } = ReceivingSourceType.RnrNormal;
        
        public DateTimeOffset CollectionDate { get; set; }
        
        [MaxLength(128)]
        public string? CollectedBy { get; set; }
        
        [MaxLength(512)]
        public string? Notes { get; set; }
        
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        
        // Navigation
        public List<ReceivingBatch> ReceivingBatches { get; set; } = new();
    }

    /// <summary>
    /// Main Receiving Batch - Phase 1
    /// </summary>
    public class ReceivingBatch
    {
        public Guid ReceivingBatchId { get; set; } = Guid.NewGuid();
        
        public ReceivingSourceType SourceType { get; set; }
        
        // For NewStock - link to Phase 0 NewStockBatch
        public Guid? NewStockBatchId { get; set; }
        
        // For NewStock (legacy) or RnR
        public Guid? OrderId { get; set; }
        
        // For RnR
        public Guid? CollectionSlipId { get; set; }
        public long? SchoolId { get; set; }
        
        public ReceivingBatchStatus Status { get; set; } = ReceivingBatchStatus.Draft;
        
        [MaxLength(128)]
        public string? ReceivedBy { get; set; }
        
        [MaxLength(128)]
        public string? ScanningOfficer { get; set; }
        
        [MaxLength(128)]
        public string? VerifiedBy { get; set; }
        
        public DateTimeOffset? ReceivedDate { get; set; }
        public DateTimeOffset? ScanningStartedAt { get; set; }
        public DateTimeOffset? ScanningCompletedAt { get; set; }
        public DateTimeOffset? VerifiedAt { get; set; }
        
        // Reconciliation
        public int ExpectedCount { get; set; }
        public int ActualCount { get; set; }
        public int VarianceCount { get; set; }
        public bool HasVariance { get; set; }
        
        [MaxLength(1024)]
        public string? VarianceReason { get; set; }
        
        public VarianceResolution? VarianceResolution { get; set; }
        
        [MaxLength(128)]
        public string? SupervisorApprovedBy { get; set; }
        
        public DateTimeOffset? SupervisorApprovedAt { get; set; }
        
        public bool IsLocked { get; set; }
        
        [MaxLength(1024)]
        public string? Notes { get; set; }
        
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        
        // Navigation
        public Order? Order { get; set; }
        public CollectionSlip? CollectionSlip { get; set; }
        public List<ReceivingBatchItem> Items { get; set; } = new();
        public GoodsReceivedNote? GRV { get; set; }
    }

    /// <summary>
    /// Goods Received Note (GRV)
    /// </summary>
    public class GoodsReceivedNote
    {
        public Guid GRVId { get; set; } = Guid.NewGuid();
        public Guid ReceivingBatchId { get; set; }
        
        [MaxLength(64)]
        public string GRVNumber { get; set; } = "";
        
        public DateTimeOffset GRVDate { get; set; } = DateTimeOffset.UtcNow;
        
        [MaxLength(128)]
        public string? SupplierName { get; set; }
        
        [MaxLength(64)]
        public string? OrderNumber { get; set; }
        
        [MaxLength(64)]
        public string? InvoiceNumber { get; set; }
        
        public int TotalQuantity { get; set; }
        
        [MaxLength(128)]
        public string? ReceivedBy { get; set; }
        
        [MaxLength(128)]
        public string? VerifiedBy { get; set; }
        
        [MaxLength(1024)]
        public string? Notes { get; set; }
        
        public byte[]? PdfData { get; set; }
        
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        
        // Navigation
        public ReceivingBatch ReceivingBatch { get; set; } = null!;
    }

    /// <summary>
    /// Individual device received in a batch
    /// </summary>
    public class ReceivingBatchItem
    {
        public Guid ReceivingBatchItemId { get; set; } = Guid.NewGuid();
        public Guid ReceivingBatchId { get; set; }
        
        [MaxLength(128)]
        public string? SerialNumber { get; set; }
        
        [MaxLength(64)]
        public string? IMEI { get; set; }
        
        [MaxLength(128)]
        public string? Brand { get; set; }
        
        [MaxLength(128)]
        public string? Model { get; set; }
        
        [MaxLength(512)]
        public string? Notes { get; set; }
        
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        
        // Navigation
        public ReceivingBatch ReceivingBatch { get; set; } = null!;
    }

    // ===== Scanning Models =====
    
    public enum ScanValidationResult
    {
        Valid = 0,
        DuplicateInBatch = 1,
        DuplicateInSystem = 2,
        ModelMismatch = 3,
        InvalidFormat = 4
    }

    // ===== DTOs =====
    
    public record CreateReceivingBatchRequest(
        ReceivingSourceType SourceType,
        Guid? OrderId,
        Guid? CollectionSlipId,
        long? SchoolId,
        string? ReceivedBy,
        string? Notes
    );

    public record ScanDeviceRequest(
        Guid ReceivingBatchId,
        string? SerialNumber,
        string? IMEI,
        string? Brand,
        string? Model,
        string? Notes
    );

    public record ScanValidationResponse(
        bool IsValid,
        ScanValidationResult ValidationResult,
        string Message,
        string? ConflictingBatchId,
        string? ExpectedBrand,
        string? ExpectedModel,
        int TotalScanned,
        int ExpectedCount
    );

    public record ScannedDeviceDto(
        Guid ReceivingBatchItemId,
        string? SerialNumber,
        string? IMEI,
        string? Brand,
        string? Model,
        string? Notes,
        DateTimeOffset ScannedAt
    );

    public record StartScanningRequest(
        Guid ReceivingBatchId,
        string ScanningOfficer
    );

    public record CompleteScanningRequest(
        Guid ReceivingBatchId
    );

    public record SubmitCountRequest(
        Guid ReceivingBatchId,
        string VerifiedBy
    );

    public record ResolveVarianceRequest(
        Guid ReceivingBatchId,
        VarianceResolution Resolution,
        string Reason,
        string? SupervisorName
    );

    public record ReconciliationStatusDto(
        Guid ReceivingBatchId,
        ReceivingBatchStatus Status,
        string StatusName,
        int ExpectedCount,
        int ActualCount,
        int VarianceCount,
        bool HasVariance,
        string? VarianceReason,
        bool IsLocked,
        bool CanSubmitCount,
        bool CanRecount,
        bool RequiresSupervisorApproval
    );

    public record GRVDto(
        Guid GRVId,
        string GRVNumber,
        DateTimeOffset GRVDate,
        string? SupplierName,
        string? OrderNumber,
        int TotalQuantity,
        string? ReceivedBy,
        string? VerifiedBy
    );

    public record ReceivingBatchDto(
        Guid ReceivingBatchId,
        ReceivingSourceType SourceType,
        string SourceTypeName,
        Guid? OrderId,
        string? OrderNumber,
        Guid? NewStockBatchId,
        Guid? CollectionSlipId,
        string? SlipNumber,
        string? SchoolName,
        ReceivingBatchStatus Status,
        string StatusName,
        string? ReceivedBy,
        DateTimeOffset? ReceivedDate,
        int ItemCount,
        DateTimeOffset CreatedAt
    );

    public record OrderDto(
        Guid OrderId,
        string OrderNumber,
        string? InvoiceNumber,
        string? SupplierName,
        DateTimeOffset OrderDate,
        OrderStatus Status,
        string StatusName,
        int TotalOrdered,
        int TotalReceived,
        int LinesCount
    );

    public record CollectionSlipDto(
        Guid CollectionSlipId,
        string SlipNumber,
        string EmisCode,
        string SchoolName,
        ReceivingSourceType SourceType,
        string SourceTypeName,
        DateTimeOffset CollectionDate,
        string? CollectedBy
    );

    // ===== RnR Models =====
    public class RnrExpectedItem
    {
        public Guid RnrExpectedItemId { get; set; } = Guid.NewGuid();
        public Guid BatchId { get; set; }
        public string Serial { get; set; } = "";
        public string? DeviceName { get; set; } // Device name (e.g., Dell, Acer, Asus, HP)
        public string? Model { get; set; }
        public string? Notes { get; set; }
        public long? SchoolId { get; set; } // School information at item level
    }

    public enum RnrScanStatus
    {
        Matched = 1,           // scanned & on slip
        Unexpected = 2,        // scanned but not on slip
        Duplicate = 3,         // scanned twice in this batch
        NotFoundInInventory = 4
    }

    public class ReceivingBatchScan
    {
        public Guid ReceivingBatchScanId { get; set; } = Guid.NewGuid();
        public Guid BatchId { get; set; }
        public string Serial { get; set; } = "";
        public RnrScanStatus Status { get; set; }
        public DateTimeOffset ScannedAt { get; set; } = DateTimeOffset.UtcNow;
        public string? DeviceInfo { get; set; }
        public string? SchoolMatch { get; set; } // optional future check
        public long? SchoolId { get; set; } // School information at scan level
    }
}
