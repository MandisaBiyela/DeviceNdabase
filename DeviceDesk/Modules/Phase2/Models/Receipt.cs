using System.ComponentModel.DataAnnotations;

namespace DeviceDesk.Modules.Phase2.Models;

public class Receipt
{
    public int Id { get; set; }

    [MaxLength(64)]
    public string GrvNumber { get; set; } = string.Empty;

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    public int ItemCount { get; set; }

    public List<Phase2Device> Devices { get; set; } = new();
}
