namespace DeviceDesk.Modules.Phase3.Models;

/// <summary>
/// Device state machine for the batch dispatch workflow.
/// Each device must follow this strict state progression.
/// </summary>
public enum DispatchDeviceState
{
    /// <summary>
    /// Device has passed Phase 2 QA but not yet scanned out to dispatch
    /// </summary>
    QA_Completed = 0,
    
    /// <summary>
    /// Device scanned out from Phase 2, awaiting batch assignment
    /// </summary>
    Dispatch_Queue = 1,
    
    /// <summary>
    /// Device assigned to a draft batch (not yet locked)
    /// </summary>
    Batch_Assigned = 2,
    
    /// <summary>
    /// Batch locked, device ready for loading audit scan
    /// </summary>
    Ready_For_Audit = 3,
    
    /// <summary>
    /// Device scanned and verified in loading audit, ready for transport
    /// </summary>
    Transport_Ready = 4,
    
    /// <summary>
    /// Trip marked en route, device in transit to school
    /// </summary>
    En_Route = 5,
    
    /// <summary>
    /// Device delivered to school, debrief completed
    /// </summary>
    Delivered = 6,
    
    /// <summary>
    /// Device removed from batch (returned to dispatch queue)
    /// </summary>
    Removed_From_Batch = 10,
    
    /// <summary>
    /// Audit failed for this device (mismatch detected)
    /// </summary>
    Audit_Failed = 11
}
