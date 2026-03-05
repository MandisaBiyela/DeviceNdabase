namespace DeviceDesk.Modules.Phase1.Models
{
    /// <summary>
    /// DTO for importing new stock devices from Excel/CSV
    /// Used for Phase 1 New Stock receiving workflow
    /// </summary>
    public class NewStockImportRow
    {
        public string OrderNumber { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int RowNumber { get; set; }
    }
}
