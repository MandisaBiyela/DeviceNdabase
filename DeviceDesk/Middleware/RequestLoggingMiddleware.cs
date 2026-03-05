using System.Diagnostics;
using System.Text;

namespace DeviceDesk.Middleware
{
    /// <summary>
    /// Middleware to log all incoming HTTP requests and responses
    /// Helps with debugging API routing and tracking request flow
    /// </summary>
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var requestId = Guid.NewGuid().ToString("N")[..8];

            // Log incoming request
            LogRequest(context, requestId);

            // Capture response
            var originalBodyStream = context.Response.Body;
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            try
            {
                await _next(context);
            }
            finally
            {
                stopwatch.Stop();
                
                // Log response
                LogResponse(context, requestId, stopwatch.ElapsedMilliseconds);

                // Copy response back to original stream
                responseBody.Seek(0, SeekOrigin.Begin);
                await responseBody.CopyToAsync(originalBodyStream);
            }
        }

        private void LogRequest(HttpContext context, string requestId)
        {
            var request = context.Request;
            var phase = GetPhaseFromPath(request.Path);
            
            _logger.LogInformation(
                "[{RequestId}] {Phase} → {Method} {Path} | User: {User} | IP: {IP}",
                requestId,
                phase,
                request.Method,
                request.Path,
                context.User?.Identity?.Name ?? "Anonymous",
                context.Connection.RemoteIpAddress?.ToString() ?? "Unknown"
            );

            // Log query parameters if any
            if (request.QueryString.HasValue)
            {
                _logger.LogDebug("[{RequestId}] Query: {QueryString}", requestId, request.QueryString);
            }
        }

        private void LogResponse(HttpContext context, string requestId, long elapsedMs)
        {
            var statusCode = context.Response.StatusCode;
            var statusEmoji = GetStatusEmoji(statusCode);
            var phase = GetPhaseFromPath(context.Request.Path);

            var logLevel = statusCode >= 500 ? LogLevel.Error :
                          statusCode >= 400 ? LogLevel.Warning :
                          LogLevel.Information;

            _logger.Log(
                logLevel,
                "[{RequestId}] {Phase} ← {StatusCode} {StatusEmoji} | {ElapsedMs}ms",
                requestId,
                phase,
                statusCode,
                statusEmoji,
                elapsedMs
            );
        }

        private static string GetPhaseFromPath(PathString path)
        {
            var pathValue = path.Value?.ToLower() ?? "";
            
            if (pathValue.Contains("/api/phase0")) return "PHASE 0";
            if (pathValue.Contains("/api/phase1")) return "PHASE 1";
            if (pathValue.Contains("/api/phase2")) return "PHASE 2";
            if (pathValue.Contains("/api/auth")) return "AUTH";
            
            return "STATIC";
        }

        private static string GetStatusEmoji(int statusCode)
        {
            return statusCode switch
            {
                >= 200 and < 300 => "✅",
                >= 300 and < 400 => "↪️",
                >= 400 and < 500 => "⚠️",
                >= 500 => "❌",
                _ => "❓"
            };
        }
    }
}
