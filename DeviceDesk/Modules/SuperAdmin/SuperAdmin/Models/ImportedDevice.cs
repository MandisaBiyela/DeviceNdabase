using System.ComponentModel.DataAnnotations;

namespace DeviceDesk.Modules.SuperAdmin.Models;

public class ImportedDevice
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Serial { get; set; } = string.Empty;

    public long? SchoolId { get; set; }

    [MaxLength(256)]
    public string? SchoolName { get; set; }

    [MaxLength(50)]
    public string? EmisCode { get; set; }

    [MaxLength(100)]
    public string? District { get; set; }

    [MaxLength(100)]
    public string? Circuit { get; set; }

    [MaxLength(256)]
    public string? ItemDescription { get; set; }

    [MaxLength(50)]
    public string? PodNumber { get; set; }

    public DateTime? DateReceived { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

