using System.ComponentModel.DataAnnotations;

namespace DeviceDesk.Modules.Phase2.Models
{
    public class CreateTechnicianRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string FullName { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string EmployeeNumber { get; set; } = string.Empty;

        // Optional: if null/empty, backend uses default password
        public string? Password { get; set; }
    }

    public class UpdateTechnicianRequest
    {
        [Required, MaxLength(200)]
        public string FullName { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string EmployeeNumber { get; set; } = string.Empty;
    }

    public class Phase2UserDto
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? EmployeeNumber { get; set; }
        public bool IsActive { get; set; }
        public string[] Roles { get; set; } = Array.Empty<string>();
    }
}
