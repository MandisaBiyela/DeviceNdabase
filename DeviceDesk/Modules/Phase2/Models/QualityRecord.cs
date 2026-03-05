using System.ComponentModel.DataAnnotations;

namespace DeviceDesk.Modules.Phase2.Models;

public class QualityRecord
{
    public int Id { get; set; }

    public int DeviceId { get; set; }
    public Phase2Device? Device { get; set; }

    public bool Passed { get; set; }
    public int Attempts { get; set; }

    [MaxLength(256)]
    public string? Notes { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
