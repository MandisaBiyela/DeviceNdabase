using System.ComponentModel.DataAnnotations;

namespace DeviceDesk.Modules.Phase2.Models;

public class AssessmentRecord
{
    public int Id { get; set; }

    public int DeviceId { get; set; }
    public Phase2Device? Device { get; set; }

    public bool IsPreAssessment { get; set; }

    public InspectionCategory Category { get; set; }

    [MaxLength(256)]
    public string? Notes { get; set; }

    [MaxLength(128)]
    public string? DocumentRef { get; set; }

    [MaxLength(128)]
    public string? PerformedBy { get; set; }

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
