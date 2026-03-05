using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DeviceDesk.Modules.Phase0.Models
{
    /// <summary>
    /// Represents a scanned serial number linked to a specific model in model-driven scanning
    /// </summary>
    public class ScannedSerial
    {
        [Key]
        public Guid SerialID { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Foreign key to NewStockBatch
        /// </summary>
        [Required]
        public Guid OrderID { get; set; }

        /// <summary>
        /// Foreign key to OrderModelList
        /// </summary>
        [Required]
        public Guid ModelID { get; set; }

        /// <summary>
        /// The actual scanned device serial number
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string DeviceSerial { get; set; } = string.Empty;

        /// <summary>
        /// When this serial was scanned
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Navigation property to the order (batch)
        /// </summary>
        [ForeignKey(nameof(OrderID))]
        public virtual NewStockBatch? Order { get; set; }

        /// <summary>
        /// Navigation property to the model
        /// </summary>
        [ForeignKey(nameof(ModelID))]
        public virtual OrderModelList? Model { get; set; }
    }
}
