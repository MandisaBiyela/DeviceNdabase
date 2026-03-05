namespace DeviceDesk.Modules.Phase2.Models;

public static class AuditActionGroups
{
    // Inspector-related actions for Phase 2
    public static readonly string[] InspectorActions =
    {
        AuditActions.PreAssessment,
        AuditActions.QualityAssessment
    };

    // Technician-related actions for Phase 2
    public static readonly string[] TechnicianActions =
    {
        AuditActions.DetailedInspection,
        AuditActions.DisposalRequested
    };
}