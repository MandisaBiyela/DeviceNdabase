using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DeviceDesk.Modules.Phase2.Data;
using DeviceDesk.Modules.Phase2.Models;
using Microsoft.Extensions.Logging;

namespace DeviceDesk.Modules.Phase2.Services;

public class DispatchService
{
    private readonly Phase2DbContext _db;
    private readonly AuditService _audit;
    private readonly ILogger<DispatchService> _logger;

    public DispatchService(Phase2DbContext db, AuditService audit, ILogger<DispatchService> logger)
    {
        _db = db;
        _audit = audit;
        _logger = logger;
    }

    public async Task<List<Phase2Device>> GetReadyForDispatchAsync(string? filter = null)
    {
        var q = _db.Devices.AsQueryable()
            .Where(d => d.Stage == Phase2Stage.AwaitingDispatch && d.QaPassed == true && d.ScannedOutAt == null);

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var term = filter.Trim();
            q = q.Where(d => d.Serial.Contains(term));
        }

        return await Task.FromResult(q.OrderByDescending(d => d.UpdatedAt).Take(500).ToList());
    }

    public async Task<Phase2Device> ScanOutBySerialAsync(string serial, string userId)
    {
        var device = _db.Devices.FirstOrDefault(d => d.Serial == serial);
        if (device == null) throw new InvalidOperationException($"Serial '{serial}' not found.");
        return await ScanOutInternalAsync(device, userId);
    }

    public async Task<Phase2Device> ScanOutByIdAsync(int deviceId, string userId)
    {
        var device = _db.Devices.FirstOrDefault(d => d.Id == deviceId);
        if (device == null) throw new InvalidOperationException("Device not found.");
        return await ScanOutInternalAsync(device, userId);
    }

    private async Task<Phase2Device> ScanOutInternalAsync(Phase2Device device, string userId)
    {
        if (device == null)
            throw new InvalidOperationException("Device not found.");

        if (device.QaPassed != true)
            throw new InvalidOperationException("Device has not passed QA.");

        if (!(device.Stage == Phase2Stage.AwaitingDispatch && device.ScannedOutAt == null))
            throw new InvalidOperationException("Device not ready for dispatch scan-out.");

        if (device.ScannedOutAt != null)
            throw new InvalidOperationException("Device already scanned out.");

        _logger.LogInformation("[ScanOut] Starting scan-out for device {DeviceId} (Serial: {Serial}) by user {UserId}", 
            device.Id, device.Serial, userId);

        var scannedOutAt = DateTimeOffset.UtcNow;
        device.Stage = Phase2Stage.Dispatch;
        device.ScannedOutAt = scannedOutAt;
        device.ScannedOutByUserId = userId;
        device.UpdatedAt = scannedOutAt;
        
        await _db.SaveChangesAsync();
        _logger.LogInformation("[ScanOut] Successfully saved scan-out for device {DeviceId} (Serial: {Serial}) at {ScannedOutAt}", 
            device.Id, device.Serial, scannedOutAt);

        // Audit trail
        await _audit.LogAsync(userId, "DispatchScanOut", device.Id, device.Serial, "Scanned out to Dispatch");
        
        _logger.LogInformation("[ScanOut] Completed scan-out for device {DeviceId} (Serial: {Serial}) - Stage: {Stage}, ScannedOutAt: {ScannedOutAt}", 
            device.Id, device.Serial, device.Stage, device.ScannedOutAt);
        
        return device;
    }

    public async Task<List<Phase2Device>> GetScanOutHistoryAsync(int take = 50)
    {
        var q = _db.Devices.Where(d => d.ScannedOutAt != null)
            .OrderByDescending(d => d.ScannedOutAt)
            .Take(take);
        return await Task.FromResult(q.ToList());
    }

    public record BatchScanOutResult(string Serial, bool Success, string Message);

    public async Task<List<BatchScanOutResult>> ScanOutManyBySerialAsync(IEnumerable<string> serials, string userId)
    {
        var results = new List<BatchScanOutResult>();
        foreach (var serial in serials)
        {
            var s = serial?.Trim();
            if (string.IsNullOrWhiteSpace(s)) continue;
            try
            {
                await ScanOutBySerialAsync(s, userId);
                results.Add(new BatchScanOutResult(s, true, "Scanned out"));
            }
            catch (InvalidOperationException ex)
            {
                results.Add(new BatchScanOutResult(s!, false, ex.Message));
            }
        }
        return results;
    }
}