using System.ComponentModel.DataAnnotations;

namespace DeviceDesk.Modules.Phase2.Models;

public class Phase2RepairRequest
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public Phase2Device? Device { get; set; }
    
    [MaxLength(100)]
    public string DeviceSerial { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? WarrantyRoute { get; set; }
    public bool IsUnderWarranty { get; set; }
    
    public InspectionCategory Category { get; set; }
    
    public string? SymptomDescription { get; set; }
    public string? TechnicianFindings { get; set; }
    public string? HardwareChecklistSummary { get; set; }
    
    [MaxLength(200)]
    public string? RecommendedAction { get; set; }
    
    [MaxLength(50)]
    public string? Priority { get; set; }
    
    public decimal? EstimatedLabourHours { get; set; }
    
    public RepairStatus Status { get; set; } = RepairStatus.PendingAuthorization;
    
    [MaxLength(128)]
    public string CreatedByUserId { get; set; } = string.Empty;
    
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    
    public ICollection<Phase2RepairPart> Parts { get; set; } = new List<Phase2RepairPart>();
}

