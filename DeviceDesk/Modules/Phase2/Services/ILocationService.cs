using System;
using System.Threading;
using System.Threading.Tasks;

namespace DeviceDesk.Modules.Phase2.Services
{
    public interface ILocationService
    {
        Task MoveDeviceAsync(
            Guid deviceId,
            int toLocationId,
            string? reason,
            string? userId,
            CancellationToken ct = default);
    }
}

