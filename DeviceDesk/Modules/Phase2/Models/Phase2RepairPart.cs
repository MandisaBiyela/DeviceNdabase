using System.ComponentModel.DataAnnotations;

namespace DeviceDesk.Modules.Phase2.Models;

public class Phase2RepairPart
{
    public int Id { get; set; }
    public int RepairRequestId { get; set; }
    public Phase2RepairRequest? RepairRequest { get; set; }
    
    [MaxLength(200)]
    public string PartName { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? PartNumber { get; set; }
    
    public int Quantity { get; set; }
    public decimal? UnitCost { get; set; }
    
    [MaxLength(200)]
    public string? Supplier { get; set; }
}

