using System.ComponentModel.DataAnnotations;
using DeviceDesk.Modules.Phase3.Models;

namespace DeviceDesk.Modules.Phase2.Models;

public class Phase2Device
{
    public int Id { get; set; }

    [MaxLength(100)]
    public string Serial { get; set; } = string.Empty;

    public Phase2Zone Zone { get; set; }

    public Phase2Stage Stage { get; set; } = Phase2Stage.Received;

    // Step 1: Receiving & Pre-Assessment
    [MaxLength(128)]
    public string? IctClerkId { get; set; }
    public DateTimeOffset? ReceivingDate { get; set; }
    public bool? VerificationStatus { get; set; }
    public bool? PreAssessmentPassed { get; set; }
    [MaxLength(128)]
    public string? PreAssessmentInspectorId { get; set; }

    // Attention required and notes captured during Pre-Assessment
    public AttentionRequired AttentionRequired { get; set; } = AttentionRequired.None;

    public string? PreAssessmentNotes { get; set; }

    // Step 2: Detailed Inspection
    public bool? UnderWarranty { get; set; }
    public bool? Repairable { get; set; }
    [MaxLength(128)]
    public string? TechnicianId { get; set; }
    public DateTimeOffset? InspectionDate { get; set; }
    [MaxLength(256)]
    public string? RepairCategory { get; set; }
    public bool? DisposalRequested { get; set; }

    // Quarantine tracking
    public bool IsQuarantined { get; set; }
    public string? QuarantineReason { get; set; }
    public DateTimeOffset? QuarantinedAtUtc { get; set; }

    // Step 3: Quality Assessment
    public bool? QaPassed { get; set; }
    [MaxLength(128)]
    public string? QaInspectorId { get; set; }
    public int ReworkCount { get; set; } = 0;

    public int? ReceiptId { get; set; }
    public Receipt? Receipt { get; set; }

    // School information (linked from Phase 1 receiving)
    public int? SchoolId { get; set; }
    public string? SchoolName { get; set; }

    // Step 4: Handover to Dispatch (Scan-Out)
    public DateTimeOffset? ScannedOutAt { get; set; }

    [MaxLength(128)]
    public string? ScannedOutByUserId { get; set; }

    // Phase 3: Dispatch Status
    public DispatchDeviceState? DispatchStatus { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
