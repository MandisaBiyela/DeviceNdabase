using System.ComponentModel.DataAnnotations;

namespace DeviceDesk.Modules.Phase2.Models;

public class PickingSlipItem
{
    public long Id { get; set; }

    public Guid PickingSlipId { get; set; }
    public PickingSlip PickingSlip { get; set; } = null!;

    public int Phase2DeviceId { get; set; }
    public Phase2Device Phase2Device { get; set; } = null!;

    // Snapshot fields (so slip doesn't break if location changes later)
    [MaxLength(100)]
    public string Serial { get; set; } = string.Empty;

    public long? SchoolId { get; set; }

    [MaxLength(256)]
    public string? SchoolName { get; set; }

    [MaxLength(128)]
    public string? District { get; set; }

    public Phase2Stage StageAtCreation { get; set; }

    // Location snapshots
    [MaxLength(128)]
    public string? Building { get; set; }

    [MaxLength(128)]
    public string? Room { get; set; }

    [MaxLength(64)]
    public string? Rack { get; set; }

    [MaxLength(64)]
    public string? Shelf { get; set; }

    [MaxLength(64)]
    public string? Bin { get; set; }

    // Picking tracking
    public bool IsPicked { get; set; }

    public DateTime? PickedAt { get; set; }

    [MaxLength(128)]
    public string? PickedByUserId { get; set; }
}

