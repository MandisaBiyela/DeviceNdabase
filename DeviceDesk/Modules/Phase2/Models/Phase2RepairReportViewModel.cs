namespace DeviceDesk.Modules.Phase2.Models;

public class Phase2RepairReportViewModel
{
    public string RepairNumber { get; set; } = string.Empty;
    public DateTimeOffset ReportDate { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    
    // School & Device
    public string SchoolName { get; set; } = string.Empty;
    public string Emis { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string DeviceSerial { get; set; } = string.Empty;
    public string ItemDescription { get; set; } = string.Empty;
    public string? AssetTag { get; set; }
    public string? PodNumber { get; set; }
    
    // Inspection
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public bool IsUnderWarranty { get; set; }
    public string? WarrantyRoute { get; set; }
    public decimal? EstimatedLabourHours { get; set; }
    public string Status { get; set; } = string.Empty;
    
    // Fault
    public string Symptoms { get; set; } = string.Empty;
    public string Findings { get; set; } = string.Empty;
    public string? HardwareChecklistSummary { get; set; }
    
    // Parts & Totals
    public List<Phase2RepairReportPartViewModel> Parts { get; set; } = new();
    public decimal? PartsSubtotal { get; set; }
    public decimal? LabourTotal { get; set; }
    public decimal VatRate { get; set; } = 15m;
    public decimal? VatAmount { get; set; }
    public decimal? GrandTotal { get; set; }
    
    public string? RecommendedAction { get; set; }
}

public class Phase2RepairReportPartViewModel
{
    public string PartName { get; set; } = string.Empty;
    public string? PartNumber { get; set; }
    public int Quantity { get; set; }
    public decimal? UnitCost { get; set; }
    public decimal? LineTotal { get; set; }
}

