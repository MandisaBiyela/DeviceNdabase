using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DeviceDesk.Modules.Phase2.Models;

public class BulkAllocationSession
{
    public Guid Id { get; set; }

    public long SchoolId { get; set; }

    [MaxLength(256)]
    public string SchoolName { get; set; } = string.Empty;

    [MaxLength(128)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public BulkAllocationStatus Status { get; set; } = BulkAllocationStatus.InProgress;

    public int DeviceCount { get; set; }

    // Navigation properties
    public ICollection<DeviceStorageLocation> Allocations { get; set; } = new List<DeviceStorageLocation>();
}

public enum BulkAllocationStatus
{
    InProgress = 0,
    Completed = 1,
    Cancelled = 2
}



