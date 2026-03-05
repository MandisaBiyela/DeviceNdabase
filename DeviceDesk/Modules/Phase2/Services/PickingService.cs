using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Infrastructure.Identity;
using DeviceDesk.Modules.Phase2.Data;
using DeviceDesk.Modules.Phase2.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase2.Services;

public class PickingService
{
    private readonly Phase2DbContext _phase2Db;
    private readonly DeviceDeskDbContext _coreDb;
    private readonly UserManager<ApplicationUser> _userManager;

    // Active slip statuses - devices on these slips cannot be added to another slip
    private static readonly PickingSlipStatus[] ActiveSlipStatuses = new[]
    {
        PickingSlipStatus.Draft,
        PickingSlipStatus.ReadyForPicking,
        PickingSlipStatus.PickingInProgress
    };

    public PickingService(Phase2DbContext phase2Db, DeviceDeskDbContext coreDb, UserManager<ApplicationUser> userManager)
    {
        _phase2Db = phase2Db;
        _coreDb = coreDb;
        _userManager = userManager;
    }

    /// <summary>
    /// Search devices available for picking (have Active storage, not on active picking slips)
    /// </summary>
    public async Task<List<DeviceForPickingDto>> SearchDevicesForPickingAsync(
        long? schoolId = null,
        string? district = null,
        string? serial = null,
        Phase2Stage? stage = null,
        CancellationToken ct = default)
    {
        // Start with devices that have active storage (keep as IQueryable for database query)
        var query = _phase2Db.Devices
            .Where(d => _phase2Db.DeviceStorageLocations
                .Any(s => s.Phase2DeviceId == d.Id && s.Status == "Active"));

        // Get device IDs that are on active picking slips
        var devicesOnActiveSlips = await _phase2Db.PickingSlipItems
            .Include(item => item.PickingSlip)
            .Where(item => ActiveSlipStatuses.Contains(item.PickingSlip.Status))
            .Select(item => item.Phase2DeviceId)
            .Distinct()
            .ToListAsync(ct);

        // Exclude devices on active picking slips
        if (devicesOnActiveSlips.Count > 0)
        {
            query = query.Where(d => !devicesOnActiveSlips.Contains(d.Id));
        }

        // School filter
        if (schoolId.HasValue)
        {
            query = query.Where(d => d.SchoolId == schoolId.Value);
        }

        // District filter - safe string comparison
        if (!string.IsNullOrWhiteSpace(district))
        {
            var districtTrimmed = district.Trim();
            if (!string.IsNullOrEmpty(districtTrimmed))
            {
                var districtLower = districtTrimmed.ToLowerInvariant();
                
                // Get school IDs from Schools table (long) and convert to int for Phase2Device comparison
                // Use safe null checking and case-insensitive comparison
                var districtSchoolIds = await _coreDb.Schools
                    .Where(s => s.District != null && 
                                !string.IsNullOrEmpty(s.District) &&
                                s.District.ToLower().Contains(districtLower))
                    .Select(s => (int)s.SchoolId) // Convert long to int for comparison with Phase2Device.SchoolId
                    .ToListAsync(ct);
                
                if (districtSchoolIds.Count > 0)
                {
                    query = query.Where(d => d.SchoolId.HasValue && districtSchoolIds.Contains(d.SchoolId.Value));
                }
                else
                {
                    // No schools found for this district, return empty result
                    return new List<DeviceForPickingDto>();
                }
            }
        }

        // Serial filter
        if (!string.IsNullOrWhiteSpace(serial))
        {
            var serialLower = serial.Trim().ToLowerInvariant();
            query = query.Where(d => d.Serial.ToLowerInvariant().Contains(serialLower));
        }

        // Stage filter
        if (stage.HasValue)
        {
            query = query.Where(d => d.Stage == stage.Value);
        }

        // Execute query and get devices
        var devices = await query
            .OrderBy(d => d.Serial)
            .ToListAsync(ct);

        // Get storage locations for these devices
        var deviceIds = devices.Select(d => d.Id).ToList();
        var storageLocations = await _phase2Db.DeviceStorageLocations
            .Where(s => deviceIds.Contains(s.Phase2DeviceId) && s.Status == "Active")
            .ToListAsync(ct);

        var storageMap = storageLocations
            .GroupBy(s => s.Phase2DeviceId)
            .ToDictionary(g => g.Key, g => g.First());

        // Get core device info for model/category
        var serials = devices.Select(d => d.Serial).ToList();
        var coreDevices = await _coreDb.Devices
            .Where(d => d.SerialNumber != null && serials.Contains(d.SerialNumber))
            .ToListAsync(ct);

        var coreDeviceMap = coreDevices
            .Where(d => !string.IsNullOrEmpty(d.SerialNumber))
            .ToDictionary(d => d.SerialNumber!, d => d);

        // Build DTOs
        var results = new List<DeviceForPickingDto>();

        foreach (var device in devices)
        {
            var storage = storageMap.GetValueOrDefault(device.Id);
            var coreDevice = coreDeviceMap.GetValueOrDefault(device.Serial);

            if (storage == null) continue; // Skip if no active storage

            string? modelName = null;
            if (coreDevice != null)
            {
                if (!string.IsNullOrWhiteSpace(coreDevice.Model))
                {
                    modelName = coreDevice.Model;
                }
                else if (!string.IsNullOrWhiteSpace(coreDevice.Brand))
                {
                    modelName = coreDevice.Brand;
                    if (!string.IsNullOrWhiteSpace(coreDevice.DeviceType))
                    {
                        modelName += $" {coreDevice.DeviceType}";
                    }
                }
            }

            results.Add(new DeviceForPickingDto
            {
                Phase2DeviceId = device.Id,
                Serial = device.Serial,
                SchoolId = device.SchoolId,
                SchoolName = device.SchoolName,
                District = device.SchoolId.HasValue
                    ? (await _coreDb.Schools
                        .Where(s => s.SchoolId == device.SchoolId.Value)
                        .Select(s => s.District)
                        .FirstOrDefaultAsync(ct))
                    : null,
                Stage = device.Stage.ToString(),
                Model = modelName ?? "Model Not Available",
                Category = coreDevice?.Category.ToString() ?? "Unknown",
                Building = storage.Building,
                Room = storage.Room,
                Rack = storage.Rack,
                Shelf = storage.Shelf,
                Bin = storage.Bin,
                Notes = storage.Notes
            });
        }

        return results;
    }

