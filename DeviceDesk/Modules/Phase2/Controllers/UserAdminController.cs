using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DeviceDesk.Infrastructure.Identity;
using DeviceDesk.Modules.Phase2.Models;
using DeviceDesk.Modules.Phase2.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DeviceDesk.Modules.Phase2.Controllers
{
    [ApiController]
    [Route("api/phase2/users")]
    [Authorize(Roles = UserRoles.IctClerk)]
    public class UserAdminController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<UserAdminController> _logger;
        public UserAdminController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, IEmailSender emailSender, ILogger<UserAdminController> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _emailSender = emailSender;
            _logger = logger;
        }

        // GET /api/phase2/users?role=IctTechnician
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Phase2UserDto>>> GetUsers([FromQuery] string? role = null)
        {
            var users = _userManager.Users.ToList();
            var result = new List<Phase2UserDto>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);

                if (!string.IsNullOrEmpty(role) && !roles.Contains(role))
                    continue;

                var isActive = user.LockoutEnabled == false || user.LockoutEnd == null || user.LockoutEnd <= DateTimeOffset.UtcNow;

                result.Add(new Phase2UserDto
                {
                    Id = user.Id,
                    Email = user.Email ?? string.Empty,
                    FullName = user.FullName ?? string.Empty,
                    EmployeeNumber = user.EmployeeNumber,
                    IsActive = isActive,
                    Roles = roles.ToArray()
                });
            }

            return Ok(result);
        }

        // GET /api/phase2/users/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Phase2UserDto>> GetById(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            var isActive = user.LockoutEnabled == false || user.LockoutEnd == null || user.LockoutEnd <= DateTimeOffset.UtcNow;

            var dto = new Phase2UserDto
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName ?? string.Empty,
                EmployeeNumber = user.EmployeeNumber,
                IsActive = isActive,
                Roles = roles.ToArray()
            };

            return Ok(dto);
        }

        // POST /api/phase2/users
        [HttpPost]
        public async Task<ActionResult<Phase2UserDto>> CreateTechnician([FromBody] CreateTechnicianRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var existing = await _userManager.FindByEmailAsync(request.Email);
            if (existing != null)
                return BadRequest("A user with this email already exists.");

            const string roleName = UserRoles.IctTechnician;

            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                var roleResult = await _roleManager.CreateAsync(new IdentityRole(roleName));
                if (!roleResult.Succeeded)
                    return StatusCode(500, "Failed to ensure IctTechnician role exists.");
            }

            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                EmailConfirmed = true,
                FullName = request.FullName,
                Department = "Phase 2 - ICT Center",
                EmployeeNumber = request.EmployeeNumber
            };

            var tempPassword = GenerateTempPassword();

            var createResult = await _userManager.CreateAsync(user, tempPassword);
            if (!createResult.Succeeded)
                return BadRequest(createResult.Errors);

            var addRoleResult = await _userManager.AddToRoleAsync(user, roleName);
            if (!addRoleResult.Succeeded)
                return BadRequest(addRoleResult.Errors);

            // Require password reset before first login
            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
            await _userManager.UpdateAsync(user);

            var dto = new Phase2UserDto
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName ?? string.Empty,
                EmployeeNumber = user.EmployeeNumber,
                IsActive = true,
                Roles = new[] { roleName }
            };

            // Generate a password reset link for the new user
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var baseUrl = $"{Request.Scheme}://{Request.Host.Value}";
            var resetUrl = $"{baseUrl}/reset-password.html?userId={Uri.EscapeDataString(user.Id)}&token={Uri.EscapeDataString(token)}";

            await SafeSendWelcomeEmailAsync(user.Email ?? string.Empty, user.FullName ?? (user.Email ?? string.Empty), resetUrl);

            return CreatedAtAction(nameof(GetUsers), new { role = roleName }, dto);
        }

        private static string GenerateTempPassword()
        {
            var guid = Guid.NewGuid().ToString("N").Substring(0, 10);
            return $"P@{guid}!";
        }

        private async Task SafeSendWelcomeEmailAsync(string email, string fullName, string resetLink)
        {
            try
            {
                var body = $"Dear {fullName},\n\nAn ICT Clerk has created an account for you on DeviceDesk.\n\nTo activate your account, please set your password using this link:\n{resetLink}\n\nAfter setting your password, you can log in here:\nhttp://localhost:5170/login.html\n\nEmail: {email}\n\nRegards,\nDeviceDesk System";
                await _emailSender.SendEmailAsync(email, "Your DeviceDesk Technician Account  Set Your Password", body);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send welcome email to {Email}", email);
            }
        }

        // PUT /api/phase2/users/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTechnician(string id, [FromBody] UpdateTechnicianRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            // Only allow updating basic profile fields from this endpoint
            user.FullName = request.FullName;
            user.EmployeeNumber = request.EmployeeNumber;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return NoContent();
        }

        // DELETE /api/phase2/users/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTechnician(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            // Only allow delete for IctTechnician accounts from this interface
            if (!roles.Contains(UserRoles.IctTechnician))
                return BadRequest(new { error = "Only technician accounts can be deleted from this page." });

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return NoContent();
        }

        // POST /api/phase2/users/{id}/deactivate
        [HttpPost("{id}/deactivate")]
        public async Task<IActionResult> DeactivateUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.MaxValue;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return NoContent();
        }

        // POST /api/phase2/users/{id}/reactivate
        [HttpPost("{id}/reactivate")]
        public async Task<IActionResult> ReactivateUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.LockoutEnd = null;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return NoContent();
        }

        // POST /api/phase2/users/{id}/toggle-active
        [HttpPost("{id}/toggle-active")]
        public async Task<IActionResult> ToggleActive(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            if (user.LockoutEnabled && user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow)
            {
                user.LockoutEnd = null;
            }
            else
            {
                user.LockoutEnabled = true;
                user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
            }

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return NoContent();
        }
    }
}
