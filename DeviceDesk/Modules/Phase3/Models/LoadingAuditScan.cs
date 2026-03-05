namespace DeviceDesk.Modules.Phase3.Models;

/// <summary>
/// Records loading audit scan sessions for a dispatch batch.
/// Tracks individual scan attempts, mismatches, and audit outcomes.
/// </summary>
public class LoadingAuditScan
{
    public Guid AuditId { get; set; } = Guid.NewGuid();
    
    // Foreign Key
    public Guid BatchId { get; set; }
    
    // Scan Details
    public string ScannedSerials { get; set; } = string.Empty; // JSON array of serials scanned
    public int ExpectedCount { get; set; }
    public int ScannedCount { get; set; }
    
    // Mismatch Detection
    public bool AuditPassed { get; set; }
    public string? MismatchDetails { get; set; } // JSON: {missing: [], extra: [], duplicate: []}
    public string? ResolutionNotes { get; set; }
    
    // Timestamps
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public string? AuditedByUserId { get; set; }
    
    // Navigation
    public DispatchBatch Batch { get; set; } = null!;
}