    /// <summary>
    /// Generate sequential slip number (PICK-YYYY-#####)
    /// </summary>
    public async Task<string> GenerateSlipNumberAsync(CancellationToken ct = default)
    {
        var year = DateTime.Now.Year;
        var prefix = $"PICK-{year}-";

        var lastSlip = await _phase2Db.PickingSlips
            .Where(s => s.SlipNumber.StartsWith(prefix))
            .OrderByDescending(s => s.SlipNumber)
            .FirstOrDefaultAsync(ct);

        int nextNumber = 1;
        if (lastSlip != null)
        {
            var lastNumberStr = lastSlip.SlipNumber.Substring(prefix.Length);
            if (int.TryParse(lastNumberStr, out int lastNumber))
            {
                nextNumber = lastNumber + 1;
            }
        }

        return $"{prefix}{nextNumber:D5}";
    }

    /// <summary>
    /// Create a picking slip with items
    /// </summary>
    public async Task<PickingSlip> CreatePickingSlipAsync(
        long? schoolId,
        DateTime? requestedCollectionDate,
        string? notes,
        string? reference,
        List<int> deviceIds,
        string userId,
        CancellationToken ct = default)
    {
        if (deviceIds == null || deviceIds.Count == 0)
        {
            throw new InvalidOperationException("At least one device ID is required.");
        }

        // Validate all devices exist and have active storage
        var devices = await _phase2Db.Devices
            .Where(d => deviceIds.Contains(d.Id))
            .ToListAsync(ct);

        if (devices.Count != deviceIds.Count)
        {
            throw new InvalidOperationException("One or more devices not found in Phase 2.");
        }

        // Check all devices have active storage
        var devicesWithStorage = await _phase2Db.DeviceStorageLocations
            .Where(s => deviceIds.Contains(s.Phase2DeviceId) && s.Status == "Active")
            .Select(s => s.Phase2DeviceId)
            .Distinct()
            .ToListAsync(ct);

        if (devicesWithStorage.Count != deviceIds.Count)
        {
            var missing = deviceIds.Except(devicesWithStorage).ToList();
            throw new InvalidOperationException(
                $"One or more devices do not have active storage allocation. Device IDs: {string.Join(", ", missing)}");
        }

        // Check no device is already on an active picking slip
        var devicesOnActiveSlips = await _phase2Db.PickingSlipItems
            .Include(item => item.PickingSlip)
            .Where(item => deviceIds.Contains(item.Phase2DeviceId) &&
                          ActiveSlipStatuses.Contains(item.PickingSlip.Status))
            .Select(item => new { item.Phase2DeviceId, item.PickingSlip.SlipNumber })
            .ToListAsync(ct);

        if (devicesOnActiveSlips.Any())
        {
            var conflict = devicesOnActiveSlips.First();
            throw new InvalidOperationException(
                $"Device ID {conflict.Phase2DeviceId} is already on an active picking slip: {conflict.SlipNumber}");
        }

        // Optional: enforce all devices from same school
        var distinctSchools = devices
            .Where(d => d.SchoolId.HasValue)
            .Select(d => d.SchoolId!.Value)
            .Distinct()
            .ToList();

        if (distinctSchools.Count > 1)
        {
            // Allow multi-school slips, but warn or enforce based on business rules
            // For now, we'll allow it but set schoolId to null if mixed
            schoolId = null;
        }
        else if (distinctSchools.Count == 1)
        {
            schoolId = distinctSchools.First();
        }

        // Get school info if schoolId is set
        string? schoolName = null;
        string? district = null;
        if (schoolId.HasValue)
        {
            var school = await _coreDb.Schools
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SchoolId == schoolId.Value, ct);
            
            schoolName = school?.Name;
            district = school?.District;
        }
        else
        {
            // Use first device's school info as fallback
            var firstDevice = devices.First();
            schoolName = firstDevice.SchoolName;
            if (firstDevice.SchoolId.HasValue)
            {
                var school = await _coreDb.Schools
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.SchoolId == firstDevice.SchoolId.Value, ct);
                district = school?.District;
            }
        }

        // Generate slip number
        var slipNumber = await GenerateSlipNumberAsync(ct);

        // Create picking slip
        var slip = new PickingSlip
        {
            Id = Guid.NewGuid(),
            SlipNumber = slipNumber,
            Reference = reference,
            SchoolId = schoolId,
            SchoolName = schoolName,
            District = district,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = userId,
            RequestedCollectionDate = requestedCollectionDate,
            Notes = notes,
            Status = PickingSlipStatus.Draft
        };

        _phase2Db.PickingSlips.Add(slip);

        // Get storage locations for all devices
        var storageLocations = await _phase2Db.DeviceStorageLocations
            .Where(s => deviceIds.Contains(s.Phase2DeviceId) && s.Status == "Active")
            .ToListAsync(ct);

        var storageMap = storageLocations
            .GroupBy(s => s.Phase2DeviceId)
            .ToDictionary(g => g.Key, g => g.First());

        // Create picking slip items with snapshots
        foreach (var device in devices)
        {
            var storage = storageMap.GetValueOrDefault(device.Id);
            if (storage == null) continue;

            var item = new PickingSlipItem
            {
                PickingSlipId = slip.Id,
                Phase2DeviceId = device.Id,
                Serial = device.Serial,
                SchoolId = device.SchoolId,
                SchoolName = device.SchoolName ?? schoolName,
                District = district,
                StageAtCreation = device.Stage,
                Building = storage.Building,
                Room = storage.Room,
                Rack = storage.Rack,
                Shelf = storage.Shelf,
                Bin = storage.Bin,
                IsPicked = false
            };

            _phase2Db.PickingSlipItems.Add(item);
        }

        await _phase2Db.SaveChangesAsync(ct);

        // Reload with items
        return await _phase2Db.PickingSlips
            .Include(s => s.Items)
            .ThenInclude(i => i.Phase2Device)
            .FirstOrDefaultAsync(s => s.Id == slip.Id, ct) ?? slip;
    }

    /// <summary>
    /// Get list of picking slips with filters
    /// </summary>
    public async Task<List<PickingSlipDto>> GetPickingSlipsAsync(
        PickingSlipStatus? status = null,
        long? schoolId = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        string? createdBy = null,
        CancellationToken ct = default)
    {
        var query = _phase2Db.PickingSlips.AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(s => s.Status == status.Value);
        }

        if (schoolId.HasValue)
        {
            query = query.Where(s => s.SchoolId == schoolId.Value);
        }

        if (dateFrom.HasValue)
        {
            query = query.Where(s => s.CreatedAt >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(s => s.CreatedAt <= dateTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(createdBy))
        {
            query = query.Where(s => s.CreatedByUserId == createdBy);
        }

        var slips = await query
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

        // Get item counts
        var slipIds = slips.Select(s => s.Id).ToList();
        var itemCounts = await _phase2Db.PickingSlipItems
            .Where(i => slipIds.Contains(i.PickingSlipId))
            .GroupBy(i => i.PickingSlipId)
            .Select(g => new { PickingSlipId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var countMap = itemCounts.ToDictionary(c => c.PickingSlipId, c => c.Count);

        return slips.Select(s => new PickingSlipDto
        {
            Id = s.Id,
            SlipNumber = s.SlipNumber,
            SchoolId = s.SchoolId,
            SchoolName = s.SchoolName,
            District = s.District,
            CreatedAt = s.CreatedAt,
            CreatedByUserId = s.CreatedByUserId,
            RequestedCollectionDate = s.RequestedCollectionDate,
            Notes = s.Notes,
            Status = s.Status.ToString(),
            DeviceCount = countMap.GetValueOrDefault(s.Id, 0)
        }).ToList();
    }

    /// <summary>
    /// Get single picking slip with items for viewing/printing
    /// </summary>
    public async Task<PickingSlipDetailDto?> GetPickingSlipAsync(Guid slipId, CancellationToken ct = default)
    {
        var slip = await _phase2Db.PickingSlips
            .Include(s => s.Items)
            .ThenInclude(i => i.Phase2Device)
            .FirstOrDefaultAsync(s => s.Id == slipId, ct);

        if (slip == null) return null;

        // Look up user information
        string createdByName = "Unknown";
        string createdByRole = "";
        if (!string.IsNullOrEmpty(slip.CreatedByUserId))
        {
            try
            {
                var user = await _userManager.FindByIdAsync(slip.CreatedByUserId);
                if (user != null)
                {
                    createdByName = user.FullName ?? user.UserName ?? user.Email ?? "Unknown";
                    var roles = await _userManager.GetRolesAsync(user);
                    createdByRole = roles.FirstOrDefault() ?? "";
                }
            }
            catch
            {
                // If user lookup fails, use default
            }
        }

        return new PickingSlipDetailDto
        {
            Id = slip.Id,
            SlipNumber = slip.SlipNumber,
            Reference = slip.Reference,
            SchoolId = slip.SchoolId,
            SchoolName = slip.SchoolName,
            District = slip.District,
            CreatedAt = slip.CreatedAt,
            CreatedByUserId = slip.CreatedByUserId,
            CreatedByUserName = createdByName,
            CreatedByUserRole = createdByRole,
            RequestedCollectionDate = slip.RequestedCollectionDate,
            Notes = slip.Notes,
            Status = slip.Status.ToString(),
            Items = slip.Items.OrderBy(i => i.Serial).Select(i => new PickingSlipItemDto
            {
                Id = i.Id,
                Phase2DeviceId = i.Phase2DeviceId,
                Serial = i.Serial,
                SchoolId = i.SchoolId,
                SchoolName = i.SchoolName,
                District = i.District,
                StageAtCreation = i.StageAtCreation.ToString(),
                Building = i.Building,
                Room = i.Room,
                Rack = i.Rack,
                Shelf = i.Shelf,
                Bin = i.Bin,
                IsPicked = i.IsPicked,
                PickedAt = i.PickedAt,
                PickedByUserId = i.PickedByUserId
            }).ToList()
        };
    }

    /// <summary>
    /// Update picking slip status
    /// </summary>
    public async Task<bool> UpdateSlipStatusAsync(Guid slipId, PickingSlipStatus status, CancellationToken ct = default)
    {
        var slip = await _phase2Db.PickingSlips
            .FirstOrDefaultAsync(s => s.Id == slipId, ct);

        if (slip == null) return false;

        slip.Status = status;
        await _phase2Db.SaveChangesAsync(ct);

        return true;
    }
}

// DTOs
public class DeviceForPickingDto
{
    public int Phase2DeviceId { get; set; }
    public string Serial { get; set; } = string.Empty;
    public long? SchoolId { get; set; }
    public string? SchoolName { get; set; }
    public string? District { get; set; }
    public string Stage { get; set; } = string.Empty;
    public string? Model { get; set; }
    public string? Category { get; set; }
    public string? Building { get; set; }
    public string? Room { get; set; }
    public string? Rack { get; set; }
    public string? Shelf { get; set; }
    public string? Bin { get; set; }
    public string? Notes { get; set; }
}

public class PickingSlipDto
{
    public Guid Id { get; set; }
    public string SlipNumber { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public long? SchoolId { get; set; }
    public string? SchoolName { get; set; }
    public string? District { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime? RequestedCollectionDate { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;
    public int DeviceCount { get; set; }
}

public class PickingSlipDetailDto
{
    public Guid Id { get; set; }
    public string SlipNumber { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public long? SchoolId { get; set; }
    public string? SchoolName { get; set; }
    public string? District { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string? CreatedByUserName { get; set; }
    public string? CreatedByUserRole { get; set; }
    public DateTime? RequestedCollectionDate { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<PickingSlipItemDto> Items { get; set; } = new();
}

public class PickingSlipItemDto
{
    public long Id { get; set; }
    public int Phase2DeviceId { get; set; }
    public string Serial { get; set; } = string.Empty;
    public long? SchoolId { get; set; }
    public string? SchoolName { get; set; }
    public string? District { get; set; }
    public string StageAtCreation { get; set; } = string.Empty;
    public string? Building { get; set; }
    public string? Room { get; set; }
    public string? Rack { get; set; }
    public string? Shelf { get; set; }
    public string? Bin { get; set; }
    public bool IsPicked { get; set; }
    public DateTime? PickedAt { get; set; }
    public string? PickedByUserId { get; set; }
}

