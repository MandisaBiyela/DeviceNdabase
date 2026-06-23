namespace DeviceDesk.Modules.Phase0.Models
{
    public record CreateProcurementOrderRequest(
        string PoNumber,
        string ProjectName,
        string FinancialYear,
        decimal TotalOrderValue,
        decimal ManagementFeePercentage,
        decimal TotalInvoicedToDepartment,
        decimal TotalPaidByDepartment,
        decimal TotalPaidToSuppliers,
        List<CreateProcurementOrderSchoolRequest> Schools,
        string? SupplierName = null,
        DateTimeOffset? ExpectedDeliveryDate = null
    );

    public record CreateProcurementOrderSchoolRequest(
        string SchoolName,
        decimal SchoolSubTotal,
        List<CreateProcurementOrderItemRequest> Items
    );

    public record CreateProcurementOrderItemRequest(
        string Description,
        decimal UnitPrice,
        int QtyOrdered,
        decimal TotalPrice,
        SchoolItemDeliveryStatus DeliveryStatus,
        string? Brand = null,
        string? Model = null,
        string? DeviceType = null
    );
}
