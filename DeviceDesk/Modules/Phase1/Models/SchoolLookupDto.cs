namespace DeviceDesk.Modules.Phase1.Models;

public class SchoolLookupDto
{
    public int SchoolId { get; set; }
    public string EmisCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Circuit { get; set; } = string.Empty;
    public string Cmc { get; set; } = string.Empty;
}

