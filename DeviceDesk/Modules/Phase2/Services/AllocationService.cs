using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Modules.Phase2.Data;
using DeviceDesk.Modules.Phase2.Models;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase2.Services;

public class AllocationService
{
    private readonly Phase2DbContext _phase2Db;
    private readonly DeviceDeskDbContext _coreDb;
    private readonly AuditService _audit;
    private readonly AutoAllocationService _autoAllocation;

    // Stages where allocation is allowed
    private static readonly Phase2Stage[] AllowedStages = new[]
    {
        Phase2Stage.Received,        // Receipting
        Phase2Stage.PreAssessment,
        Phase2Stage.DetailedInspection,  // Assessment
        Phase2Stage.HardwareDept,    // Repair
        Phase2Stage.SoftwareDept,    // Repair
        Phase2Stage.QualityAssessment // Quality
    };

    // Stages where allocation is blocked (device has left ICT)
    private static readonly Phase2Stage[] BlockedStages = new[]
    {
        Phase2Stage.Dispatch,
        Phase2Stage.AwaitingDispatch,
        Phase2Stage.Disposal,
        Phase2Stage.SchoolAllocation
    };

    public AllocationService(Phase2DbContext phase2Db, DeviceDeskDbContext coreDb, AuditService audit, AutoAllocationService autoAllocation)
    {
        _phase2Db = phase2Db;
        _coreDb = coreDb;
        _audit = audit;
        _autoAllocation = autoAllocation;
    }

    public async Task<Phase2Device?> FindDeviceBySerialAsync(string serial, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(serial)) return null;
        var s = serial.Trim();

