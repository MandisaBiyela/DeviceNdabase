using System;
using System.Threading;
using System.Threading.Tasks;
using DeviceDesk.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase2.Services
{
    public class LocationService : ILocationService
    {
        private readonly DeviceDeskDbContext _coreDb;

        public LocationService(DeviceDeskDbContext coreDb)
        {
            _coreDb = coreDb;
        }

        public async Task MoveDeviceAsync(
            Guid deviceId,
            int toLocationId,
            string? reason,
            string? userId,
            CancellationToken ct = default)
        {
            var now = DateTimeOffset.UtcNow;

            var current = await _coreDb.DeviceLocations
                .FirstOrDefaultAsync(x => x.DeviceId == deviceId && x.IsCurrent, ct);

            if (current != null)
            {
                current.IsCurrent = false;
            }

            var movement = new DeviceLocation
            {
                DeviceId = deviceId,
                StorageLocationId = toLocationId,
                MovedAt = now,
                MovedByUserId = userId,
                IsCurrent = true
            };

            var history = new DeviceLocationHistory
            {
                DeviceId = deviceId,
                FromLocationId = current?.StorageLocationId,
                ToLocationId = toLocationId,
                Timestamp = now,
                Reason = reason,
                MovedByUserId = userId
            };

            _coreDb.DeviceLocations.Add(movement);
            _coreDb.DeviceLocationHistory.Add(history);

            await _coreDb.SaveChangesAsync(ct);
        }
    }
}

