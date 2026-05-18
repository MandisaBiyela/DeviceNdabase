namespace DeviceDesk.Modules.Phase2.Models
{
    public class DetailedInspectionDto
    {
        public int Id { get; set; }
        public string Serial { get; set; } = string.Empty;
        public Phase2Zone Zone { get; set; }
        public Phase2Stage Stage { get; set; }

        // Pre-assessment context
        public AttentionRequired AttentionRequired { get; set; }
        public bool? PreAssessmentPassed { get; set; }
        public string? PreAssessmentNotes { get; set; }
        public string? PreAssessmentInspectorId { get; set; }

        // Detailed inspection fields
        public bool? UnderWarranty { get; set; }
        public bool? Repairable { get; set; }
        public string? TechnicianId { get; set; }
        public DateTimeOffset? InspectionDate { get; set; }
        public string? RepairCategory { get; set; }
        public bool? DisposalRequested { get; set; }

        // Quality context
        public bool? QaPassed { get; set; }
        public string? QaInspectorId { get; set; }
        public int ReworkCount { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }
}