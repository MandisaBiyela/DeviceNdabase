using System.ComponentModel.DataAnnotations;
using DeviceDesk.Infrastructure.Data.Enums;

namespace DeviceDesk.Modules.Phase2.Models;

public class StorageSlotOccupancy
{
    public int Id { get; set; }

    public long SchoolId { get; set; }
    public DeviceCategory Category { get; set; }

    [MaxLength(128)]
    public string Building { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Room { get; set; } = string.Empty;

    [MaxLength(64)]
    public string Rack { get; set; } = string.Empty;

    [MaxLength(64)]
    public string Shelf { get; set; } = string.Empty;

    [MaxLength(64)]
    public string Bin { get; set; } = string.Empty;

    public int Phase2DeviceId { get; set; }
    public Phase2Device Phase2Device { get; set; } = null!;

    public bool IsOccupied { get; set; } = true;

    public DateTimeOffset OccupiedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? VacatedAt { get; set; }
}

