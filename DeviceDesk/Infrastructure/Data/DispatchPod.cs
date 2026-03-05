using System.ComponentModel.DataAnnotations;

namespace DeviceDesk.Infrastructure.Data
{
    public enum DispatchPodStatus
    {
        Draft = 0,
        Ready = 1,
        Signed = 2
    }

    public class DispatchPod
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [MaxLength(64)]
        public string PodNumber { get; set; } = string.Empty; // e.g., POD-2025-003

        [MaxLength(64)]
        public string? DeliveryNoteNumber { get; set; }

        [MaxLength(256)]
        public string SchoolName { get; set; } = string.Empty;

        [MaxLength(128)]
        public string? District { get; set; }

        [MaxLength(32)]
        public string StockType { get; set; } = "New"; // New | R&R

        [MaxLength(128)]
        public string? SourceReference { get; set; } // Order # or Collection #

        // Collection Slip Integration (Phase 3)
        public Guid? RnrBatchId { get; set; } // FK → RnrBatch
        [MaxLength(64)]
        public string? CollectionSlipNumber { get; set; }
        public bool IsLockedToCollectionSlip { get; set; } = false;
        public bool CollectionSlipValidated { get; set; } = false;
        public DateTimeOffset? CollectionSlipValidatedAt { get; set; }
        [MaxLength(128)]
        public string? CollectionSlipValidatedBy { get; set; }
        [MaxLength(32)]
        public string? EmisCode { get; set; }
        public int? TotalDevicesExpected { get; set; }
        public int? TotalDevicesScanned { get; set; }
        [MaxLength(128)]
        public string? CollectedBy { get; set; }
        [MaxLength(512)]
        public string? CollectionNotes { get; set; }

        public DispatchPodStatus Status { get; set; } = DispatchPodStatus.Ready;

        public Guid? TripId { get; set; } // FK → DispatchTrips (future)

        // System documents
        public long? PodDocumentId { get; set; }
        public long? DeliveryNoteDocumentId { get; set; }

        // Signed POD upload metadata
        public long? SignedPodDocumentId { get; set; } // FK → Documents
        public DateTimeOffset? SignedPodUploadedAt { get; set; }
        [MaxLength(128)]
        public string? SignedPodUploadedByUserId { get; set; }

        // Audit
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        [MaxLength(128)]
        public string? CreatedByUserId { get; set; }
    }
}