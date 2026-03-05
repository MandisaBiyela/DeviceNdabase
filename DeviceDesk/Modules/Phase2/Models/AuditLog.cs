using System.ComponentModel.DataAnnotations;

namespace DeviceDesk.Modules.Phase2.Models;

public class AuditLog
{
    public int Id { get; set; }

    public int? DeviceId { get; set; }

    [MaxLength(100)]
    public string? DeviceSerial { get; set; }

    [MaxLength(128)]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? Details { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
