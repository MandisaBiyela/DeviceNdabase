namespace DeviceDesk.Services
{
    using System.Security.Claims;
    using System.Text.Json;
    using DeviceDesk.Infrastructure.Data;

    public interface IAuditLogService
    {
        Task LogAsync(ClaimsPrincipal user, string action, string entityType, string? entityId = null, object? meta = null, CancellationToken ct = default);
    }

    public class AuditLogService : IAuditLogService
    {
        private readonly DeviceDeskDbContext _db;
        public AuditLogService(DeviceDeskDbContext db) { _db = db; }

        public async Task LogAsync(ClaimsPrincipal user, string action, string entityType, string? entityId = null, object? meta = null, CancellationToken ct = default)
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
            var userName = user.Identity?.Name ?? "unknown";

            var log = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                UserName = userName,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                TimestampUtc = DateTime.UtcNow,
                MetaJson = meta == null ? null : JsonSerializer.Serialize(meta)
            };

            _db.AuditLogs.Add(log);
            await _db.SaveChangesAsync(ct);
        }
    }
}