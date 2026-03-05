using System;
using System.ComponentModel.DataAnnotations;

namespace DeviceDesk.Modules.Phase2.Models;

public class DeviceScan
{
    public Guid Id { get; set; }

    [MaxLength(100)]
    public string DeviceSerial { get; set; } = string.Empty;

    public DateTime ScanTime { get; set; } = DateTime.UtcNow;

    [MaxLength(128)]
    public string ScannedBy { get; set; } = string.Empty;

    [MaxLength(255)]
    public string Location { get; set; } = string.Empty;

    [MaxLength(255)]
    public string Purpose { get; set; } = string.Empty;
}



