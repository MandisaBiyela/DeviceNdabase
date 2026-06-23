using DeviceDesk.Modules.Phase0.Models;

namespace DeviceDesk.Modules.Phase0.Services
{
    public class ProcurementOrderFinancialService
    {
        public static decimal RoundCurrency(decimal value) =>
            decimal.Round(value, 2, MidpointRounding.AwayFromZero);

        public (decimal ManagementFeeAmount, decimal SupplierFee) ComputeFees(
            decimal orderValue,
            decimal managementFeePercentage)
        {
            var feePct = RoundCurrency(managementFeePercentage);
            var managementFeeAmount = RoundCurrency(orderValue * (feePct / 100m));
            var supplierFee = RoundCurrency(orderValue - managementFeeAmount);
            return (managementFeeAmount, supplierFee);
        }

        public decimal ComputeTotalAllocatedToSchools(IEnumerable<ProcurementOrderSchool> schools) =>
            RoundCurrency(schools.Sum(s => s.SchoolSubTotal));

        public decimal ComputeAllocationVariance(decimal supplierFee, decimal totalAllocatedToSchools) =>
            RoundCurrency(supplierFee - totalAllocatedToSchools);

        public AllocationBalanceStatus GetAllocationBalanceStatus(decimal allocationVariance) =>
            allocationVariance == 0m ? AllocationBalanceStatus.Balanced : AllocationBalanceStatus.Unbalanced;

        public void ApplyStoredFees(ProcurementOrder order)
        {
            var (managementFeeAmount, supplierFee) = ComputeFees(
                order.TotalOrderValue,
                order.ManagementFeePercentage);
            order.ManagementFeeAmount = managementFeeAmount;
            order.SupplierFee = supplierFee;
        }

        public ProcurementOrderFinancialSummary Summarize(ProcurementOrder order)
        {
            var totalAllocated = ComputeTotalAllocatedToSchools(order.Schools);
            var supplierFee = order.SupplierFee > 0m || order.ManagementFeePercentage > 0m
                ? order.SupplierFee
                : ComputeFees(order.TotalOrderValue, order.ManagementFeePercentage).SupplierFee;
            var variance = ComputeAllocationVariance(supplierFee, totalAllocated);
            var outstandingFromDoe = RoundCurrency(order.TotalInvoicedToDepartment - order.TotalPaidByDepartment);
            var outstandingToSupplier = RoundCurrency(supplierFee - order.TotalPaidToSuppliers);

            return new ProcurementOrderFinancialSummary(
                order.TotalOrderValue,
                order.ManagementFeePercentage,
                order.ManagementFeeAmount > 0m || order.ManagementFeePercentage > 0m
                    ? order.ManagementFeeAmount
                    : ComputeFees(order.TotalOrderValue, order.ManagementFeePercentage).ManagementFeeAmount,
                supplierFee,
                totalAllocated,
                variance,
                GetAllocationBalanceStatus(variance),
                outstandingFromDoe,
                outstandingToSupplier);
        }
    }

    public record ProcurementOrderFinancialSummary(
        decimal OrderValue,
        decimal ManagementFeePercentage,
        decimal ManagementFeeAmount,
        decimal SupplierFee,
        decimal TotalAllocatedToSchools,
        decimal AllocationVariance,
        AllocationBalanceStatus AllocationBalanceStatus,
        decimal OutstandingFromDoe,
        decimal OutstandingToSupplier);
}
