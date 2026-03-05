namespace DeviceDesk.Modules.Phase3.Models;

public class DispatchTrip
{
    public Guid TripId { get; set; } = Guid.NewGuid();
    public string TripRef { get; set; } = string.Empty; // Trip sheet number
    public string DriverName { get; set; } = string.Empty;
    public string? DriverUserId { get; set; } // Link to Driver user account
    public string VehicleReg { get; set; } = string.Empty;
    public TripStatus Status { get; set; } = TripStatus.Draft;
    
    // Acceptance
    public bool DriverAccepted { get; set; }
    public DateTimeOffset? DriverAcceptedAt { get; set; }
    
    // Completion
    public bool Completed { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? CompletedByUserId { get; set; }
    
    // Debriefing
    public bool DebriefingPassed { get; set; }
    public DateTimeOffset? DebriefingCompletedAt { get; set; }
    public string? DebriefingByUserId { get; set; }
    public string? DebriefingNotes { get; set; }
    
    // Final Sign-Off
    public bool FinalSignOffPassed { get; set; }
    public DateTimeOffset? FinalSignOffAt { get; set; }
    public string? FinalSignOffByUserId { get; set; }
    public string? FinalSignOffNotes { get; set; }
    
    // Timestamps
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    
    // Navigation
    public ICollection<DispatchPOD> PODs { get; set; } = new List<DispatchPOD>();
}

public enum TripStatus
{
    Draft = 0,              // Created, not yet sent to driver
    PendingAcceptance = 1,  // Sent to driver, awaiting acceptance
    InTransit = 2,          // Driver accepted, en route
    Completed = 3,          // Driver completed delivery
    InDebriefing = 4,       // QA reviewing
    DebriefingFailed = 5,   // QA rejected, needs rework
    AwaitingSignOff = 6,    // Passed debriefing, awaiting manager approval
    Closed = 7,             // Manager approved, case closed
    Cancelled = 99          // Cancelled
}

public class DispatchPOD
{
    public Guid PODId { get; set; } = Guid.NewGuid();
    public string PODNumber { get; set; } = string.Empty;
    public string DeliveryNoteNumber { get; set; } = string.Empty;
    
    // School Details
    public string SchoolName { get; set; } = string.Empty;
    public string? District { get; set; }
    public string? EmisCode { get; set; }
    
    // Source Details
    public string StockType { get; set; } = string.Empty; // RNR, NEW, etc.
    public string? SourceReference { get; set; }
    
    // Trip Assignment
    public Guid? TripId { get; set; }
    public DispatchTrip? Trip { get; set; }
    
    // Device Status
    public PODStatus Status { get; set; } = PODStatus.ReadyForDispatch;
    
    // Documents
    public long? DeliveryNoteDocumentId { get; set; }
    public long? PODDocumentId { get; set; }
    
    // Signed POD Upload
    public long? SignedPODDocumentId { get; set; }
    public DateTimeOffset? SignedPODUploadedAt { get; set; }
    public string? SignedPODUploadedByUserId { get; set; }
    
    // Exception Handling
    public bool HasExceptions { get; set; }
    public string? ExceptionNotes { get; set; }
    public string? ExceptionPhotos { get; set; } // JSON array of document IDs
    
    // School Signature
    public bool SchoolSigned { get; set; }
    public DateTimeOffset? SchoolSignedAt { get; set; }
    public string? SchoolSignatoryName { get; set; }
    
    // Timestamps
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? CreatedByUserId { get; set; }
}

public enum PODStatus
{
    ReadyForDispatch = 0,   // Phase 2 QA passed, ready for dispatch
    InDispatch = 1,         // Scanned out to dispatch
    AssignedToTrip = 2,     // Assigned to a trip
    InTransit = 3,          // Trip in progress
    Delivered = 4,          // Delivered to school
    Exception = 5,          // Delivery exception
    InDebriefing = 6,       // Being reviewed by QA
    DebriefingFailed = 7,   // QA rejected
    AwaitingSignOff = 8,    // Awaiting manager approval
    Closed = 9              // Fully closed
}
