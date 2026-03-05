using Microsoft.AspNetCore.Identity;

namespace DeviceDesk.Infrastructure.Identity
{
    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; }
        public string? Department { get; set; }
        public string? EmployeeNumber { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public static class UserRoles
    {
        public const string OrdersClerk = "OrdersClerk";
        public const string ReceivingClerk = "ReceivingClerk";
        public const string Supervisor = "Supervisor";
        public const string Admin = "Admin";
        public const string SuperAdmin = "SuperAdmin";
        // Phase 2 roles
        public const string IctClerk = "IctClerk";
        public const string IctInspector = "IctInspector";
        public const string IctTechnician = "IctTechnician";
        public const string IctManager = "IctManager";
        public const string IctAllocator = "IctAllocator";
        // Phase 3 roles
        public const string DispatchClerk = "DispatchClerk";
        public const string DispatchDriver = "DispatchDriver";
        public const string Driver = "Driver";
        public const string DispatchQA = "DispatchQA";
        public const string DispatchManager = "DispatchManager";
    }
}
