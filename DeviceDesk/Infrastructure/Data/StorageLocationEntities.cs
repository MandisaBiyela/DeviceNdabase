using System;
using DeviceDesk.Infrastructure.Data.Enums;

namespace DeviceDesk.Infrastructure.Data
{
    public class StorageLocation
    {
        public int Id { get; set; }

        public long? SchoolId { get; set; }
        public School? School { get; set; }

        public DeviceCategory Category { get; set; }
        public StorageArea Area { get; set; }

        public string Name { get; set; } = string.Empty;
        public string LocationCode { get; set; } = string.Empty;

        public bool IsDispatchReadyZone { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class DeviceLocation
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid DeviceId { get; set; }
        public Device Device { get; set; } = default!;

        public int StorageLocationId { get; set; }
        public StorageLocation StorageLocation { get; set; } = default!;

        public DateTimeOffset MovedAt { get; set; } = DateTimeOffset.UtcNow;
        public string? MovedByUserId { get; set; }
        public bool IsCurrent { get; set; } = true;
    }

    public class DeviceLocationHistory
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid DeviceId { get; set; }
        public Device Device { get; set; } = default!;

        public int? FromLocationId { get; set; }
        public StorageLocation? FromLocation { get; set; }

        public int ToLocationId { get; set; }
        public StorageLocation ToLocation { get; set; } = default!;

        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
        public string? Reason { get; set; }
        public string? MovedByUserId { get; set; }
    }
}

