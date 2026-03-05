namespace DeviceDesk.Modules.Phase3.Models;

/// <summary>
/// Junction table linking devices to dispatch batches.
/// Tracks which devices are assigned to which batch and their audit status.
/// </summary>
public class BatchDevice
{
    public Guid BatchDeviceId { get; set; } = Guid.NewGuid();
    
    // Foreign Keys
    public Guid BatchId { get; set; }
    public int DeviceId { get; set; } // References Phase2Devices.Id
    
    // Device Information (denormalized for quick access)
    public string Serial { get; set; } = string.Empty;
    public string? Model { get; set; }
    public string? Condition { get; set; }
    
    // Audit Status
    public bool ScannedInAudit { get; set; }
    public DateTimeOffset? ScannedAt { get; set; }
    public string? ScannedByUserId { get; set; }
    
    // Timestamps
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? AddedByUserId { get; set; }
    
    // Navigation
    public DispatchBatch Batch { get; set; } = null!;
}
