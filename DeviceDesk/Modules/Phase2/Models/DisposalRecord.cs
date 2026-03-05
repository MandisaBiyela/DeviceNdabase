using System.ComponentModel.DataAnnotations;

namespace DeviceDesk.Modules.Phase2.Models;

public class DisposalRecord
{
    public int Id { get; set; }

    public int DeviceId { get; set; }
    public Phase2Device? Device { get; set; }

    [MaxLength(128)]
    public string RequestedBy { get; set; } = string.Empty; // Technician ID

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(256)]
    public string? Reason { get; set; }

    [MaxLength(128)]
    public string? ApprovedBy { get; set; } // Manager ID

    [MaxLength(256)]
    public string? ManagerSignature { get; set; }

    [MaxLength(128)]
    public string? ManagerPinHash { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public bool IsApproved { get; set; } = false;

    [MaxLength(256)]
    public string? DocumentPath { get; set; }
}