        return await _phase2Db.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Serial == s, ct);
    }

    public async Task<DeviceStorageLocation?> GetPhase2StorageAsync(int phase2DeviceId, CancellationToken ct = default)
    {
        return await _phase2Db.DeviceStorageLocations
            .AsNoTracking()
            .Where(x => x.Phase2DeviceId == phase2DeviceId && x.Status == "Active")
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Get the default storage location for a school.
    /// Returns the first active storage location associated with the school.
    /// </summary>
    public async Task<StorageLocation?> GetSchoolStorageLocationAsync(long schoolId, CancellationToken ct = default)
    {
        return await _coreDb.StorageLocations
            .AsNoTracking()
            .Where(sl => sl.SchoolId == schoolId && sl.IsActive)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Get suggested storage allocation for a device based on its school and category.
    /// </summary>
    public async Task<SuggestedAllocationDto?> GetSuggestedAllocationAsync(int phase2DeviceId, CancellationToken ct = default)
    {
        return await _autoAllocation.GetSuggestedAllocationAsync(phase2DeviceId, ct);
    }

    public async Task<DeviceStorageLocation> AllocatePhase2StorageAsync(
        int phase2DeviceId,
        int? storageLocationId,
        string? building,
        string? room,
        string? rack,
        string? shelf,
        string? bin,
        string? notes,
        string userId,
        CancellationToken ct = default)
    {
        var device = await _phase2Db.Devices
            .FirstOrDefaultAsync(d => d.Id == phase2DeviceId, ct);

        if (device == null)
            throw new InvalidOperationException($"Phase2Device {phase2DeviceId} not found.");

        // Validate stage - allocation is only allowed in certain stages
        if (!AllowedStages.Contains(device.Stage))
        {
            throw new InvalidOperationException(
                $"Device cannot be allocated in its current stage ({device.Stage}). " +
                $"Allocation is only allowed in: {string.Join(", ", AllowedStages)}");
        }

        // Check if device already has active storage - prevent re-allocation
        var existing = await _phase2Db.DeviceStorageLocations
            .Where(x => x.Phase2DeviceId == phase2DeviceId && x.Status == "Active")
            .FirstOrDefaultAsync(ct);

        if (existing != null)
        {
            var existingLocation = $"{existing.Building ?? ""} > {existing.Room ?? ""} > Rack {existing.Rack ?? ""} > Shelf {existing.Shelf ?? ""} > Bin {existing.Bin ?? ""}";
            throw new InvalidOperationException(
                $"Device already has an active storage allocation at: {existingLocation}. " +
                $"Storage allocations cannot be changed once assigned. " +
                $"If you need to change the location, please contact an administrator.");
        }

        // If device has SchoolId and no storageLocationId provided, try to get school's default storage location
        if (!storageLocationId.HasValue && device.SchoolId.HasValue)
        {
            var schoolStorageLocation = await GetSchoolStorageLocationAsync(device.SchoolId.Value, ct);
            if (schoolStorageLocation != null)
            {
                storageLocationId = schoolStorageLocation.Id;
                // If building/room/rack/shelf/bin are not provided, use the school's storage location name as a suggestion
                // (The actual detailed location is still stored in DeviceStorageLocation, but we link to the school's location)
            }
        }

        // Create new storage location record
        var storage = new DeviceStorageLocation
        {
            Phase2DeviceId = phase2DeviceId,
            StorageLocationId = storageLocationId,
            Building = building?.Trim(),
            Room = room?.Trim(),
            Rack = rack?.Trim(),
            Shelf = shelf?.Trim(),
            Bin = bin?.Trim(),
            Notes = notes?.Trim(),
            Status = "Active",
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = userId
        };

        _phase2Db.DeviceStorageLocations.Add(storage);
        await _phase2Db.SaveChangesAsync(ct);

        // Mark slot as occupied if we have all location details and device has school
        if (device.SchoolId.HasValue && 
            !string.IsNullOrEmpty(building) && 
            !string.IsNullOrEmpty(room) && 
            !string.IsNullOrEmpty(rack) && 
            !string.IsNullOrEmpty(shelf) && 
            !string.IsNullOrEmpty(bin))
        {
            try
            {
                // Get device category
                var coreDevice = await _coreDb.Devices
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.SerialNumber == device.Serial, ct);
                
                var category = coreDevice?.Category ?? Infrastructure.Data.Enums.DeviceCategory.Unknown;
                if (category != Infrastructure.Data.Enums.DeviceCategory.Unknown)
                {
                    await _autoAllocation.MarkSlotOccupiedAsync(
                        phase2DeviceId,
                        device.SchoolId.Value,
                        category,
                        building,
                        room,
                        rack,
                        shelf,
                        bin,
                        ct);
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail allocation if slot marking fails
                Console.WriteLine($"[AllocationService] Warning: Failed to mark slot as occupied: {ex.Message}");
            }
        }

        // Audit log
        await _audit.LogAsync(
            userId,
            "StorageAllocated",
            phase2DeviceId,
            device.Serial,
            $"Location: {building}/{room}/{rack}/{shelf}/{bin}");

        return storage;
    }

    public async Task ClearAllocationAsync(int phase2DeviceId, string userId, CancellationToken ct = default)
    {
        var device = await _phase2Db.Devices
            .FirstOrDefaultAsync(d => d.Id == phase2DeviceId, ct);

        if (device == null)
            throw new InvalidOperationException($"Phase2Device {phase2DeviceId} not found.");

        // Validate stage - can only clear if device is still in ICT
        if (!AllowedStages.Contains(device.Stage))
        {
            throw new InvalidOperationException(
                $"Cannot clear storage; device has left ICT centre or is disposed (Stage: {device.Stage}).");
        }

        var active = await _phase2Db.DeviceStorageLocations
            .Where(x => x.Phase2DeviceId == phase2DeviceId && x.Status == "Active")
            .FirstOrDefaultAsync(ct);

        if (active != null)
        {
            active.Status = "Archived";
            active.UpdatedAt = DateTimeOffset.UtcNow;
            await _phase2Db.SaveChangesAsync(ct);

            // Mark slot as available
            try
            {
                await _autoAllocation.MarkSlotAvailableAsync(phase2DeviceId, ct);
            }
            catch (Exception ex)
            {
                // Log but don't fail if slot marking fails
                Console.WriteLine($"[AllocationService] Warning: Failed to mark slot as available: {ex.Message}");
            }

            await _audit.LogAsync(
                userId,
                "StorageCleared",
                phase2DeviceId,
                device.Serial,
                "Storage allocation cleared");
        }
    }

    public async Task<List<PendingAllocationDto>> GetPendingAllocationsAsync(CancellationToken ct = default)
    {
        // Devices in Receipting or PreAssessment stages without storage
        var devices = await _phase2Db.Devices
            .AsNoTracking()
            .Where(d =>
                (d.Stage == Phase2Stage.Received || d.Stage == Phase2Stage.PreAssessment) &&
                !_phase2Db.DeviceStorageLocations.Any(s =>
                    s.Phase2DeviceId == d.Id && s.Status == "Active"))
            .OrderByDescending(d => d.ReceivingDate ?? d.CreatedAt)
            .Select(d => new PendingAllocationDto
            {
                Phase2DeviceId = d.Id,
                Serial = d.Serial,
                Stage = d.Stage.ToString(),
                ReceivingDate = d.ReceivingDate ?? d.CreatedAt
            })
            .ToListAsync(ct);

        // Get school info - prefer Phase2Device, fallback to core Device
        var serials = devices.Select(d => d.Serial).ToList();
        if (serials.Count > 0)
        {
            // Get Phase2Device school info
            var phase2Devices = await _phase2Db.Devices
                .AsNoTracking()
                .Where(d => serials.Contains(d.Serial))
                .Select(d => new { d.Serial, d.SchoolId, d.SchoolName })
                .ToListAsync(ct);

            // Get core Device school info as fallback
            var coreDevices = await _coreDb.Devices
                .AsNoTracking()
                .Where(d => d.SerialNumber != null && serials.Contains(d.SerialNumber))
                .Select(d => new { d.SerialNumber, d.SchoolId, d.SchoolName, d.Model, d.Brand, d.DeviceType })
                .ToListAsync(ct);

            foreach (var device in devices)
            {
                var p2 = phase2Devices.FirstOrDefault(p => p.Serial == device.Serial);
                var core = coreDevices.FirstOrDefault(c => c.SerialNumber == device.Serial);
                
                // Prefer Phase2Device, fallback to core Device, then explicit fallback
                device.SchoolId = p2?.SchoolId ?? (core?.SchoolId.HasValue == true ? (int?)core.SchoolId.Value : null);
                device.SchoolName = p2?.SchoolName ?? core?.SchoolName ?? "No School Linked";
                
                // Get model with better fallback logic
                if (core != null && !string.IsNullOrWhiteSpace(core.Model))
                {
                    device.Model = core.Model;
                }
                else if (core != null && !string.IsNullOrWhiteSpace(core.Brand))
                {
                    // Fallback: use Brand + DeviceType if Model is empty
                    device.Model = core.Brand;
                    if (!string.IsNullOrWhiteSpace(core.DeviceType))
                    {
                        device.Model += $" {core.DeviceType}";
                    }
                }
                else
                {
                    device.Model = "Model Not Available";
                }
            }
        }

        return devices;
    }

    public async Task<List<StorageOverviewDto>> GetStorageOverviewAsync(CancellationToken ct = default)
    {
        // Load all active storage locations with their devices
        var storageLocations = await _phase2Db.DeviceStorageLocations
            .AsNoTracking()
            .Where(x => x.Status == "Active")
            .Include(x => x.Phase2Device)
            .ToListAsync(ct);

        // Get serials for school lookup
        var serials = storageLocations
            .Where(sl => sl.Phase2Device != null && !string.IsNullOrEmpty(sl.Phase2Device.Serial))
            .Select(sl => sl.Phase2Device.Serial)
            .Distinct()
            .ToList();

        // Get Phase2Device school info
        var phase2Devices = new Dictionary<string, (int? SchoolId, string? SchoolName)>();
        if (serials.Count > 0)
        {
            var p2Devices = await _phase2Db.Devices
                .AsNoTracking()
                .Where(d => serials.Contains(d.Serial))
                .Select(d => new { d.Serial, d.SchoolId, d.SchoolName })
                .ToListAsync(ct);

            foreach (var d in p2Devices)
            {
                phase2Devices[d.Serial] = (d.SchoolId, d.SchoolName);
            }
        }

        // Get core Device school info as fallback
        var coreDevices = new Dictionary<string, (long? SchoolId, string? SchoolName)>();
        if (serials.Count > 0)
        {
            var coreDevs = await _coreDb.Devices
                .AsNoTracking()
                .Where(d => d.SerialNumber != null && serials.Contains(d.SerialNumber))
                .Select(d => new { d.SerialNumber, d.SchoolId, d.SchoolName })
                .ToListAsync(ct);

            foreach (var d in coreDevs)
            {
                if (!string.IsNullOrEmpty(d.SerialNumber))
                {
                    coreDevices[d.SerialNumber] = (d.SchoolId, d.SchoolName);
                }
            }
        }

        // Group by location and build DTOs
        var grouped = storageLocations
            .Where(sl => sl.Phase2Device != null)
            .GroupBy(x => new
            {
                x.Building,
                x.Room,
                x.Rack,
                x.Shelf,
                x.Bin
            })
            .Select(g => new
            {
                Location = g.Key,
                StorageLocations = g.ToList()
            })
            .ToList();

        var overview = new List<StorageOverviewDto>();

        foreach (var group in grouped)
        {
            var devices = new List<DeviceInLocationDto>();

            foreach (var storage in group.StorageLocations)
            {
                if (storage.Phase2Device == null || string.IsNullOrEmpty(storage.Phase2Device.Serial))
                    continue;

                var serial = storage.Phase2Device.Serial;
                int? schoolId = null;
                string? schoolName = null;

                // Prefer Phase2Device, fallback to core Device
                if (phase2Devices.TryGetValue(serial, out var p2Info))
                {
                    schoolId = p2Info.SchoolId;
                    schoolName = p2Info.SchoolName;
                }
                else if (coreDevices.TryGetValue(serial, out var coreInfo))
                {
                    schoolId = coreInfo.SchoolId.HasValue ? (int?)coreInfo.SchoolId.Value : null;
                    schoolName = coreInfo.SchoolName;
                }

                devices.Add(new DeviceInLocationDto
                {
                    Phase2DeviceId = storage.Phase2Device.Id,
                    Serial = serial,
                    SchoolName = schoolName ?? "No School Linked",
                    SchoolId = schoolId
                });
            }

            overview.Add(new StorageOverviewDto
            {
                Building = group.Location.Building ?? "",
                Room = group.Location.Room ?? "",
                Rack = group.Location.Rack ?? "",
                Shelf = group.Location.Shelf ?? "",
                Bin = group.Location.Bin ?? "",
                DeviceCount = devices.Count,
                Devices = devices
            });
        }

        return overview
            .OrderBy(x => x.Building)
            .ThenBy(x => x.Room)
            .ThenBy(x => x.Rack)
            .ThenBy(x => x.Shelf)
            .ThenBy(x => x.Bin)
            .ToList();
    }

    public async Task<List<UnallocatedDeviceDto>> GetUnallocatedDevicesAsync(CancellationToken ct = default)
    {
        // Get all Phase2 devices with their IDs
        var allDevices = await _phase2Db.Devices
            .AsNoTracking()
            .Select(d => new { d.Id, d.Serial, d.Stage, d.ReceivingDate, d.CreatedAt, d.SchoolId, d.SchoolName })
            .ToListAsync(ct);

        // Get all devices WITH active storage (more efficient than subquery)
        var devicesWithStorage = await _phase2Db.DeviceStorageLocations
            .AsNoTracking()
            .Where(s => s.Status == "Active")
            .Select(s => s.Phase2DeviceId)
            .Distinct()
            .ToListAsync(ct);

        // Filter to unallocated devices (in memory)
        var unallocated = allDevices
            .Where(d => !devicesWithStorage.Contains(d.Id))
            .Select(d => new UnallocatedDeviceDto
            {
                Phase2DeviceId = d.Id,
                Serial = d.Serial,
                Stage = d.Stage.ToString(),
                ReceivingDate = d.ReceivingDate ?? d.CreatedAt
            })
            .OrderBy(d => d.Stage)
            .ThenBy(d => d.ReceivingDate)
            .ToList();

        // Get school info - prefer Phase2Device, fallback to core Device
        var serials = unallocated.Select(d => d.Serial).ToList();
        if (serials.Count > 0)
        {
            // Get core Device school info as fallback
            var coreDevices = await _coreDb.Devices
                .AsNoTracking()
                .Where(d => d.SerialNumber != null && serials.Contains(d.SerialNumber))
                .Select(d => new { d.SerialNumber, d.SchoolName, d.Model })
                .ToListAsync(ct);

            foreach (var device in unallocated)
            {
                var phase2Device = allDevices.FirstOrDefault(p2 => p2.Serial == device.Serial);
                var coreDevice = coreDevices.FirstOrDefault(cd => cd.SerialNumber == device.Serial);
                
                // Prefer Phase2Device.SchoolName, fallback to Device.SchoolName, then explicit fallback
                device.SchoolName = phase2Device?.SchoolName ?? coreDevice?.SchoolName ?? "No School Linked";
                device.Model = coreDevice?.Model ?? "Unknown";
            }
        }

        return unallocated;
    }

    public async Task<List<SchoolInStorageDto>> GetSchoolsInStorageAsync(CancellationToken ct = default)
    {
        // Get devices with active storage and their schools
        var devicesWithStorage = await _phase2Db.DeviceStorageLocations
            .AsNoTracking()
            .Where(x => x.Status == "Active")
            .Include(x => x.Phase2Device)
            .Select(x => new
            {
                x.Phase2Device.Serial,
                x.Phase2Device.Stage
            })
            .ToListAsync(ct);

        var serials = devicesWithStorage.Select(d => d.Serial).ToList();
        if (serials.Count == 0)
        {
            return new List<SchoolInStorageDto>();
        }

        // Get Phase2Device school info (preferred)
        var phase2Devices = await _phase2Db.Devices
            .AsNoTracking()
            .Where(d => serials.Contains(d.Serial))
            .Select(d => new
            {
                d.Serial,
                d.SchoolId,
                d.SchoolName
            })
            .ToListAsync(ct);

        // Get core Device school info as fallback
        var coreDevices = await _coreDb.Devices
            .AsNoTracking()
            .Where(d => d.SerialNumber != null && serials.Contains(d.SerialNumber))
            .Select(d => new
            {
                d.SerialNumber,
                d.SchoolId,
                d.SchoolName
            })
            .ToListAsync(ct);

        // Combine: prefer Phase2Device, fallback to Device
        var schoolMap = new Dictionary<string, (int? SchoolId, string? SchoolName)>();
        foreach (var serial in serials)
        {
            var phase2Device = phase2Devices.FirstOrDefault(p2 => p2.Serial == serial);
            var coreDevice = coreDevices.FirstOrDefault(cd => cd.SerialNumber == serial);
            
            var schoolId = phase2Device?.SchoolId ?? (coreDevice?.SchoolId.HasValue == true ? (int?)coreDevice.SchoolId.Value : null);
            var schoolName = phase2Device?.SchoolName ?? coreDevice?.SchoolName;
            
            if (schoolId.HasValue || !string.IsNullOrEmpty(schoolName))
            {
                schoolMap[serial] = (schoolId, schoolName);
            }
        }

        var result = schoolMap
            .Where(kvp => kvp.Value.SchoolId.HasValue || !string.IsNullOrEmpty(kvp.Value.SchoolName))
            .GroupBy(kvp => new { 
                SchoolId = kvp.Value.SchoolId ?? 0, 
                SchoolName = kvp.Value.SchoolName ?? "No School Linked" 
            })
            .Select(g => new SchoolInStorageDto
            {
                SchoolId = g.Key.SchoolId > 0 ? (long)g.Key.SchoolId : 0,
                SchoolName = g.Key.SchoolName,
                TotalDevices = g.Count(),
                ByStage = devicesWithStorage
                    .Where(dws => g.Any(kvp => kvp.Key == dws.Serial))
                    .GroupBy(dws => dws.Stage.ToString())
                    .ToDictionary(grp => grp.Key, grp => grp.Count())
            })
            .OrderBy(x => x.SchoolName)
            .ToList();

        return result;
    }

    public async Task<List<SchoolDeviceDetailDto>> GetSchoolDevicesInStorageAsync(long schoolId, CancellationToken ct = default)
    {
        // Convert long schoolId to int for Phase2Device comparison
        int schoolIdInt = schoolId > int.MaxValue ? 0 : (int)schoolId;
        
        // Get devices with active storage for this school
        // First get devices where Phase2Device.SchoolId matches
        var phase2DeviceIds = await _phase2Db.Devices
            .AsNoTracking()
            .Where(d => d.SchoolId == schoolIdInt)
            .Select(d => d.Id)
            .ToListAsync(ct);
        
        // Also get devices where core Device.SchoolId matches but Phase2Device.SchoolId is null
        var coreDeviceSerials = await _coreDb.Devices
            .AsNoTracking()
            .Where(d => d.SchoolId == schoolId && d.SerialNumber != null)
            .Select(d => d.SerialNumber!)
            .ToListAsync(ct);
        
        var phase2DeviceIdsFromCore = await _phase2Db.Devices
            .AsNoTracking()
            .Where(d => d.SchoolId == null && coreDeviceSerials.Contains(d.Serial))
            .Select(d => d.Id)
            .ToListAsync(ct);
        
        var allDeviceIds = phase2DeviceIds.Union(phase2DeviceIdsFromCore).ToList();
        
        if (allDeviceIds.Count == 0)
        {
            return new List<SchoolDeviceDetailDto>();
        }
        
        var devicesWithStorage = await _phase2Db.DeviceStorageLocations
            .AsNoTracking()
            .Where(x => x.Status == "Active" && allDeviceIds.Contains(x.Phase2DeviceId))
            .Include(x => x.Phase2Device)
            .Select(x => new
            {
                x.Phase2Device.Id,
                x.Phase2Device.Serial,
                x.Phase2Device.Stage,
                x.Phase2Device.SchoolName,
                x.Building,
                x.Room,
                x.Rack,
                x.Shelf,
                x.Bin,
                x.CreatedAt
            })
            .ToListAsync(ct);

        // Get model info from core devices
        var serials = devicesWithStorage.Select(d => d.Serial).ToList();
        var coreDevices = await _coreDb.Devices
            .AsNoTracking()
            .Where(d => d.SerialNumber != null && serials.Contains(d.SerialNumber))
            .Select(d => new { d.SerialNumber, d.Model, d.Brand, d.DeviceType })
            .ToListAsync(ct);

        var result = devicesWithStorage.Select(d => 
        {
            var coreDevice = coreDevices.FirstOrDefault(c => c.SerialNumber == d.Serial);
            var model = coreDevice?.Model ?? 
                       (!string.IsNullOrWhiteSpace(coreDevice?.Brand) 
                           ? $"{coreDevice.Brand} {coreDevice?.DeviceType}".Trim() 
                           : "Unknown");

            return new SchoolDeviceDetailDto
            {
                DeviceId = d.Id,
                Serial = d.Serial,
                Model = model,
                Stage = d.Stage.ToString(),
                SchoolName = d.SchoolName,
                Building = d.Building ?? "",
                Room = d.Room ?? "",
                Rack = d.Rack ?? "",
                Shelf = d.Shelf ?? "",
                Bin = d.Bin ?? "",
                AllocatedAt = d.CreatedAt
            };
        })
        .OrderBy(d => d.Serial)
        .ToList();

        return result;
    }
}

// DTOs
public class PendingAllocationDto
{
    public int Phase2DeviceId { get; set; }
    public string Serial { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public DateTime? ReceivingDate { get; set; }
    public int? SchoolId { get; set; }
    public string? SchoolName { get; set; }
    public string? Model { get; set; }
}

public class StorageOverviewDto
{
    public string Building { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public string Rack { get; set; } = string.Empty;
    public string Shelf { get; set; } = string.Empty;
    public string Bin { get; set; } = string.Empty;
    public int DeviceCount { get; set; }
    public List<DeviceInLocationDto> Devices { get; set; } = new();
}

public class DeviceInLocationDto
{
    public int Phase2DeviceId { get; set; }
    public string Serial { get; set; } = string.Empty;
    public string? SchoolName { get; set; }
    public int? SchoolId { get; set; }
}

public class UnallocatedDeviceDto
{
    public int Phase2DeviceId { get; set; }
    public string Serial { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public DateTime? ReceivingDate { get; set; }
    public string? SchoolName { get; set; }
    public string? Model { get; set; }
}

public class SchoolInStorageDto
{
    public long SchoolId { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public int TotalDevices { get; set; }
    public Dictionary<string, int> ByStage { get; set; } = new();
}

public class SchoolDeviceDetailDto
{
    public int DeviceId { get; set; }
    public string Serial { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public string? SchoolName { get; set; }
    public string Building { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public string Rack { get; set; } = string.Empty;
    public string Shelf { get; set; } = string.Empty;
    public string Bin { get; set; } = string.Empty;
    public DateTimeOffset? AllocatedAt { get; set; }
}

