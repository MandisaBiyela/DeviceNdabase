namespace DeviceDesk.Modules.Phase0.Models
{
    public record CreateProcurementOrderRequest(
        string PoNumber,
        string ProjectName,
        string FinancialYear,
        decimal TotalOrderValue,
        decimal TotalInvoicedToDepartment,
        decimal TotalPaidByDepartment,
        decimal TotalPaidToSuppliers,
        List<CreateProcurementOrderSchoolRequest> Schools
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
        SchoolItemDeliveryStatus DeliveryStatus
    );
}
