using DeviceDesk.Modules.Phase2.Data;
using DeviceDesk.Modules.Phase2.Models;
using Microsoft.EntityFrameworkCore;
using Phase2AuditActions = DeviceDesk.Modules.Phase2.Models.AuditActions;
using System.Security.Cryptography;
using System.Text;

namespace DeviceDesk.Modules.Phase2.Services;

public class DisposalService
{
    private readonly Phase2DbContext _db;
    private readonly AuditService _audit;
    public DisposalService(Phase2DbContext db, AuditService audit) 
    { 
        _db = db; 
        _audit = audit;
    }

    // Step 2.5: Technician requests disposal
    // Returns: (DisposalId, Reused) where Reused is always false (duplicates blocked)
    public async Task<(int DisposalId, bool Reused)> RequestDisposalAsync(int deviceId, string technicianId, string reason)
    {
        var device = await _db.Devices.FindAsync(deviceId) ?? throw new InvalidOperationException("Device not found");

        // Block if already approved and in Disposal stage
        var alreadyDisposed = device.Stage == Phase2Stage.Disposal ||
            await _db.Disposals.AnyAsync(d => d.DeviceId == deviceId && d.IsApproved);
        if (alreadyDisposed)
        {
            throw new InvalidOperationException("Device has already been disposed.");
        }

        // Block duplicate pending disposals
        var hasPending = await _db.Disposals.AnyAsync(d => d.DeviceId == deviceId && !d.IsApproved);
        if (hasPending)
        {
            throw new InvalidOperationException("There is already a pending disposal request for this device.");
        }

        // Create a new pending disposal request
        device.DisposalRequested = true;
        device.UpdatedAt = DateTime.UtcNow;

        var disposal = new DisposalRecord
        {
            DeviceId = deviceId,
            RequestedBy = technicianId,
            RequestedAt = DateTime.UtcNow,
            Reason = reason,
            IsApproved = false
        };

        _db.Disposals.Add(disposal);
        await _db.SaveChangesAsync();

        await _audit.LogAsync(technicianId, Phase2AuditActions.DisposalRequested, deviceId, device.Serial, reason);
        return (disposal.Id, false);
    }

    // Step 2.6: Manager approves disposal with PIN + signature
    public async Task ApproveDisposalAsync(int disposalId, string managerId, string managerPin, string managerSignature)
    {
        var disposal = await _db.Disposals.FindAsync(disposalId) ?? throw new InvalidOperationException("Disposal request not found");
        var device = await _db.Devices.FindAsync(disposal.DeviceId) ?? throw new InvalidOperationException("Device not found");
        
        // Hash the PIN for security
        var pinHash = HashPin(managerPin);
        
        disposal.ApprovedBy = managerId;
        disposal.ManagerSignature = managerSignature;
        disposal.ManagerPinHash = pinHash;
        disposal.ApprovedAt = DateTime.UtcNow;
        disposal.IsApproved = true;
        
        // Step 2.7: Generate disposal document path (placeholder)
        disposal.DocumentPath = $"/documents/disposal/{device.Serial}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
        
        // Only at manager approval does the device move to Disposal
        device.Stage = Phase2Stage.Disposal;
        device.DisposalRequested = false;
        device.UpdatedAt = DateTime.UtcNow;
        
        await _db.SaveChangesAsync();
        
        await _audit.LogAsync(managerId, "DisposalApproved", device.Id, device.Serial, $"Disposal ID: {disposalId}");
    }

    private static string HashPin(string pin)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(pin));
        return Convert.ToBase64String(bytes);
    }

    // List pending disposals requested by a specific technician
    public async Task<List<DisposalRecord>> ListPendingByTechnicianAsync(string technicianId)
    {
        return await _db.Disposals
            .Include(d => d.Device)
            .Where(d => d.RequestedBy == technicianId && !d.IsApproved)
            .OrderByDescending(d => d.RequestedAt)
            .ToListAsync();
    }
}
