using Microsoft.AspNetCore.Identity;

namespace DeviceDesk.Infrastructure.Identity
{
    public class UserSeeder
    {
        public static async Task SeedUsersAsync(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            // Create roles (Phase 0/1 + Phase 2 ICT + Phase 3 Dispatch + Admin)
            string[] roles = {
                UserRoles.OrdersClerk, UserRoles.ReceivingClerk, UserRoles.Supervisor, UserRoles.Admin,
                UserRoles.IctClerk, UserRoles.IctInspector, UserRoles.IctTechnician, UserRoles.IctManager,
                UserRoles.IctAllocator, UserRoles.DispatchClerk, UserRoles.DispatchDriver, 
                UserRoles.DispatchQA, UserRoles.DispatchManager, UserRoles.SuperAdmin
            };
            
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // Create Orders Clerk
            var ordersClerkEmail = "orders.clerk@local";
            if (await userManager.FindByEmailAsync(ordersClerkEmail) == null)
            {
                var ordersClerk = new ApplicationUser
                {
                    UserName = ordersClerkEmail,
                    Email = ordersClerkEmail,
                    EmailConfirmed = true,
                    FullName = "Orders Clerk",
                    Department = "Phase 0 - Procurement"
                };

                var result = await userManager.CreateAsync(ordersClerk, "P@ssw0rd1!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(ordersClerk, UserRoles.OrdersClerk);
                }
            }

            // Create Receiving Clerk
            var receivingClerkEmail = "receiving.clerk@local";
            if (await userManager.FindByEmailAsync(receivingClerkEmail) == null)
            {
                var receivingClerk = new ApplicationUser
                {
                    UserName = receivingClerkEmail,
                    Email = receivingClerkEmail,
                    EmailConfirmed = true,
                    FullName = "Receiving Clerk",
                    Department = "Phase 1 - Receiving"
                };

                var result = await userManager.CreateAsync(receivingClerk, "P@ssw0rd1!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(receivingClerk, UserRoles.ReceivingClerk);
                }
            }

            // Create ICT users
            await CreateUser(userManager, UserRoles.IctClerk,      "ict.clerk@local",      "ICT Clerk",      "Phase 2 - ICT Center");
            await CreateUser(userManager, UserRoles.IctInspector,  "ict.inspector@local",  "ICT Inspector",  "Phase 2 - ICT Center");
            await CreateUser(userManager, UserRoles.IctTechnician, "ict.technician@local", "ICT Technician", "Phase 2 - ICT Center");
            await CreateUser(userManager, UserRoles.IctManager,    "ict.manager@local",    "ICT Manager",    "Phase 2 - ICT Center");
            await CreateUser(userManager, UserRoles.IctAllocator,  "ict.allocator@local",  "ICT Allocator",  "Phase 2 - ICT Center");

            // Create Dispatch Clerk (Phase 3)
            await CreateUser(userManager, UserRoles.DispatchClerk, "dispatch.clerk@local", "Dispatch Clerk", "Phase 3 - Dispatch");

            // Create SuperAdmin
            await CreateUser(userManager, UserRoles.SuperAdmin, "superadmin@local", "Super Admin", "System Administration");

            // General Manager not seeded in this build

            static async Task CreateUser(UserManager<ApplicationUser> userManager, string role, string email, string fullName, string dept)
            {
                if (await userManager.FindByEmailAsync(email) != null) return;
                var user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = fullName,
                    Department = dept
                };
                var result = await userManager.CreateAsync(user, "P@ssw0rd1!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, role);
                }
            }
        }
    }
}
