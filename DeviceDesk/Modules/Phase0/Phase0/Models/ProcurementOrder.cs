using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DeviceDesk.Modules.Phase0.Models
{
    public enum SchoolItemDeliveryStatus
    {
        Pending = 0,
        InProgress = 1,
        Delivered = 2,
        InTransit = 3,
        Partial = 4,
        Cancelled = 5
    }

    public enum FinancialBalanceStatus
    {
        Balanced = 0,
        Outstanding = 1
    }

    public enum AllocationBalanceStatus
    {
        Balanced = 0,
        Unbalanced = 1
    }

    public class ProcurementOrder
    {
        [Key]
        public Guid ProcurementOrderId { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(100)]
        public string PoNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string ProjectName { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string FinancialYear { get; set; } = string.Empty;

        /// <summary>DOE order value (full contract amount).</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalOrderValue { get; set; }

        /// <summary>Ndabase management fee percentage (e.g. 10 for 10%).</summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal ManagementFeePercentage { get; set; }

        /// <summary>Management fee amount retained by Ndabase.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal ManagementFeeAmount { get; set; }

        /// <summary>Budget available for school procurement (order value minus management fee).</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal SupplierFee { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalInvoicedToDepartment { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPaidByDepartment { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPaidToSuppliers { get; set; }

        [Column(TypeName = "datetimeoffset(7)")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Column(TypeName = "datetimeoffset(7)")]
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>Optional narrative for Close-Out report Section 6 (timeline).</summary>
        public string? TimelineNotes { get; set; }

        /// <summary>Optional narrative for Close-Out report Section 7 (scope changes).</summary>
        public string? ScopeNotes { get; set; }

        /// <summary>Supplier responsible for delivering the devices for this order.</summary>
        [MaxLength(200)]
        public string? SupplierName { get; set; }

        /// <summary>Expected delivery date for the order (Phase 1 uses this on the receiving batch).</summary>
        public DateTimeOffset? ExpectedDeliveryDate { get; set; }

        /// <summary>Back-link to the NewStockBatch generated for this order (Phase 1 receiving).</summary>
        public Guid? NewStockBatchId { get; set; }

        public virtual ICollection<ProcurementOrderSchool> Schools { get; set; } = new List<ProcurementOrderSchool>();
    }

    public class ProcurementOrderSchool
    {
        [Key]
        public Guid ProcurementOrderSchoolId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ProcurementOrderId { get; set; }

        [ForeignKey(nameof(ProcurementOrderId))]
        public ProcurementOrder ProcurementOrder { get; set; } = null!;

        [Required]
        [MaxLength(256)]
        public string SchoolName { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal SchoolSubTotal { get; set; }

        public virtual ICollection<ProcurementOrderItem> Items { get; set; } = new List<ProcurementOrderItem>();
    }

    public class ProcurementOrderItem
    {
        [Key]
        public Guid ProcurementOrderItemId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ProcurementOrderSchoolId { get; set; }

        [ForeignKey(nameof(ProcurementOrderSchoolId))]
        public ProcurementOrderSchool ProcurementOrderSchool { get; set; } = null!;

        [Required]
        [MaxLength(300)]
        public string Description { get; set; } = string.Empty;

        /// <summary>Optional device brand (e.g. "Asus") parsed from the order line.</summary>
        [MaxLength(100)]
        public string? Brand { get; set; }

        /// <summary>Optional device model (e.g. "Vivobook").</summary>
        [MaxLength(100)]
        public string? Model { get; set; }

        /// <summary>Optional device type (e.g. "Laptop", "Tablet").</summary>
        [MaxLength(50)]
        public string? DeviceType { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        public int QtyOrdered { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }

        public SchoolItemDeliveryStatus DeliveryStatus { get; set; } = SchoolItemDeliveryStatus.Pending;
    }
}
