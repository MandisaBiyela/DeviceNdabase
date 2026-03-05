using System.Net;
using System.Text.Json;

namespace DeviceDesk.Middleware
{
    public class GlobalErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalErrorHandlingMiddleware> _logger;

        public GlobalErrorHandlingMiddleware(RequestDelegate next, ILogger<GlobalErrorHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
                await HandleExceptionAsync(context, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var code = HttpStatusCode.InternalServerError; // 500 by default
            var result = string.Empty;

            switch (exception)
            {
                case ValidationException validationException:
                    code = HttpStatusCode.BadRequest; // 400
                    result = JsonSerializer.Serialize(new
                    {
                        error = "Validation failed",
                        errors = validationException.Errors
                    });
                    break;

                case UnauthorizedAccessException:
                    code = HttpStatusCode.Unauthorized; // 401
                    result = JsonSerializer.Serialize(new
                    {
                        error = "Authentication required",
                        details = "You must be logged in to access this resource"
                    });
                    break;

                case ForbiddenException:
                    code = HttpStatusCode.Forbidden; // 403
                    result = JsonSerializer.Serialize(new
                    {
                        error = "Access denied",
                        details = "You do not have permission to access this resource"
                    });
                    break;

                case NotFoundException notFoundException:
                    code = HttpStatusCode.NotFound; // 404
                    result = JsonSerializer.Serialize(new
                    {
                        error = "Resource not found",
                        details = notFoundException.Message
                    });
                    break;

                case ConflictException conflictException:
                    code = HttpStatusCode.Conflict; // 409
                    result = JsonSerializer.Serialize(new
                    {
                        error = "Conflict",
                        details = conflictException.Message
                    });
                    break;

                case BusinessRuleException businessRuleException:
                    code = HttpStatusCode.UnprocessableEntity; // 422
                    result = JsonSerializer.Serialize(new
                    {
                        error = "Business rule violation",
                        details = businessRuleException.Message
                    });
                    break;

                default:
                    result = JsonSerializer.Serialize(new
                    {
                        error = "Internal server error",
                        details = exception.Message,
                        stackTrace = exception.StackTrace // Only in development
                    });
                    break;
            }

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)code;

            return context.Response.WriteAsync(result);
        }
    }

    // Custom Exception Classes
    public class ValidationException : Exception
    {
        public List<ValidationError> Errors { get; }

        public ValidationException(List<ValidationError> errors) : base("Validation failed")
        {
            Errors = errors;
        }

        public ValidationException(string field, string message) : base("Validation failed")
        {
            Errors = new List<ValidationError> { new ValidationError(field, message) };
        }
    }

    public class ValidationError
    {
        public string Field { get; set; }
        public string Message { get; set; }

        public ValidationError(string field, string message)
        {
            Field = field;
            Message = message;
        }
    }

    public class NotFoundException : Exception
    {
        public NotFoundException(string message) : base(message) { }
        public NotFoundException(string resourceType, object id) : base($"{resourceType} with ID '{id}' not found") { }
    }

    public class ConflictException : Exception
    {
        public ConflictException(string message) : base(message) { }
    }

    public class ForbiddenException : Exception
    {
        public ForbiddenException(string message = "Access denied") : base(message) { }
    }

    public class BusinessRuleException : Exception
    {
        public BusinessRuleException(string message) : base(message) { }
    }
}
