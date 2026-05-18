using DeviceDesk.Modules.Phase2.Data;
using DeviceDesk.Modules.Phase2.Models;
using Phase2AuditActions = DeviceDesk.Modules.Phase2.Models.AuditActions;

namespace DeviceDesk.Modules.Phase2.Services;

public class AssessmentService
{
    private readonly Phase2DbContext _db;
    private readonly AuditService _audit;
    public AssessmentService(Phase2DbContext db, AuditService audit)
    { 
        _db = db; 
        _audit = audit;
    }

    // Step 1.5: Pre-Assessment by Inspector
    public async Task PreAssessmentAsync(int deviceId, bool passed, AttentionRequired attentionRequired, string inspectorId, string? notes)
    {
        var device = await _db.Devices.FindAsync(deviceId) ?? throw new InvalidOperationException("Device not found");
        
        // Store pre-assessment outcome
        device.PreAssessmentPassed = passed;
        device.PreAssessmentInspectorId = inspectorId;
        device.PreAssessmentNotes = notes;
        device.AttentionRequired = attentionRequired;

        // Always move to Detailed Inspection queue after pre-assessment.
        // Technicians will use flags (AttentionRequired, PreAssessmentPassed) to triage.
        device.Stage = Phase2Stage.DetailedInspection;

        device.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        
        await _audit.LogAsync(inspectorId, Phase2AuditActions.PreAssessment, deviceId, device.Serial, $"Passed: {passed}; Attention: {attentionRequired}");
    }

    // Step 2: Detailed Inspection by Technician
    public async Task DetailedInspectionAsync(
        int deviceId,
        string technicianId,
        bool underWarranty,
        bool? repairable,
        InspectionCategory category,
        string? notes,
        string? documentRef = null,
        Phase2Stage? destination = null,
        // New parameters for structured data
        string? symptoms = null,
        string? findings = null,
        List<string>? faultChecklist = null,
        List<(string PartName, string? PartCode, int Quantity, string? Notes)>? parts = null,
        string? recommendedAction = null,
        string? priority = null,
        decimal? estimatedLabourHours = null)
    {
        var device = await _db.Devices.FindAsync(deviceId) ?? throw new InvalidOperationException("Device not found");

        // Guard: do not allow detailed inspection on disposed devices
        if (device.Stage == Phase2Stage.Disposal)
        {
            throw new InvalidOperationException("Device has already been disposed.");
        }

        // Guard: require pre-assessment to be completed (true or false); block only if not done
        if (device.PreAssessmentPassed == null)
        {
            throw new InvalidOperationException("Pre-assessment must be completed before detailed inspection.");
        }
        device.UnderWarranty = underWarranty;
        device.Repairable = repairable;

        device.TechnicianId = technicianId;
        device.InspectionDate = DateTimeOffset.UtcNow;
        device.RepairCategory = category.ToString();
        
        _db.Assessments.Add(new AssessmentRecord
        {
            DeviceId = deviceId,
            IsPreAssessment = false,
            Category = category,
            Notes = notes,
            PerformedBy = technicianId,
            DocumentRef = documentRef
        });

        // Create structured repair request for hardware/quarantine/warranty categories
        if (category == InspectionCategory.HardwareFailure ||
            category == InspectionCategory.Quarantine ||
            category == InspectionCategory.WarrantyReturn)
        {
            var repairRequest = new Phase2RepairRequest
            {
                DeviceId = deviceId,
                DeviceSerial = device.Serial,
                IsUnderWarranty = underWarranty,
                WarrantyRoute = category == InspectionCategory.WarrantyReturn ? "ReturnToManufacturer" : "InternalRepair",
                Category = category,
                SymptomDescription = symptoms,
                TechnicianFindings = findings,
                HardwareChecklistSummary = faultChecklist != null ? string.Join(", ", faultChecklist) : null,
                RecommendedAction = recommendedAction,
                Priority = priority ?? "Normal",
                EstimatedLabourHours = estimatedLabourHours,
                Status = RepairStatus.PendingAuthorization,
                CreatedByUserId = technicianId,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            
            if (parts != null && parts.Any())
            {
                foreach (var p in parts)
                {
                    repairRequest.Parts.Add(new Phase2RepairPart
                    {
                        PartName = p.PartName,
                        PartNumber = p.PartCode,
                        Quantity = p.Quantity
                    });
                }
            }
            
            _db.RepairRequests.Add(repairRequest);
            
            // Set quarantine flags
            device.IsQuarantined = true;
            device.QuarantineReason = recommendedAction ?? symptoms ?? "Requires repair";
            device.QuarantinedAtUtc = DateTimeOffset.UtcNow;
        }

        if (underWarranty && category == InspectionCategory.WarrantyReturn)
        {
            device.Stage = Phase2Stage.WarrantyReturn;
        }
        else if (!underWarranty && category == InspectionCategory.HardwareFailure)
        {
            device.Stage = Phase2Stage.HardwareDept;
        }
        else if (!underWarranty && category == InspectionCategory.SoftwareIssueUpgrade)
        {
            device.Stage = Phase2Stage.SoftwareDept;
        }
        else if (category == InspectionCategory.NoIssuesFound)
        {
            // Default to Dispatch unless explicitly set to SchoolAllocation
            device.Stage = destination == Phase2Stage.SchoolAllocation ? Phase2Stage.SchoolAllocation : Phase2Stage.Dispatch;
        }
        else if (category == InspectionCategory.Quarantine)
        {
            device.Stage = Phase2Stage.Quarantine;
        }
        else if (category == InspectionCategory.Disposal)
        {
            // Flag disposal; Stage will be set to Disposal only on manager approval
            device.DisposalRequested = true;
        }

        device.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        
        await _audit.LogAsync(technicianId, Phase2AuditActions.DetailedInspection, deviceId, device.Serial, $"Category: {category}, Warranty: {underWarranty}");
}
}
