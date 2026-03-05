using DeviceDesk.Middleware;

namespace DeviceDesk.Services
{
    /// <summary>
    /// Centralized validation service for common validation patterns
    /// </summary>
    public class ValidationService
    {
        /// <summary>
        /// Validate required GUID parameter
        /// </summary>
        public static void ValidateGuid(Guid id, string parameterName)
        {
            if (id == Guid.Empty)
            {
                throw new ValidationException(parameterName, $"{parameterName} cannot be empty");
            }
        }

        /// <summary>
        /// Validate required string parameter
        /// </summary>
        public static void ValidateRequired(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ValidationException(fieldName, $"{fieldName} is required");
            }
        }

        /// <summary>
        /// Validate file upload
        /// </summary>
        public static void ValidateFile(IFormFile? file, string[] allowedExtensions)
        {
            if (file == null || file.Length == 0)
            {
                throw new ValidationException("file", "No file uploaded. Please select a file.");
            }

            var extension = Path.GetExtension(file.FileName).ToLower();
            if (!allowedExtensions.Contains(extension))
            {
                throw new ValidationException("file", 
                    $"Invalid file format. Allowed formats: {string.Join(", ", allowedExtensions)}");
            }
        }

        /// <summary>
        /// Validate serial number format
        /// </summary>
        public static void ValidateSerialNumber(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial))
            {
                throw new ValidationException("serial", "Serial number is required");
            }

            if (serial.Length < 3)
            {
                throw new ValidationException("serial", "Serial number must be at least 3 characters");
            }

            if (serial.Length > 100)
            {
                throw new ValidationException("serial", "Serial number cannot exceed 100 characters");
            }
        }

        /// <summary>
        /// Validate multiple fields at once
        /// </summary>
        public static void ValidateMultiple(params (string field, string? value)[] validations)
        {
            var errors = new List<ValidationError>();

            foreach (var (field, value) in validations)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    errors.Add(new ValidationError(field, $"{field} is required"));
                }
            }

            if (errors.Any())
            {
                throw new ValidationException(errors);
            }
        }

        /// <summary>
        /// Check if resource exists, throw NotFoundException if not
        /// </summary>
        public static void EnsureExists<T>(T? resource, string resourceType, object id) where T : class
        {
            if (resource == null)
            {
                throw new NotFoundException(resourceType, id);
            }
        }

        /// <summary>
        /// Check for duplicate, throw ConflictException if found
        /// </summary>
        public static void EnsureUnique<T>(T? existing, string message) where T : class
        {
            if (existing != null)
            {
                throw new ConflictException(message);
            }
        }

        /// <summary>
        /// Validate business rule
        /// </summary>
        public static void EnsureBusinessRule(bool condition, string message)
        {
            if (!condition)
            {
                throw new BusinessRuleException(message);
            }
        }
    }
}
