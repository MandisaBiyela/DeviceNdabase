namespace DeviceDesk.Modules.Phase3.Models;

/// <summary>
/// Represents a dispatch batch in the new 8-stage batch workflow.
/// A batch is created in Draft status, devices are added, then locked for audit.
/// </summary>
public class DispatchBatch
{
    public Guid BatchId { get; set; } = Guid.NewGuid();
    
    // Batch Status
    public BatchStatus Status { get; set; } = BatchStatus.Draft;
    
    // School Details
    public string SchoolName { get; set; } = string.Empty;
    public string? District { get; set; }
    public string? EmisCode { get; set; }
    
    // Source Details
    public string StockType { get; set; } = string.Empty; // RNR, NEW, etc.
    public string? SourceReference { get; set; }
    
    // Generated Documents
    public string? PODNumber { get; set; } // Auto-generated when locked
    public string? DeliveryNoteNumber { get; set; } // Auto-generated when locked
    
    // Trip Details
    public string? TripReference { get; set; }
    public string? DriverName { get; set; }
    public string? DriverUserId { get; set; }
    public string? VehicleReg { get; set; }
    
    // Document References
    public long? DeliveryNoteDocumentId { get; set; }
    public long? PODDocumentId { get; set; }
    
    // Loading Audit
    public bool AuditPassed { get; set; } = false;
    public DateTimeOffset? AuditCompletedAt { get; set; }
    public string? AuditCompletedByUserId { get; set; }
    
    // Transport Status
    public bool EnRoute { get; set; } = false;
    public DateTimeOffset? EnRouteAt { get; set; }
    public string? EnRouteByUserId { get; set; }
    
    // Arrival (Stage 7b: Mark as Delivered/Arrived)
    public DateTimeOffset? ArrivedAt { get; set; }
    public string? ArrivedByUserId { get; set; }
    
    // Delivery & Debrief
    public bool SchoolSigned { get; set; } = false;
    public DateTimeOffset? SchoolSignedAt { get; set; }
    public string? SchoolSignatoryName { get; set; }
    public long? SignedPODDocumentId { get; set; }
    
    public bool DebriefCompleted { get; set; } = false;
    public DateTimeOffset? DebriefCompletedAt { get; set; }
    public string? DebriefCompletedByUserId { get; set; }
    public string? DebriefNotes { get; set; }
    
    // Exception Handling
    public bool HasExceptions { get; set; } = false;
    public string? ExceptionNotes { get; set; }
    public string? ExceptionPhotos { get; set; } // JSON array of document IDs
    
    // Timestamps
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? CreatedByUserId { get; set; }
    public DateTimeOffset? LockedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    
    // Navigation
    public ICollection<BatchDevice> Devices { get; set; } = new List<BatchDevice>();
    public ICollection<LoadingAuditScan> AuditScans { get; set; } = new List<LoadingAuditScan>();
}

/// <summary>
/// Batch lifecycle status
/// </summary>
public enum BatchStatus
{
    Draft = 0,              // Created, devices being added, details being filled
    Locked = 1,             // Locked for loading audit, no more changes allowed
    AuditInProgress = 2,    // Loading audit scan in progress
    AuditPassed = 3,        // Audit passed, ready to mark en route
    AuditFailed = 4,        // Audit failed (mismatches detected), needs resolution
    EnRoute = 5,            // Trip marked en route to school
    Delivered = 6,          // Delivered at school, awaiting debrief
    Completed = 7,          // Debrief completed, batch closed
    Cancelled = 99          // Batch cancelled
}
