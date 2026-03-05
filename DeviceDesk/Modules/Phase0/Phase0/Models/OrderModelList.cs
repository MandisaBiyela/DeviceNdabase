using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DeviceDesk.Modules.Phase0.Models
{
    /// <summary>
    /// Represents a model within an order for model-driven scanning workflow
    /// </summary>
    public class OrderModelList
    {
        [Key]
        public Guid ModelID { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Foreign key to NewStockBatch
        /// </summary>
        [Required]
        public Guid OrderID { get; set; }

        /// <summary>
        /// Model name (from Brand + Model + DeviceType)
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string ModelName { get; set; } = string.Empty;

        /// <summary>
        /// Expected quantity for this model
        /// </summary>
        public int ExpectedQty { get; set; }

        /// <summary>
        /// Counted quantity (incremented during scanning)
        /// </summary>
        public int CountedQty { get; set; } = 0;

        /// <summary>
        /// Status: "Open" or "Closed"
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Open";

        /// <summary>
        /// Navigation property to the order (batch)
        /// </summary>
        [ForeignKey(nameof(OrderID))]
        public virtual NewStockBatch? Order { get; set; }

        /// <summary>
        /// Scanned serials for this model
        /// </summary>
        public virtual ICollection<ScannedSerial> ScannedSerials { get; set; } = new List<ScannedSerial>();
    }
}
