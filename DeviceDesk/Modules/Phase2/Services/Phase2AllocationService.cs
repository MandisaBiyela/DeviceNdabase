using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Modules.Phase1.Models;
using DeviceDesk.Modules.Phase2.Data;
using DeviceDesk.Modules.Phase2.Models;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase2.Services
{
    public interface IPhase2AllocationService
    {
        Task AssignStudentTeacherAsync(
            int phase2DeviceId,
            DeviceAllocationDto allocation,
            string userId,
            CancellationToken ct);
    }

    public class Phase2AllocationService : IPhase2AllocationService
    {
        private readonly Phase2DbContext _phase2Db;
        private readonly DeviceDeskDbContext _coreDb;

        public Phase2AllocationService(
            Phase2DbContext phase2Db,
            DeviceDeskDbContext coreDb)
        {
            _phase2Db = phase2Db;
            _coreDb = coreDb;
        }

        public async Task AssignStudentTeacherAsync(
            int phase2DeviceId,
            DeviceAllocationDto allocation,
            string userId,
            CancellationToken ct)
        {
            var p2 = await _phase2Db.Devices
                .FirstOrDefaultAsync(d => d.Id == phase2DeviceId, ct);
            
            if (p2 == null)
                throw new InvalidOperationException("Phase 2 device not found.");
            
            // ✅ Allocation allowed right after receipting - no QA requirement
            // Devices can be allocated early and go through repair while already assigned
            
            if (p2.DisposalRequested == true || p2.Stage == Phase2Stage.Disposal)
                throw new InvalidOperationException("Cannot allocate disposed devices.");

            var coreDevice = await _coreDb.Devices
                .FirstOrDefaultAsync(d => d.SerialNumber == p2.Serial, ct);
            
            if (coreDevice == null)
                throw new InvalidOperationException("Core device record not found for serial: " + p2.Serial);

            // Apply allocation – one side only
            coreDevice.AllocationType = (AllocationType)allocation.AllocationType;
            
            if (allocation.AllocationType == AllocationTypeDto.Student)
            {
                coreDevice.StudentName = allocation.StudentName?.Trim();
                coreDevice.StudentIdNumber = allocation.StudentIdNumber?.Trim();
                coreDevice.TeacherName = null;
                coreDevice.TeacherPersalNumber = null;
            }
            else if (allocation.AllocationType == AllocationTypeDto.Teacher)
            {
                coreDevice.TeacherName = allocation.TeacherName?.Trim();
                coreDevice.TeacherPersalNumber = allocation.TeacherPersalNumber?.Trim();
                coreDevice.StudentName = null;
                coreDevice.StudentIdNumber = null;
            }
            else
            {
                coreDevice.StudentName = null;
                coreDevice.StudentIdNumber = null;
                coreDevice.TeacherName = null;
                coreDevice.TeacherPersalNumber = null;
            }

            coreDevice.AllocatedAt = DateTimeOffset.UtcNow;
            coreDevice.AllocatedByUserId = userId;

            // Persal validation – numeric only
            if (allocation.AllocationType == AllocationTypeDto.Teacher &&
                !string.IsNullOrWhiteSpace(coreDevice.TeacherPersalNumber) &&
                !coreDevice.TeacherPersalNumber.All(char.IsDigit))
            {
                throw new InvalidOperationException("Persal number must be numeric.");
            }

            // Phase2 AuditLog
            _phase2Db.AuditLogs.Add(new Models.AuditLog
            {
                DeviceId = p2.Id,
                DeviceSerial = p2.Serial,
                UserId = userId,
                Action = "StudentTeacherAllocated",
                Details = $"AllocatedType={coreDevice.AllocationType}; Serial={p2.Serial}; School={p2.SchoolName}",
                Timestamp = DateTime.UtcNow
            });

            await _coreDb.SaveChangesAsync(ct);
            await _phase2Db.SaveChangesAsync(ct);
        }
    }
}
