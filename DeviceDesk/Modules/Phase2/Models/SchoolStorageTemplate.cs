using System.ComponentModel.DataAnnotations;
using DeviceDesk.Infrastructure.Data.Enums;

namespace DeviceDesk.Modules.Phase2.Models;

public class SchoolStorageTemplate
{
    public int Id { get; set; }

    public long SchoolId { get; set; }
    public DeviceCategory Category { get; set; }

    [MaxLength(128)]
    public string Building { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Room { get; set; } = string.Empty;

    [MaxLength(64)]
    public string RackPattern { get; set; } = "Rack {n:00}"; // e.g., "Rack {n:00}" becomes "Rack 01", "Rack 02", etc.

    [MaxLength(64)]
    public string ShelfPattern { get; set; } = "Shelf {n:00}"; // e.g., "Shelf {n:00}" becomes "Shelf 01", "Shelf 02", etc.

    [MaxLength(64)]
    public string BinPattern { get; set; } = "Bin {n:00}"; // e.g., "Bin {n:00}" becomes "Bin 01", "Bin 02", etc.

    public int MaxRacks { get; set; } = 10; // Maximum number of racks for this school+category
    public int MaxShelvesPerRack { get; set; } = 10; // Maximum shelves per rack
    public int MaxBinsPerShelf { get; set; } = 10; // Maximum bins per shelf

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}

