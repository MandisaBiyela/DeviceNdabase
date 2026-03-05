using DeviceDesk.Modules.Phase2.Data;
using DeviceDesk.Modules.Phase2.Models;
using Phase2AuditActions = DeviceDesk.Modules.Phase2.Models.AuditActions;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase2.Services;

public class QualityService
{
    private readonly Phase2DbContext _db;
    private readonly AuditService _audit;
    private readonly DispatchService _dispatch;
    public QualityService(Phase2DbContext db, AuditService audit, DispatchService dispatch)
    { 
        _db = db;
        _audit = audit;
        _dispatch = dispatch;
    }

    // Step 3: Quality Assessment by Inspector
    public async Task RecordQualityAsync(int deviceId, string inspectorId, bool passed, string? notes, bool scanOutToDispatch, string? scanOutUserId)
    {
        var device = await _db.Devices.FindAsync(deviceId) ?? throw new InvalidOperationException("Device not found");

        device.QaPassed = passed;
        device.QaInspectorId = inspectorId;
        
        var attempts = await _db.Quality.Where(q => q.DeviceId == deviceId).CountAsync();
        device.ReworkCount = attempts; // Track before adding new record
        
        _db.Quality.Add(new QualityRecord
        {
            DeviceId = deviceId,
            Passed = passed,
            Attempts = attempts + 1,
            Notes = notes
        });

        if (passed)
        {
            device.Stage = Phase2Stage.AwaitingDispatch;
            if (scanOutToDispatch)
            {
                var scanUser = string.IsNullOrWhiteSpace(scanOutUserId) ? inspectorId : scanOutUserId;
                await _dispatch.ScanOutByIdAsync(device.Id, scanUser);
            }
        }
        else
        {
            // Step 3.3: Fail → Return to Technician for rework
            // Route back to appropriate department
            if (device.RepairCategory == InspectionCategory.HardwareFailure.ToString())
            {
                device.Stage = Phase2Stage.HardwareDept;
            }
            else if (device.RepairCategory == InspectionCategory.SoftwareIssueUpgrade.ToString())
            {
                device.Stage = Phase2Stage.SoftwareDept;
            }
            else
            {
                device.Stage = Phase2Stage.HardwareDept; // Default
            }
            
            // Step 3.4: Track repeated failures (for potential disposal)
            device.ReworkCount++;
        }

        device.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        
        await _audit.LogAsync(inspectorId, Phase2AuditActions.QualityAssessment, deviceId, device.Serial, $"Passed: {passed}, Attempt: {attempts + 1}");


    }
}
