using DeviceDesk.Middleware;
using DeviceDesk.Modules.Phase0.Models;

namespace DeviceDesk.Modules.Phase0.Services
{
    public class OrderValidationService
    {
        public void ValidateCreateRequest(CreateProcurementOrderRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PoNumber))
                throw new ValidationException("poNumber", "PO Number is required.");
            if (string.IsNullOrWhiteSpace(request.ProjectName))
                throw new ValidationException("projectName", "Project Name is required.");
            if (string.IsNullOrWhiteSpace(request.FinancialYear))
                throw new ValidationException("financialYear", "Financial Year is required.");

            if (request.TotalOrderValue < 0m || request.TotalInvoicedToDepartment < 0m ||
                request.TotalPaidByDepartment < 0m || request.TotalPaidToSuppliers < 0m)
            {
                throw new ValidationException("financials", "Financial values cannot be negative.");
            }

            if (request.Schools == null || request.Schools.Count == 0)
                throw new ValidationException("schools", "At least one school entry is required.");

            decimal schoolTotal = 0m;
            foreach (var school in request.Schools)
            {
                if (string.IsNullOrWhiteSpace(school.SchoolName))
                    throw new ValidationException("schoolName", "School name is required.");
                if (school.Items == null || school.Items.Count == 0)
                    throw new ValidationException("items", $"School '{school.SchoolName}' requires at least one item.");

                decimal subtotal = 0m;
                foreach (var item in school.Items)
                {
                    if (item.QtyOrdered > 0)
                    {
                        if (string.IsNullOrWhiteSpace(item.Description))
                            throw new ValidationException("description", "Item description is required when quantity is greater than zero.");
                        if (item.UnitPrice <= 0m)
                            throw new ValidationException("unitPrice", "Unit price is required when quantity is greater than zero.");
                    }

                    if (item.UnitPrice < 0m)
                        throw new ValidationException("unitPrice", "Unit price cannot be negative.");
                    if (item.QtyOrdered < 0)
                        throw new ValidationException("qtyOrdered", "Quantity cannot be negative.");
                    if (item.TotalPrice < 0m)
                        throw new ValidationException("totalPrice", "Total price cannot be negative.");

                    var expectedTotal = RoundCurrency(item.UnitPrice * item.QtyOrdered);
                    var providedTotal = RoundCurrency(item.TotalPrice);
                    if (expectedTotal != providedTotal)
                    {
                        var label = string.IsNullOrWhiteSpace(item.Description) ? "(line item)" : item.Description.Trim();
                        throw new BusinessRuleException(
                            $"Item total mismatch for '{label}'. Expected {expectedTotal:0.00}, received {providedTotal:0.00}.");
                    }

                    subtotal += providedTotal;
                }

                var providedSubTotal = RoundCurrency(school.SchoolSubTotal);
                var computedSubTotal = RoundCurrency(subtotal);
                if (providedSubTotal != computedSubTotal)
                {
                    throw new BusinessRuleException(
                        $"School subtotal mismatch for '{school.SchoolName}'. Expected {computedSubTotal:0.00}, received {providedSubTotal:0.00}.");
                }

                schoolTotal += computedSubTotal;
            }

            var parentTotal = RoundCurrency(request.TotalOrderValue);
            var allSchoolTotals = RoundCurrency(schoolTotal);
            if (parentTotal != allSchoolTotals)
            {
                throw new BusinessRuleException(
                    $"Order total mismatch. Parent total is {parentTotal:0.00} but school totals are {allSchoolTotals:0.00}.");
            }
        }

        private static decimal RoundCurrency(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
