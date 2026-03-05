using DeviceDesk.Modules.Phase2.Data;
using DeviceDesk.Modules.Phase2.Models;

namespace DeviceDesk.Modules.Phase2.Services;

public class AuditService
{
    private readonly Phase2DbContext _db;
    public AuditService(Phase2DbContext db) { _db = db; }

    public async Task LogAsync(string userId, string action, int? deviceId = null, string? deviceSerial = null, string? details = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            DeviceId = deviceId,
            DeviceSerial = deviceSerial,
            Details = details,
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }
}
