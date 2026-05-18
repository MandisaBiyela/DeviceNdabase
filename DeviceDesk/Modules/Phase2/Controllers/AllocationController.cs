using System;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Infrastructure.Data.Enums;
using DeviceDesk.Infrastructure.Identity;
using DeviceDesk.Modules.Phase1.Models;
using DeviceDesk.Modules.Phase2.Data;
using DeviceDesk.Modules.Phase2.Models;
using DeviceDesk.Modules.Phase2.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace DeviceDesk.Modules.Phase2.Controllers;

[ApiController]
[Route("api/phase2/allocation")]
[Authorize(Roles = UserRoles.IctAllocator)]
public class AllocationController : ControllerBase
{
    private readonly Phase2DbContext _phase2Db;
    private readonly DeviceDeskDbContext _coreDb;
    private readonly ILocationService _locationService;
    private readonly AllocationService _allocationService;
    private readonly IPhase2AllocationService _phase2AllocationService;
    private readonly AuditService _audit;
    private readonly UserManager<ApplicationUser> _userManager;

    public AllocationController(
        Phase2DbContext phase2Db,
        DeviceDeskDbContext coreDb,
        ILocationService locationService,
        AllocationService allocationService,
        IPhase2AllocationService phase2AllocationService,
        AuditService audit,
        UserManager<ApplicationUser> userManager)
    {
        _phase2Db = phase2Db;
        _coreDb = coreDb;
        _locationService = locationService;
        _allocationService = allocationService;
        _phase2AllocationService = phase2AllocationService;
        _audit = audit;
        _userManager = userManager;
    }

    [HttpGet("device")]
    [HttpGet("search")]
    public async Task<IActionResult> SearchBySerial([FromQuery] string serial, CancellationToken ct)
    {
        serial = serial?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(serial))
        {
            return BadRequest("Serial is required.");
        }

