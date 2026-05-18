using DeviceDesk.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DeviceDesk.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AuthController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { error = "Invalid login request." });
            }

            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                return Unauthorized(new { error = "Invalid email or password." });
            }

            // First: verify password
            var result = await _signInManager.CheckPasswordSignInAsync(
                user,
                request.Password,
                lockoutOnFailure: false
            );
            
            if (!result.Succeeded)
            {
                return Unauthorized(new { error = "Invalid email or password." });
            }

            // Fetch roles from Identity
            var roles = await _userManager.GetRolesAsync(user);
            Console.WriteLine($"[LOGIN] User {user.Email} has roles: {string.Join(", ", roles)}");

            // Build claims manually
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim(ClaimTypes.Name, user.UserName ?? string.Empty)
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
                Console.WriteLine($"[LOGIN] Added role claim: {ClaimTypes.Role} = {role}");
            }

            // Sign-in WITH these claims (this creates the auth cookie)
            await _signInManager.SignInWithClaimsAsync(user, isPersistent: true, claims);
            Console.WriteLine($"[LOGIN] Sign-in completed with {claims.Count} total claims");

            // Define priority: SuperAdmin > DispatchClerk > ReceivingClerk > ICT > OrdersClerk > default
            string primaryRole;
            if (roles.Contains("SuperAdmin"))
                primaryRole = "SuperAdmin";
            else if (roles.Contains("DispatchClerk"))
                primaryRole = "DispatchClerk";
            else if (roles.Contains("ReceivingClerk"))
                primaryRole = "ReceivingClerk";
            else if (roles.Contains("IctInspector") || roles.Contains("IctClerk") || roles.Contains("IctTechnician") || roles.Contains("IctManager"))
                primaryRole = "IctClerk"; // Use IctClerk as representative for ICT group
            else if (roles.Contains("OrdersClerk"))
                primaryRole = "OrdersClerk";
            else
                primaryRole = roles.FirstOrDefault() ?? "User";

            // Determine redirect URL based on role
            string redirectUrl = primaryRole switch
            {
                "SuperAdmin" => "/superadmin/dashboard.html",
                "DispatchClerk" => "/dispatch/index.html",
                "ReceivingClerk" => "/phase1/dashboard.html",
                "IctClerk" => "/phase2/index.html", // All ICT roles go to phase2
                "OrdersClerk" => "/phase0/new.html",
                _ => "/login.html"
            };

            return Ok(new
            {
                success = true,
                user = new
                {
                    userId = user.Id,
                    email = user.Email,
                    userName = user.UserName,
                    fullName = user.FullName,
                    department = user.Department,
                    role = primaryRole,
                    roles
                },
                redirectUrl
            });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return Ok(new { success = true, message = "Logged out successfully." });
        }

        // Public logout endpoint for sidebar links: GET /logout
        [HttpGet("/logout")]
        public async Task<IActionResult> LogoutGet()
        {
            await _signInManager.SignOutAsync();
            return Redirect("/login.html");
        }

        [HttpGet("current-user")]
        public async Task<IActionResult> GetCurrentUser()
        {
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                return Unauthorized(new { error = "Not authenticated." });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized(new { error = "User not found." });
            }

            var roles = await _userManager.GetRolesAsync(user);

            return Ok(new
            {
                email = user.Email,
                fullName = user.FullName,
                department = user.Department,
                role = roles.FirstOrDefault() ?? "User",
                roles
            });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { error = "Missing required fields." });
            }

            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
            {
                return NotFound(new { error = "User not found." });
            }

            var result = await _userManager.ResetPasswordAsync(user, request.Token, request.Password);
            if (!result.Succeeded)
            {
                var errorMessage = string.Join("; ", result.Errors.Select(e => e.Description));
                return BadRequest(new { error = errorMessage });
            }

            // Unlock account after successful reset (for first-time activation flow)
            user.LockoutEnd = null;
            user.LockoutEnabled = false;
            await _userManager.UpdateAsync(user);

            return Ok(new { success = true, message = "Password set successfully. You can now log in." });
        }
    }

    public record LoginRequest(string Email, string Password);
    public record ResetPasswordRequest(string UserId, string Token, string Password);
}
