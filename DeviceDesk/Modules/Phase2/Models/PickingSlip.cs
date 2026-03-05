using System.ComponentModel.DataAnnotations;

namespace DeviceDesk.Modules.Phase2.Models;

public class PickingSlip
{
    public Guid Id { get; set; }

    [MaxLength(64)]
    public string SlipNumber { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Reference { get; set; }

    public long? SchoolId { get; set; }

    [MaxLength(256)]
    public string? SchoolName { get; set; }

    [MaxLength(128)]
    public string? District { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(128)]
    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTime? RequestedCollectionDate { get; set; }

    public string? Notes { get; set; }

    public PickingSlipStatus Status { get; set; } = PickingSlipStatus.Draft;

    public ICollection<PickingSlipItem> Items { get; set; } = new List<PickingSlipItem>();
}