        var phase2Device = await _phase2Db.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Serial == serial, ct);

        if (phase2Device == null)
        {
            return NotFound("Device not found in Phase 2.");
        }

        var coreDevice = await _coreDb.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.SerialNumber == serial, ct);

        // Get school info - prefer Phase2Device, fallback to core Device
        long? schoolId = phase2Device.SchoolId.HasValue 
            ? (long)phase2Device.SchoolId.Value 
            : (coreDevice?.SchoolId.HasValue == true ? coreDevice.SchoolId.Value : null);

        var school = schoolId.HasValue
            ? await _coreDb.Schools.AsNoTracking().FirstOrDefaultAsync(s => s.SchoolId == schoolId.Value, ct)
            : null;

        // Get school name - prefer Phase2Device, fallback to core Device, then school lookup
        string? schoolName = phase2Device.SchoolName ?? coreDevice?.SchoolName ?? school?.Name;

        // Get model name properly - check if model exists and is not empty
        string? modelName = null;
        if (coreDevice != null)
        {
            // Use Model property if it exists and is not empty
            if (!string.IsNullOrWhiteSpace(coreDevice.Model))
            {
                modelName = coreDevice.Model;
            }
            // Fallback: try to construct from Brand + Model if Model is empty but Brand exists
            else if (!string.IsNullOrWhiteSpace(coreDevice.Brand))
            {
                modelName = coreDevice.Brand;
                if (!string.IsNullOrWhiteSpace(coreDevice.DeviceType))
                {
                    modelName += $" {coreDevice.DeviceType}";
                }
            }
        }

        StorageLocation? currentLocation = null;
        if (coreDevice != null)
        {
            currentLocation = await _coreDb.DeviceLocations
                .Include(dl => dl.StorageLocation)
                .Where(dl => dl.DeviceId == coreDevice.Id && dl.IsCurrent)
                .Select(dl => dl.StorageLocation)
                .AsNoTracking()
                .FirstOrDefaultAsync(ct);
        }

        // Get school's default storage location (if device has a school)
        StorageLocation? schoolStorageLocation = null;
        if (schoolId.HasValue)
        {
            schoolStorageLocation = await _allocationService.GetSchoolStorageLocationAsync(schoolId.Value, ct);
        }

        // Get Phase 2 detailed storage
        var phase2Storage = await _allocationService.GetPhase2StorageAsync(phase2Device.Id, ct);

        // Get suggested allocation if device doesn't have storage yet
        // Only suggest if: no current storage and has school (category is optional now)
        SuggestedAllocationDto? suggestedAllocation = null;
        if (phase2Storage == null && 
            (phase2Device.SchoolId.HasValue || schoolId.HasValue))
        {
            suggestedAllocation = await _allocationService.GetSuggestedAllocationAsync(phase2Device.Id, ct);
        }

        return Ok(new
        {
            phase2Id = phase2Device.Id,
            serial = phase2Device.Serial,
            phase2Stage = phase2Device.Stage.ToString(),
            coreDeviceId = coreDevice?.Id,
            schoolId = schoolId,
            schoolName = schoolName,
            emis = school?.EmisCode,
            category = coreDevice?.Category.ToString(),
            model = modelName ?? "Model Not Available",
            currentLocation = currentLocation == null ? null : new
            {
                id = currentLocation.Id,
                code = currentLocation.LocationCode,
                name = currentLocation.Name,
                area = currentLocation.Area.ToString()
            },
            schoolStorageLocation = schoolStorageLocation == null ? null : new
            {
                id = schoolStorageLocation.Id,
                code = schoolStorageLocation.LocationCode,
                name = schoolStorageLocation.Name,
                area = schoolStorageLocation.Area.ToString(),
                category = schoolStorageLocation.Category.ToString()
            },
            phase2Storage = phase2Storage == null ? null : new
            {
                building = phase2Storage.Building,
                room = phase2Storage.Room,
                rack = phase2Storage.Rack,
                shelf = phase2Storage.Shelf,
                bin = phase2Storage.Bin,
                notes = phase2Storage.Notes,
                createdAt = phase2Storage.CreatedAt
            },
            suggestedAllocation = suggestedAllocation == null ? null : new
            {
                building = suggestedAllocation.Building,
                room = suggestedAllocation.Room,
                rack = suggestedAllocation.Rack,
                shelf = suggestedAllocation.Shelf,
                bin = suggestedAllocation.Bin,
                category = suggestedAllocation.Category.ToString()
            }
        });
    }

    [HttpGet("locations")]
    public async Task<IActionResult> GetLocationsForDevice([FromQuery] Guid deviceId, CancellationToken ct)
    {
        var device = await _coreDb.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == deviceId, ct);

        if (device == null)
        {
            return NotFound("Device not found in core DB.");
        }

        if (device.SchoolId == null)
        {
            return BadRequest("Device has no school assigned.");
        }

        var school = await _coreDb.Schools.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SchoolId == device.SchoolId, ct);

        var locations = await _coreDb.StorageLocations
            .AsNoTracking()
            .Where(x =>
                x.SchoolId == device.SchoolId &&
                x.Category == device.Category &&
                x.Area == StorageArea.Phase2IctCenter &&
                x.IsActive)
            .OrderBy(x => x.LocationCode)
            .Select(x => new { id = x.Id, code = x.LocationCode, name = x.Name })
            .ToListAsync(ct);

        return Ok(new
        {
            deviceId = device.Id,
            schoolName = school?.Name ?? device.SchoolName,
            emis = school?.EmisCode,
            category = device.Category.ToString(),
            locations
        });
    }

    public record MoveRequest(Guid DeviceId, int StorageLocationId, string? Reason);

    [HttpPost("move")]
    public async Task<IActionResult> MoveDevice([FromBody] MoveRequest request, CancellationToken ct)
    {
        if (request.DeviceId == Guid.Empty)
        {
            return BadRequest("DeviceId is required.");
        }

        var coreDevice = await _coreDb.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == request.DeviceId, ct);

        if (coreDevice == null)
        {
            return NotFound("Device not found.");
        }

        // Find Phase2Device to validate stage
        var phase2Device = await _phase2Db.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Serial == coreDevice.SerialNumber, ct);

        if (phase2Device != null)
        {
            // Validate stage - allocation only allowed in certain stages
            var allowedStages = new[] { Phase2Stage.Received, Phase2Stage.PreAssessment, Phase2Stage.DetailedInspection, 
                Phase2Stage.HardwareDept, Phase2Stage.SoftwareDept, Phase2Stage.QualityAssessment };
            var blockedStages = new[] { Phase2Stage.Dispatch, Phase2Stage.AwaitingDispatch, Phase2Stage.Disposal, Phase2Stage.SchoolAllocation };

            if (blockedStages.Contains(phase2Device.Stage))
            {
                return BadRequest($"Device cannot be allocated in its current stage ({phase2Device.Stage}). Device has left ICT centre or is disposed.");
            }
        }

        var location = await _coreDb.StorageLocations
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == request.StorageLocationId && l.IsActive, ct);

        if (location == null)
        {
            return NotFound("Location not found or inactive.");
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        await _locationService.MoveDeviceAsync(
            request.DeviceId,
            request.StorageLocationId,
            request.Reason ?? "ICT manual allocation",
            userId,
            ct);

        return Ok(new { success = true });
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingAllocations(CancellationToken ct)
    {
        var pending = await _allocationService.GetPendingAllocationsAsync(ct);
        return Ok(pending);
    }

    public record BulkAllocationRequest(
        List<string> DeviceSerials,
        string? Building = null,
        string? Room = null,
        string? Rack = null,
        string? Shelf = null,
        string? Bin = null,
        string? Notes = null);

    /// <summary>
    /// Format a pattern string by replacing {n:00} with the number.
    /// Examples: "Rack {n:00}" with n=1 becomes "Rack 01"
    /// </summary>
    private string FormatPattern(string pattern, int number)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return $"Item {number.ToString().PadLeft(2, '0')}";
            
        // Match {n:00} or {n:0} patterns
        var regex = new Regex(@"\{n:(\d+)\}");
        var match = regex.Match(pattern);
        
        if (match.Success)
        {
            var format = match.Groups[1].Value;
            var paddedNumber = number.ToString($"D{format.Length}");
            return regex.Replace(pattern, paddedNumber);
        }
        
        // Fallback: replace {n} with number
        return pattern.Replace("{n}", number.ToString());
    }

    /// <summary>
    /// Generate unique bin locations for bulk allocation.
    /// Devices share Building/Room/Rack/Shelf until full, but each gets unique bin.
    /// Uses school's zone template patterns if provided.
    /// </summary>
    private List<(string Building, string Room, string Rack, string Shelf, string Bin)> 
        GenerateUniqueBinsForBulkAllocation(
            string building, string room, string rack, string shelf,
            int deviceCount,
            HashSet<string> occupiedLocations,
            SchoolStorageTemplate? zoneTemplate = null)
    {
        var locations = new List<(string, string, string, string, string)>();
        
        // Use zone template patterns if available
        string rackPattern = zoneTemplate?.RackPattern ?? "Rack {n:00}";
        string shelfPattern = zoneTemplate?.ShelfPattern ?? "Shelf {n:00}";
        string binPattern = zoneTemplate?.BinPattern ?? "Bin {n:00}";
        int maxRacks = zoneTemplate?.MaxRacks ?? 10;
        int maxShelvesPerRack = zoneTemplate?.MaxShelvesPerRack ?? 10;
        int maxBinsPerShelf = zoneTemplate?.MaxBinsPerShelf ?? 10;
        
        // Parse starting rack, shelf and bin numbers from suggested location
        int currentRack = 1;
        int currentShelf = 1;
        int currentBin = 1;
        
        // Try to parse rack number
        if (!string.IsNullOrWhiteSpace(rack))
        {
            var rackStr = rack.Replace("Rack", "").Replace("rack", "").Replace("RACK", "").Trim();
            if (int.TryParse(rackStr, out var rackNum))
                currentRack = rackNum;
        }
        
        // Try to parse shelf number
        if (!string.IsNullOrWhiteSpace(shelf))
        {
            var shelfStr = shelf.Replace("Shelf", "").Replace("shelf", "").Replace("SHELF", "").Trim();
            if (int.TryParse(shelfStr, out var shelfNum))
                currentShelf = shelfNum;
        }
        
        // Find next available starting bin for current shelf/rack
        // Check existing bins in this shelf to find the highest
        int maxBinInShelf = 0;
        string currentRackFormatted = FormatPattern(rackPattern, currentRack);
        string currentShelfFormatted = FormatPattern(shelfPattern, currentShelf);
        var shelfPrefix = $"{building}|{room}|{currentRackFormatted}|{currentShelfFormatted}|";
        
        foreach (var occupied in occupiedLocations)
        {
            if (occupied.StartsWith(shelfPrefix))
            {
                var parts = occupied.Split('|');
                if (parts.Length > 4 && !string.IsNullOrWhiteSpace(parts[4]))
                {
                    var binStr = parts[4];
                    // Try to extract number from bin string (handle "Bin 01" or "01")
                    binStr = binStr.Replace("Bin", "").Replace("bin", "").Replace("BIN", "").Trim();
                    if (int.TryParse(binStr, out var binNum))
                    {
                        maxBinInShelf = Math.Max(maxBinInShelf, binNum);
                    }
                }
            }
        }
        currentBin = maxBinInShelf + 1;

        for (int i = 0; i < deviceCount; i++)
        {
            // Try to find next available bin
            int attempts = 0;
            bool found = false;
            int maxAttempts = maxBinsPerShelf * maxShelvesPerRack * maxRacks;
            
            while (!found && attempts < maxAttempts)
            {
                // Format using zone template patterns
                string rackFormatted = FormatPattern(rackPattern, currentRack);
                string shelfFormatted = FormatPattern(shelfPattern, currentShelf);
                string binFormatted = FormatPattern(binPattern, currentBin);
                
                var locationKey = $"{building}|{room}|{rackFormatted}|{shelfFormatted}|{binFormatted}";
                
                if (!occupiedLocations.Contains(locationKey))
                {
                    locations.Add((
                        building, 
                        room, 
                        rackFormatted, 
                        shelfFormatted, 
                        binFormatted
                    ));
                    occupiedLocations.Add(locationKey); // Mark as occupied for next iteration
                    found = true;
                }
                
                // Move to next bin
                currentBin++;
                if (currentBin > maxBinsPerShelf)
                {
                    currentBin = 1;
                    currentShelf++;
                }
                if (currentShelf > maxShelvesPerRack)
                {
                    currentShelf = 1;
                    currentRack++;
                }
                if (currentRack > maxRacks)
                {
                    throw new InvalidOperationException(
                        $"Not enough available storage space. Found {locations.Count} available bins for {deviceCount} devices. " +
                        $"Reached maximum racks ({maxRacks}) for this school's zone template.");
                }
                attempts++;
            }
            
            if (!found)
            {
                throw new InvalidOperationException(
                    $"Could not find enough unique bins. Found {locations.Count} available for {deviceCount} devices.");
            }
        }

        return locations;
    }

    [HttpPost("bulk-allocate")]
    public async Task<IActionResult> BulkAllocate([FromBody] BulkAllocationRequest request, CancellationToken ct)
    {
        if (request.DeviceSerials == null || request.DeviceSerials.Count == 0)
        {
            return BadRequest(new { success = false, message = "At least one device serial is required" });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "unknown";

        try
        {
            // Validate all devices belong to the same school
            var deviceSchools = await _phase2Db.Devices
                .Where(d => request.DeviceSerials.Contains(d.Serial))
                .Select(d => new { d.Serial, d.SchoolId })
                .ToListAsync(ct);

            if (deviceSchools.Count != request.DeviceSerials.Count)
            {
                return BadRequest(new { success = false, message = "One or more devices not found in Phase 2" });
            }

            var distinctSchools = deviceSchools
                .Where(d => d.SchoolId.HasValue)
                .Select(d => d.SchoolId!.Value)
                .Distinct()
                .ToList();

            if (distinctSchools.Count == 0)
            {
                return BadRequest(new { success = false, message = "All devices must have a school assigned" });
            }

            if (distinctSchools.Count > 1)
            {
                return BadRequest(new { success = false, message = "All devices must be from the same school" });
            }

            var schoolId = distinctSchools.First();
            var school = await _coreDb.Schools
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SchoolId == schoolId, ct);

            // Get all devices upfront for validation and suggested allocation
            var devices = await _phase2Db.Devices
                .Where(d => request.DeviceSerials.Contains(d.Serial))
                .ToListAsync(ct);

            if (devices.Count == 0)
            {
                return BadRequest(new { success = false, message = "No devices found" });
            }

            // Get suggested allocation from first device to determine base location
            var firstDevice = devices.First();
            var suggestedAllocation = await _allocationService.GetSuggestedAllocationAsync(firstDevice.Id, ct);
            
            // Get device category to find the correct zone template
            var coreDevice = await _coreDb.Devices
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.SerialNumber == firstDevice.Serial, ct);
            var deviceCategory = coreDevice?.Category ?? DeviceCategory.Unknown;
            
            // Get school's zone template for this category (or any active template if category unknown)
            var zoneTemplate = await _phase2Db.SchoolStorageTemplates
                .AsNoTracking()
                .FirstOrDefaultAsync(t => 
                    t.SchoolId == schoolId && 
                    (deviceCategory == DeviceCategory.Unknown || t.Category == deviceCategory) && 
                    t.IsActive, ct);
            
            // If no category-specific template, try any active template for the school
            if (zoneTemplate == null)
            {
                zoneTemplate = await _phase2Db.SchoolStorageTemplates
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.SchoolId == schoolId && t.IsActive, ct);
            }
            
            // Determine base location - use provided values, suggested allocation, or template defaults
            string building = request.Building ?? suggestedAllocation?.Building ?? zoneTemplate?.Building ?? "ICT Centre Main";
            string room = request.Room ?? suggestedAllocation?.Room ?? zoneTemplate?.Room ?? "Room 1";
            string rack = request.Rack ?? suggestedAllocation?.Rack ?? (zoneTemplate != null ? FormatPattern(zoneTemplate.RackPattern, 1) : "Rack 01");
            string shelf = request.Shelf ?? suggestedAllocation?.Shelf ?? (zoneTemplate != null ? FormatPattern(zoneTemplate.ShelfPattern, 1) : "Shelf 01");

            // Get all occupied locations for this school in this building/room to avoid conflicts
            // Check across all racks, not just one
            var occupiedLocations = await _phase2Db.DeviceStorageLocations
                .Where(s => s.Status == "Active" && 
                           s.Building == building && 
                           s.Room == room &&
                           s.Phase2Device != null &&
                           s.Phase2Device.SchoolId == schoolId)
                .Select(s => new { s.Building, s.Room, s.Rack, s.Shelf, s.Bin })
                .ToListAsync(ct);

            var occupiedSet = new HashSet<string>();
            foreach (var loc in occupiedLocations)
            {
                if (!string.IsNullOrWhiteSpace(loc.Bin) && !string.IsNullOrWhiteSpace(loc.Shelf))
                {
                    var key = $"{loc.Building}|{loc.Room}|{loc.Rack}|{loc.Shelf}|{loc.Bin}";
                    occupiedSet.Add(key);
                }
            }

            // Generate unique locations for each device using zone template patterns
            var uniqueLocations = GenerateUniqueBinsForBulkAllocation(
                building, room, rack, shelf,
                request.DeviceSerials.Count,
                occupiedSet,
                zoneTemplate);

            // Get or create bulk allocation session
            var bulkSession = new BulkAllocationSession
            {
                Id = Guid.NewGuid(),
                SchoolId = schoolId,
                SchoolName = school?.Name ?? "Unknown School",
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                Status = BulkAllocationStatus.InProgress,
                DeviceCount = request.DeviceSerials.Count
            };

            _phase2Db.BulkAllocationSessions.Add(bulkSession);

            // Create individual allocations
            var allocations = new List<DeviceStorageLocation>();
            var failedDevices = new List<string>();

            for (int i = 0; i < request.DeviceSerials.Count; i++)
            {
                var serial = request.DeviceSerials[i];
                var uniqueLoc = uniqueLocations[i];

                try
                {
                    var phase2Device = devices.FirstOrDefault(d => d.Serial == serial);
                    if (phase2Device == null)
                    {
                        failedDevices.Add(serial);
                        continue;
                    }

                    // Use existing allocation service method with unique location
                    var storage = await _allocationService.AllocatePhase2StorageAsync(
                        phase2Device.Id,
                        null, // storageLocationId
                        uniqueLoc.Building,
                        uniqueLoc.Room,
                        uniqueLoc.Rack,
                        uniqueLoc.Shelf,
                        uniqueLoc.Bin,
                        request.Notes ?? $"Auto-allocated via bulk allocation (device {i + 1}/{request.DeviceSerials.Count})",
                        userId,
                        ct);

                    // Link to bulk session
                    storage.BulkSessionId = bulkSession.Id;
                    allocations.Add(storage);
                }
                catch (Exception)
                {
                    failedDevices.Add(serial);
                    // Log error but continue with other devices
                }
            }

            if (allocations.Count == 0)
            {
                _phase2Db.BulkAllocationSessions.Remove(bulkSession);
                await _phase2Db.SaveChangesAsync(ct);
                return BadRequest(new { success = false, message = "Failed to allocate any devices", failedDevices });
            }

            // Update session status and device count
            bulkSession.DeviceCount = allocations.Count;
            bulkSession.Status = BulkAllocationStatus.Completed;

            await _phase2Db.SaveChangesAsync(ct);

            return Ok(new
            {
                success = true,
                message = $"Allocated {allocations.Count} device(s) to {school?.Name ?? "Unknown School"}",
                bulkSessionId = bulkSession.Id,
                allocatedCount = allocations.Count,
                failedCount = failedDevices.Count,
                failedDevices = failedDevices.Count > 0 ? failedDevices : null
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpGet("device-details/{serial}")]
    public async Task<IActionResult> GetDeviceDetails(string serial, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(serial))
        {
            return BadRequest(new { success = false, message = "Serial is required" });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "unknown";

        try
        {
            // Get device from Phase2
            var phase2Device = await _phase2Db.Devices
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Serial == serial, ct);

            if (phase2Device == null)
            {
                return NotFound(new { success = false, message = "Device not found" });
            }

            // Get core device for model/category
            var coreDevice = await _coreDb.Devices
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.SerialNumber == serial, ct);

            // Get school info
            var schoolId = phase2Device.SchoolId ?? coreDevice?.SchoolId;
            var school = schoolId.HasValue
                ? await _coreDb.Schools.AsNoTracking().FirstOrDefaultAsync(s => s.SchoolId == schoolId.Value, ct)
                : null;

            // Get allocation history
            var allocations = await _phase2Db.DeviceStorageLocations
                .Where(a => a.Phase2DeviceId == phase2Device.Id)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync(ct);

            var currentAllocation = allocations.FirstOrDefault(a => a.Status == "Active");
            var firstAllocation = allocations.LastOrDefault();

            // Get scan history (last 10)
            var scanHistory = await _phase2Db.DeviceScans
                .Where(s => s.DeviceSerial == serial)
                .OrderByDescending(s => s.ScanTime)
                .Take(10)
                .ToListAsync(ct);

            // Log this scan
            var newScan = new DeviceScan
            {
                Id = Guid.NewGuid(),
                DeviceSerial = serial,
                ScanTime = DateTime.UtcNow,
                ScannedBy = userId,
                Location = "Device Details Page",
                Purpose = "Details View"
            };

            _phase2Db.DeviceScans.Add(newScan);

            // Also create audit log entry
            await _audit.LogAsync(userId, "DeviceScan", phase2Device.Id, serial, 
                "Device scanned from Device Details Page");

            await _phase2Db.SaveChangesAsync(ct);

            // Get model name
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

            // Helper function to get user display name
            async Task<string> GetUserDisplayName(string? userId)
            {
                if (string.IsNullOrWhiteSpace(userId)) return "Unknown";
                
                try
                {
                    var user = await _userManager.FindByIdAsync(userId);
                    if (user != null)
                    {
                        // Try FullName first, then UserName, then Email
                        if (!string.IsNullOrWhiteSpace(user.FullName))
                            return user.FullName;
                        if (!string.IsNullOrWhiteSpace(user.UserName))
                            return user.UserName;
                        if (!string.IsNullOrWhiteSpace(user.Email))
                            return user.Email;
                    }
                    
                    // If user not found, try to get role from claims or return a default
                    return "ICT Allocator"; // Default fallback
                }
                catch
                {
                    return "Unknown";
                }
            }

            // Get user display names for allocations
            var currentAllocatedBy = currentAllocation != null 
                ? await GetUserDisplayName(currentAllocation.CreatedByUserId) 
                : null;
            var firstAllocatedBy = firstAllocation != null 
                ? await GetUserDisplayName(firstAllocation.CreatedByUserId) 
                : null;

            return Ok(new
            {
                success = true,
                phase2Id = phase2Device.Id,
                device = new
                {
                    id = phase2Device.Id,
                    serial = phase2Device.Serial,
                    model = modelName,
                    category = coreDevice?.Category.ToString() ?? "Unknown",
                    school = new
                    {
                        id = schoolId,
                        name = school?.Name ?? phase2Device.SchoolName ?? "Not assigned"
                    },
                    currentAllocation = currentAllocation != null ? new
                    {
                        building = currentAllocation.Building,
                        room = currentAllocation.Room,
                        rack = currentAllocation.Rack,
                        shelf = currentAllocation.Shelf,
                        bin = currentAllocation.Bin,
                        allocatedAt = currentAllocation.CreatedAt,
                        allocatedBy = currentAllocatedBy
                    } : null,
                    firstAllocation = firstAllocation != null ? new
                    {
                        allocatedAt = firstAllocation.CreatedAt,
                        allocatedBy = firstAllocatedBy
                    } : null,
                    allocationCount = allocations.Count,
                    lastScanned = scanHistory.FirstOrDefault()?.ScanTime,
                    scanHistory = scanHistory.Select(s => new
                    {
                        scanTime = s.ScanTime,
                        scannedBy = s.ScannedBy,
                        location = s.Location,
                        purpose = s.Purpose
                    })
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpGet("device-history/{serial}")]
    public async Task<IActionResult> GetDeviceHistory(string serial, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(serial))
        {
            return BadRequest(new { success = false, message = "Serial is required" });
        }

        try
        {
            // Get device from Phase2
            var phase2Device = await _phase2Db.Devices
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Serial == serial, ct);

            if (phase2Device == null)
            {
                return NotFound(new { success = false, message = "Device not found" });
            }

            // Get core device for location history
            var coreDevice = await _coreDb.Devices
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.SerialNumber == serial, ct);

            // Get all scan history (no limit)
            var scanHistory = await _phase2Db.DeviceScans
                .Where(s => s.DeviceSerial == serial)
                .OrderByDescending(s => s.ScanTime)
                .ToListAsync(ct);

            // Get allocation history
            var allocations = await _phase2Db.DeviceStorageLocations
                .Where(a => a.Phase2DeviceId == phase2Device.Id)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync(ct);

            // Get audit logs for this device
            var auditLogs = await _phase2Db.AuditLogs
                .Where(a => (a.DeviceId == phase2Device.Id || a.DeviceSerial == serial))
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync(ct);

            // Get location history from core DB if device exists
            List<DeviceLocationHistory> locationHistory = new();
            if (coreDevice != null)
            {
                locationHistory = await _coreDb.DeviceLocationHistory
                    .Include(h => h.FromLocation)
                    .Include(h => h.ToLocation)
                    .Where(h => h.DeviceId == coreDevice.Id)
                    .OrderByDescending(h => h.Timestamp)
                    .ToListAsync(ct);
            }

            // Helper record for history items
            var historyItems = new List<(DateTime timestamp, object item)>();

            // Add scans
            foreach (var scan in scanHistory)
            {
                historyItems.Add((scan.ScanTime, new
                {
                    type = "scan",
                    timestamp = scan.ScanTime,
                    title = scan.Purpose,
                    description = $"Scanned at {scan.Location}",
                    user = scan.ScannedBy,
                    icon = "bi-search",
                    color = "info"
                }));
            }

            // Add allocations
            foreach (var alloc in allocations)
            {
                var locationParts = new[] { alloc.Building, alloc.Room, alloc.Rack, alloc.Shelf, alloc.Bin }
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();
                var locationStr = locationParts.Any() ? string.Join(" / ", locationParts) : "Unknown location";

                historyItems.Add((alloc.CreatedAt.DateTime, new
                {
                    type = alloc.Status == "Active" ? "allocation" : "allocation-updated",
                    timestamp = alloc.CreatedAt,
                    title = alloc.Status == "Active" ? "Device Allocated" : "Allocation Updated",
                    description = $"Location: {locationStr}",
                    user = alloc.CreatedByUserId,
                    icon = "bi-geo-alt",
                    color = alloc.Status == "Active" ? "success" : "warning",
                    location = locationStr
                }));
            }

            // Add audit logs
            foreach (var audit in auditLogs)
            {
                if (audit.Action != "DeviceScan") // Skip duplicate scan entries
                {
                    historyItems.Add((audit.Timestamp, new
                    {
                        type = "audit",
                        timestamp = audit.Timestamp,
                        title = audit.Action,
                        description = audit.Details ?? "",
                        user = audit.UserId,
                        icon = "bi-journal-text",
                        color = "secondary"
                    }));
                }
            }

            // Add location movements
            foreach (var loc in locationHistory)
            {
                var fromLocation = loc.FromLocation != null ? loc.FromLocation.Name : "Unknown";
                var toLocation = loc.ToLocation?.Name ?? "Unknown";

                historyItems.Add((loc.Timestamp.UtcDateTime, new
                {
                    type = "location-move",
                    timestamp = loc.Timestamp.UtcDateTime,
                    title = "Location Changed",
                    description = $"Moved from {fromLocation} to {toLocation}",
                    user = loc.MovedByUserId,
                    icon = "bi-arrow-right-circle",
                    color = "primary",
                    reason = loc.Reason
                }));
            }

            // Sort by timestamp (most recent first) and extract items
            var sortedHistory = historyItems
                .OrderByDescending(h => h.timestamp)
                .Select(h => h.item)
                .ToList();

            return Ok(new
            {
                success = true,
                deviceSerial = serial,
                totalItems = sortedHistory.Count,
                history = sortedHistory
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    public record AllocateDetailedRequest(
        int Phase2DeviceId,
        int? StorageLocationId,
        string? Building,
        string? Room,
        string? Rack,
        string? Shelf,
        string? Bin,
        string? Notes);

    [HttpPost("allocate-detailed")]
    public async Task<IActionResult> AllocateDetailed([FromBody] AllocateDetailedRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

        try
        {
            var storage = await _allocationService.AllocatePhase2StorageAsync(
                request.Phase2DeviceId,
                request.StorageLocationId,
                request.Building,
                request.Room,
                request.Rack,
                request.Shelf,
                request.Bin,
                request.Notes,
                userId,
                ct);

            return Ok(new
            {
                success = true,
                message = "Storage allocated successfully.",
                storage = new
                {
                    id = storage.Id,
                    building = storage.Building,
                    room = storage.Room,
                    rack = storage.Rack,
                    shelf = storage.Shelf,
                    bin = storage.Bin,
                    notes = storage.Notes,
                    createdAt = storage.CreatedAt
                }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("clear")]
    public Task<IActionResult> ClearAllocation([FromQuery] int phase2DeviceId, CancellationToken ct)
    {
        // Prevent allocators from clearing storage - only admins/managers should be able to do this
        // For now, we'll completely disable this endpoint for allocators
        return Task.FromResult<IActionResult>(Forbid("Storage allocations cannot be cleared. Once a device is allocated storage, the allocation cannot be changed. Please contact an administrator if you need to modify a storage allocation."));
        
        /* DISABLED - Allocators cannot clear storage
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

        try
        {
            await _allocationService.ClearAllocationAsync(phase2DeviceId, userId, ct);
            return Ok(new { success = true, message = "Storage allocation cleared." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        */
    }

    [HttpGet("storage-overview")]
    public async Task<IActionResult> GetStorageOverview(CancellationToken ct)
    {
        var overview = await _allocationService.GetStorageOverviewAsync(ct);
        return Ok(overview);
    }

    [HttpGet("unallocated")]
    public async Task<IActionResult> GetUnallocatedDevices(CancellationToken ct)
    {
        var devices = await _allocationService.GetUnallocatedDevicesAsync(ct);
        return Ok(devices);
    }

    [HttpGet("schools-in-storage")]
    public async Task<IActionResult> GetSchoolsInStorage(CancellationToken ct)
    {
        var schools = await _allocationService.GetSchoolsInStorageAsync(ct);
        return Ok(schools);
    }

    [HttpGet("schools-in-storage/{schoolId}/devices")]
    public async Task<IActionResult> GetSchoolDevicesInStorage(long schoolId, CancellationToken ct)
    {
        var devices = await _allocationService.GetSchoolDevicesInStorageAsync(schoolId, ct);
        return Ok(devices);
    }

    [HttpGet("occupied-locations")]
    public async Task<IActionResult> GetOccupiedLocations(CancellationToken ct)
    {
        var occupied = await _phase2Db.DeviceStorageLocations
            .AsNoTracking()
            .Where(s => s.Status == "Active")
            .Select(s => new
            {
                building = s.Building ?? "",
                room = s.Room ?? "",
                rack = s.Rack ?? "",
                shelf = s.Shelf ?? "",
                bin = s.Bin ?? ""
            })
            .Where(s => !string.IsNullOrEmpty(s.building) && 
                       !string.IsNullOrEmpty(s.room) && 
                       !string.IsNullOrEmpty(s.rack) && 
                       !string.IsNullOrEmpty(s.shelf) && 
                       !string.IsNullOrEmpty(s.bin))
            .Distinct()
            .ToListAsync(ct);
        
        return Ok(occupied);
    }

    [HttpGet("suggested-allocation")]
    public async Task<IActionResult> GetSuggestedAllocation([FromQuery] int phase2DeviceId, CancellationToken ct)
    {
        var suggested = await _allocationService.GetSuggestedAllocationAsync(phase2DeviceId, ct);
        if (suggested == null)
        {
            return NotFound("No suggested allocation available. Device may not have a school or category assigned.");
        }

        return Ok(new
        {
            building = suggested.Building,
            room = suggested.Room,
            rack = suggested.Rack,
            shelf = suggested.Shelf,
            bin = suggested.Bin,
            category = suggested.Category.ToString()
        });
    }

    /// <summary>
    /// Diagnostic endpoint to debug device model data
    /// </summary>
    [HttpGet("debug-device/{serial}")]
    public async Task<IActionResult> DebugDeviceModel(string serial, CancellationToken ct)
    {
        serial = serial?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(serial))
        {
            return BadRequest("Serial is required.");
        }

        var phase2Device = await _phase2Db.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Serial == serial, ct);

        var coreDevice = await _coreDb.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.SerialNumber == serial, ct);

        if (coreDevice == null && phase2Device == null)
        {
            return NotFound($"Device with serial {serial} not found in either Phase 2 or core DB");
        }

        return Ok(new
        {
            serial = serial,
            phase2Device = phase2Device != null ? new
            {
                id = phase2Device.Id,
                serial = phase2Device.Serial,
                stage = phase2Device.Stage.ToString(),
                schoolId = phase2Device.SchoolId,
                schoolName = phase2Device.SchoolName
            } : null,
            coreDevice = coreDevice != null ? new
            {
                id = coreDevice.Id,
                serialNumber = coreDevice.SerialNumber,
                imei = coreDevice.IMEI,
                brand = coreDevice.Brand,
                model = coreDevice.Model,
                deviceType = coreDevice.DeviceType,
                hasModel = !string.IsNullOrWhiteSpace(coreDevice.Model),
                hasBrand = !string.IsNullOrWhiteSpace(coreDevice.Brand),
                hasDeviceType = !string.IsNullOrWhiteSpace(coreDevice.DeviceType),
                category = coreDevice.Category.ToString(),
                schoolId = coreDevice.SchoolId,
                schoolName = coreDevice.SchoolName,
                source = coreDevice.Source,
                importedAt = coreDevice.ImportedAt
            } : null,
            modelAnalysis = new
            {
                modelFromCoreDevice = coreDevice?.Model,
                modelIsEmpty = string.IsNullOrWhiteSpace(coreDevice?.Model),
                fallbackModel = !string.IsNullOrWhiteSpace(coreDevice?.Model) 
                    ? coreDevice.Model 
                    : (!string.IsNullOrWhiteSpace(coreDevice?.Brand) 
                        ? $"{coreDevice?.Brand} {coreDevice?.DeviceType}".Trim() 
                        : "Model Not Available")
            }
        });
    }

    public record BatchAllocateRequest(int[] Phase2DeviceIds);

    [HttpPost("batch-allocate")]
    public async Task<IActionResult> BatchAllocate([FromBody] BatchAllocateRequest request, CancellationToken ct)
    {
        if (request.Phase2DeviceIds == null || request.Phase2DeviceIds.Length == 0)
        {
            return BadRequest("At least one device ID is required.");
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        var results = new List<BatchAllocationResult>();
        var errors = new List<string>();

        // Get all devices and their suggested allocations
        var devices = await _phase2Db.Devices
            .AsNoTracking()
            .Where(d => request.Phase2DeviceIds.Contains(d.Id))
            .ToListAsync(ct);

        // Get core devices for category lookup
        var serials = devices.Select(d => d.Serial).ToList();
        var coreDevices = await _coreDb.Devices
            .AsNoTracking()
            .Where(d => d.SerialNumber != null && serials.Contains(d.SerialNumber))
            .ToDictionaryAsync(d => d.SerialNumber!, d => d, ct);

        foreach (var device in devices)
        {
            try
            {
                // Get suggested allocation
                var suggested = await _allocationService.GetSuggestedAllocationAsync(device.Id, ct);
                if (suggested == null)
                {
                    errors.Add($"Device {device.Serial}: No suggested allocation available (missing school or category)");
                    continue;
                }

                // Allocate storage
                var storage = await _allocationService.AllocatePhase2StorageAsync(
                    device.Id,
                    null,
                    suggested.Building,
                    suggested.Room,
                    suggested.Rack,
                    suggested.Shelf,
                    suggested.Bin,
                    $"Auto-allocated via batch allocation",
                    userId,
                    ct);

                results.Add(new BatchAllocationResult
                {
                    Phase2DeviceId = device.Id,
                    Serial = device.Serial,
                    Success = true,
                    Building = storage.Building,
                    Room = storage.Room,
                    Rack = storage.Rack,
                    Shelf = storage.Shelf,
                    Bin = storage.Bin
                });
            }
            catch (Exception ex)
            {
                errors.Add($"Device {device.Serial}: {ex.Message}");
                results.Add(new BatchAllocationResult
                {
                    Phase2DeviceId = device.Id,
                    Serial = device.Serial,
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        return Ok(new
        {
            success = errors.Count == 0,
            total = request.Phase2DeviceIds.Length,
            succeeded = results.Count(r => r.Success),
            failed = results.Count(r => !r.Success),
            results,
            errors = errors.Count > 0 ? errors : null
        });
    }

    private class BatchAllocationResult
    {
        public int Phase2DeviceId { get; set; }
        public string Serial { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? Building { get; set; }
        public string? Room { get; set; }
        public string? Rack { get; set; }
        public string? Shelf { get; set; }
        public string? Bin { get; set; }
        public string? Error { get; set; }
    }

    #region Student/Teacher Allocation Endpoints

    /// <summary>
    /// Get receipted devices ready for student/teacher allocation.
    /// Shows ALL devices from Received stage onwards, including those already allocated or in storage.
    /// Allows viewing and modifying allocations for devices throughout the workflow.
    /// Supports pagination for better performance with large datasets.
    /// </summary>
    [HttpGet("ready-for-assignment")]
    [Authorize(Roles = $"{UserRoles.IctAllocator},{UserRoles.IctManager},{UserRoles.Admin}")]
    public async Task<IActionResult> GetReadyForAssignment(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        try
        {
            // Show devices right after receipting through entire workflow until dispatch
            var readyStages = new[]
            {
                Phase2Stage.Received,           // Right after receipting ← PRIMARY
                Phase2Stage.PreAssessment,      // During pre-assessment
                Phase2Stage.DetailedInspection, // During inspection
                Phase2Stage.HardwareDept,       // In hardware repair
                Phase2Stage.SoftwareDept,       // In software repair
                Phase2Stage.QualityAssessment,  // At QA
                Phase2Stage.AwaitingDispatch    // Waiting dispatch
            };

            // Validate pagination parameters
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 100;
            if (pageSize > 500) pageSize = 500; // Max limit to prevent abuse
            
            var skip = (page - 1) * pageSize;

            // Build base query with stage, disposal, and school filters
            var query = _phase2Db.Devices
                .Where(p2 => readyStages.Contains(p2.Stage) 
                      && (p2.DisposalRequested == null || p2.DisposalRequested == false)
                      && p2.SchoolId != null); // Only show devices with schools assigned

            // Add server-side search filtering if search term provided
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchTerm = search.Trim();
                query = query.Where(p2 => 
                    p2.Serial.Contains(searchTerm) || 
                    (p2.SchoolName != null && p2.SchoolName.Contains(searchTerm)));
            }

            // Step 1: Get total count for pagination info
            var totalCount = await query.CountAsync(ct);

            // Step 2: Get Phase2 devices that are in the right stages (paginated)
            // Order by newest first (ReceivingDate DESC), then by school and serial for consistency
            var phase2Devices = await query
                .OrderByDescending(p2 => p2.ReceivingDate ?? p2.CreatedAt)
                .ThenBy(p2 => p2.SchoolName)
                .ThenBy(p2 => p2.Serial)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(ct);

            // Step 3: Get devices with active storage locations (these should be prioritized)
            var devicesInStorageList = await _phase2Db.DeviceStorageLocations
                .Where(x => x.Status == "Active")
                .Select(x => x.Phase2DeviceId)
                .ToListAsync(ct);
            var devicesInStorage = new HashSet<int>(devicesInStorageList);

            // Step 4: Get serials to look up in core Devices
            var serials = phase2Devices.Select(p => p.Serial).ToList();
            
            // Step 5: Get allocation info from core Devices first
            // With pagination, we now only query 100 devices max, so no need for batching
            var coreDevicesList = await _coreDb.Devices
                .Where(cd => cd.SerialNumber != null && serials.Contains(cd.SerialNumber))
                .Select(cd => new {
                    cd.SerialNumber,
                    cd.SchoolId,
                    cd.SchoolName,
                    cd.AllocationType,
                    cd.StudentName,
                    cd.StudentIdNumber,
                    cd.TeacherName,
                    cd.TeacherPersalNumber
                })
                .ToListAsync(ct);
            
            // Step 6: Collect ALL school IDs from both Phase2 and Core devices for Schools table lookup
            var schoolIds = new List<long>();
            
            // Add SchoolIds from Phase2Devices
            schoolIds.AddRange(phase2Devices
                .Where(p => p.SchoolId.HasValue)
                .Select(p => (long)p.SchoolId!.Value));
            
            // Add SchoolIds from CoreDevices
            schoolIds.AddRange(coreDevicesList
                .Where(c => c.SchoolId.HasValue)
                .Select(c => c.SchoolId!.Value));
            
            schoolIds = schoolIds.Distinct().ToList();
            
            // Query Schools table for all collected SchoolIds
            var schoolsDict = await _coreDb.Schools
                .Where(s => schoolIds.Contains(s.SchoolId))
                .ToDictionaryAsync(s => (int)s.SchoolId, s => s.Name, ct);
            
            var coreDevicesDict = coreDevicesList
                .Where(cd => cd.SerialNumber != null)
                .ToDictionary(cd => cd.SerialNumber!, cd => (cd.SchoolId, cd.SchoolName, cd.AllocationType, cd.StudentName, 
                    cd.StudentIdNumber, cd.TeacherName, cd.TeacherPersalNumber));

            // Step 7: LEFT JOIN in memory - include Phase2 devices even without core Device match
            var items = phase2Devices.Select(p2 =>
            {
                // Try to get core device info, but don't require it
                var hasCoreDevice = coreDevicesDict.TryGetValue(p2.Serial, out var cd);
                
                // Determine school info with proper fallback chain:
                // 1. Try Phase2Device.SchoolId -> Schools table
                // 2. Fall back to Phase2Device.SchoolName (if populated)
                // 3. Fall back to CoreDevice.SchoolId -> Schools table
                // 4. Fall back to CoreDevice.SchoolName (if populated)
                int? finalSchoolId = p2.SchoolId;
                string? finalSchoolName = null;
                
                // First priority: lookup school name from Schools table using Phase2Device.SchoolId
                if (p2.SchoolId.HasValue && schoolsDict.TryGetValue(p2.SchoolId.Value, out var schoolName))
                {
                    finalSchoolName = schoolName;
                }
                // Second priority: use Phase2Device.SchoolName if populated
                else if (!string.IsNullOrWhiteSpace(p2.SchoolName))
                {
                    finalSchoolName = p2.SchoolName;
                }
                // Third priority: if core device has different SchoolId, lookup from Schools table
                else if (hasCoreDevice && cd.SchoolId.HasValue && schoolsDict.TryGetValue((int)cd.SchoolId.Value, out var coreSchoolName))
                {
                    finalSchoolName = coreSchoolName;
                    if (!finalSchoolId.HasValue)
                    {
                        finalSchoolId = (int)cd.SchoolId.Value;
                    }
                }
                // Fourth priority: use CoreDevice.SchoolName if populated
                else if (hasCoreDevice && !string.IsNullOrWhiteSpace(cd.SchoolName))
                {
                    finalSchoolName = cd.SchoolName;
                    if (!finalSchoolId.HasValue && cd.SchoolId.HasValue)
                    {
                        finalSchoolId = (int)cd.SchoolId.Value;
                    }
                }
                
                return new Phase2AllocationListItemDto
                {
                    Phase2DeviceId = p2.Id,
                    Serial = p2.Serial,
                    Zone = p2.Zone,
                    Stage = p2.Stage,
                    SchoolId = finalSchoolId,
                    SchoolName = finalSchoolName,
                    QaPassed = p2.QaPassed,
                    IsInStorage = devicesInStorage.Contains(p2.Id),
                    // Use core device allocation info if available, otherwise default to None
                    AllocationType = hasCoreDevice ? cd.AllocationType : AllocationType.None,
                    StudentName = hasCoreDevice ? cd.StudentName : null,
                    StudentIdNumber = hasCoreDevice ? cd.StudentIdNumber : null,
                    TeacherName = hasCoreDevice ? cd.TeacherName : null,
                    TeacherPersalNumber = hasCoreDevice ? cd.TeacherPersalNumber : null
                };
            }).ToList();

            // Return paginated result with metadata
            var result = new
            {
                items,
                page,
                pageSize,
                totalCount,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to get ready devices", details = ex.Message });
        }
    }

    /// <summary>
    /// Assign student/teacher allocation for a single Phase 2 device.
    /// </summary>
    [HttpPost("devices/{phase2DeviceId:int}/assign")]
    [Authorize(Roles = $"{UserRoles.IctAllocator},{UserRoles.IctManager},{UserRoles.Admin}")]
    public async Task<IActionResult> AssignStudentTeacher(
        int phase2DeviceId,
        [FromBody] DeviceAllocationDto dto,
        CancellationToken ct)
    {
        try
        {
            var userId = User?.Identity?.Name ?? "system";
            await _phase2AllocationService.AssignStudentTeacherAsync(phase2DeviceId, dto, userId, ct);
            return Ok(new { success = true, message = "Allocation saved successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    #endregion
}

