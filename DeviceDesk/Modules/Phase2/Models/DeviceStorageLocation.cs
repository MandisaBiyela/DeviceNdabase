using System.ComponentModel.DataAnnotations;

namespace DeviceDesk.Modules.Phase2.Models;

public class DeviceStorageLocation
{
    public int Id { get; set; }

    public int Phase2DeviceId { get; set; }
    public Phase2Device Phase2Device { get; set; } = null!;

    // Optional link to core StorageLocation
    public int? StorageLocationId { get; set; }

    // Detailed physical location
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

    public string? Notes { get; set; }

    [MaxLength(64)]
    public string Status { get; set; } = "Active"; // Active, Moved, Archived

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }

    [MaxLength(128)]
    public string? CreatedByUserId { get; set; }

    // Bulk allocation session link
    public Guid? BulkSessionId { get; set; }
    public BulkAllocationSession? BulkSession { get; set; }
}

