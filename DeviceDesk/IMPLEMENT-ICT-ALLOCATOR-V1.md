Here’s the full content for **`IMPLEMENT-ICT-ALLOCATOR-V1.md`** that you can drop straight into your repo 👇

---

````markdown
# IMPLEMENT-ICT-ALLOCATOR-V1.md

**Version:** v1 – Safe, Add-Only Implementation  
**Scope:** Introduce a new ICT actor + dashboard to manage physical device allocation in the ICT Centre **without** changing existing Phase 0/1/2/3 behaviour.

---

## 🎯 Objective

Introduce:

- A **new ICT role**: `IctAllocator`
- A **new API controller** for allocation
- A **new dashboard** for managing device locations
- Core tables to store **Storage Locations** (zones/shelves) and **Device Locations** (current + history)

All of this must be:

- ✅ **Additive only** (no breaking changes to existing flows)
- ✅ Limited to **ICT allocator** functionality in Phase 2
- ✅ Re-usable later when we are ready for deeper integration (Receipting, Pre-Assessment, QA, Dispatch)

---

## 🔐 High-Level Design

1. **Core DB (DeviceDeskDbContext)**:  
   Add shared tables:
   - `StorageLocation` – describes physical zones/shelves
   - `DeviceLocation` – current location of a device
   - `DeviceLocationHistory` – full movement history

2. **New Role**:  
   `IctAllocator` – dedicated actor responsible for connecting **physical shelves** to **system locations**.

3. **New Service**:  
   `LocationService` – handles moving a device between locations and writing history.

4. **New API Controller** (Phase 2):  
   `/api/phase2/allocation/*` – used **only** by `IctAllocator` for:
   - Searching device by serial
   - Listing allowed locations for that device
   - Moving device between locations

5. **New UI**:  
   `ict-allocation-dashboard.html` – simple page to:
   - Scan/search serial
   - See current location
   - Select shelf/zone
   - Confirm move

---

## ✅ Step 1 – Core Model: Storage + Locations

> Project: **Core / Infrastructure** (where `Device` and `School` live)  
> Context: `DeviceDeskDbContext`

### 1.1 Add enums

Create or extend a shared enums file, e.g.:

`DeviceDesk.Infrastructure/Data/Enums/StorageEnums.cs`

```csharp
public enum DeviceCategory
{
    Unknown = 0,
    Laptop = 1,
    Desktop = 2,
    Printer = 3,
    VrHeadset = 4,
    Monitor = 5,
    Other = 99
}

public enum StorageArea
{
    Unknown = 0,
    Phase2IctCenter = 2,
    Phase2DispatchReady = 6,
    AtSchool = 8,
    ScrapCage = 9
}
````

> You can extend `StorageArea` later (warehouse, benches, QA, parts, etc.)

---

### 1.2 Add entities

In the **core** project (same namespace as `Device` and `School`), add:

```csharp
public class StorageLocation
{
    public int Id { get; set; }

    public int? SchoolId { get; set; }
    public School? School { get; set; }

    public DeviceCategory Category { get; set; }
    public StorageArea Area { get; set; }

    public string Name { get; set; } = null!;
    public string LocationCode { get; set; } = null!; // e.g. "EMIS500123-LAP-A01"

    public bool IsDispatchReadyZone { get; set; }
    public bool IsActive { get; set; } = true;
}

public class DeviceLocation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    public int StorageLocationId { get; set; }
    public StorageLocation StorageLocation { get; set; } = null!;

    public DateTime MovedAt { get; set; } = DateTime.UtcNow;
    public string? MovedByUserId { get; set; }
    public bool IsCurrent { get; set; } = true;
}

public class DeviceLocationHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    public int? FromLocationId { get; set; }
    public StorageLocation? FromLocation { get; set; }

    public int ToLocationId { get; set; }
    public StorageLocation ToLocation { get; set; } = null!;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Reason { get; set; }
    public string? MovedByUserId { get; set; }
}
```

---

### 1.3 Register in `DeviceDeskDbContext`

In your **core** DbContext:

```csharp
public DbSet<StorageLocation> StorageLocations => Set<StorageLocation>();
public DbSet<DeviceLocation> DeviceLocations => Set<DeviceLocation>();
public DbSet<DeviceLocationHistory> DeviceLocationHistory => Set<DeviceLocationHistory>();
```

Optionally in `OnModelCreating`:

```csharp
protected override void OnModelCreating(ModelBuilder builder)
{
    base.OnModelCreating(builder);

    builder.Entity<StorageLocation>()
        .HasIndex(x => x.LocationCode)
        .IsUnique();

    builder.Entity<DeviceLocation>()
        .HasIndex(x => new { x.DeviceId, x.IsCurrent });
}
```

---

### 1.4 Run core migration

From the core project:

```bash
dotnet ef migrations add AddStorageLocationsAndDeviceLocations -c DeviceDeskDbContext
dotnet ef database update -c DeviceDeskDbContext
```

✅ At this point:

* New tables exist.
* No existing workflows are aware of them yet.
* Safe.

---

## ✅ Step 2 – New Role: `IctAllocator`

> Project: Web/API layer (where you define roles & seed Identity)

### 2.1 Add role constant

In your `UserRoles` static class or equivalent:

```csharp
public static class UserRoles
{
    // existing roles...
    public const string IctAllocator = "IctAllocator";
}
```

---

### 2.2 Seed the role (+ optional user)

In your Identity seeding logic:

```csharp
var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
if (!await roleManager.RoleExistsAsync(UserRoles.IctAllocator))
{
    await roleManager.CreateAsync(new IdentityRole(UserRoles.IctAllocator));
}

var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
var allocator = await userManager.FindByNameAsync("ict.allocator");
if (allocator == null)
{
    allocator = new ApplicationUser
    {
        UserName = "ict.allocator",
        Email = "ict.allocator@example.com",
        EmailConfirmed = true
    };
    await userManager.CreateAsync(allocator, "StrongPass1!");
    await userManager.AddToRoleAsync(allocator, UserRoles.IctAllocator);
}
```

> Adjust username/email/password to your environment.

---

## ✅ Step 3 – LocationService (shared helper)

> Project: likely `Modules/Phase2/Services` or shared service folder.

Create `LocationService`:

```csharp
public interface ILocationService
{
    Task MoveDeviceAsync(
        Guid deviceId,
        int toLocationId,
        string? reason,
        string? userId,
        CancellationToken ct = default);
}
```

Implementation:

```csharp
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
        var now = DateTime.UtcNow;

        var current = await _coreDb.DeviceLocations
            .FirstOrDefaultAsync(x => x.DeviceId == deviceId && x.IsCurrent, ct);

        if (current != null)
            current.IsCurrent = false;

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
```

---

### 3.1 Register in DI

In your main `Program.cs` / service registration:

```csharp
builder.Services.AddScoped<ILocationService, LocationService>();
```

✅ No existing code is forced to use this.
Only the new controller will use it in v1.

---

## ✅ Step 4 – ICT Allocation API (Phase 2, new controller)

> Project: Phase 2 API
> File: `Modules/Phase2/Controllers/AllocationController.cs` (name can vary but must be new)

```csharp
[Route("api/phase2/allocation")]
[ApiController]
[Authorize(Roles = UserRoles.IctAllocator)]
public class AllocationController : ControllerBase
{
    private readonly Phase2DbContext _phase2Db;
    private readonly DeviceDeskDbContext _coreDb;
    private readonly ILocationService _locationService;

    public AllocationController(
        Phase2DbContext phase2Db,
        DeviceDeskDbContext coreDb,
        ILocationService locationService)
    {
        _phase2Db = phase2Db;
        _coreDb = coreDb;
        _locationService = locationService;
    }

    // 1) Search device by serial (Phase 2 + core + current location)
    [HttpGet("search")]
    public async Task<IActionResult> SearchBySerial([FromQuery] string serial, CancellationToken ct)
    {
        serial = serial?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(serial))
            return BadRequest("Serial is required.");

        var p2 = await _phase2Db.Devices
            .FirstOrDefaultAsync(d => d.Serial == serial, ct);

        if (p2 == null)
            return NotFound("Device not found in Phase 2.");

        var dev = await _coreDb.Devices
            .Include(d => d.School)
            .FirstOrDefaultAsync(d => d.SerialNumber == serial, ct);

        StorageLocation? currentLoc = null;
        if (dev != null)
        {
            currentLoc = await _coreDb.DeviceLocations
                .Include(dl => dl.StorageLocation)
                .Where(dl => dl.DeviceId == dev.Id && dl.IsCurrent)
                .Select(dl => dl.StorageLocation)
                .FirstOrDefaultAsync(ct);
        }

        return Ok(new
        {
            phase2Id = p2.Id,
            serial = p2.Serial,
            phase2Stage = p2.Stage.ToString(),
            coreDeviceId = dev?.Id,
            schoolId = dev?.SchoolId,
            schoolName = dev?.School?.Name,
            emis = dev?.School?.EmisCode,
            category = dev?.Category.ToString(),
            currentLocation = currentLoc == null ? null : new
            {
                id = currentLoc.Id,
                code = currentLoc.LocationCode,
                name = currentLoc.Name,
                area = currentLoc.Area.ToString()
            }
        });
    }

    // 2) Get available locations (per school + category)
    [HttpGet("locations")]
    public async Task<IActionResult> GetLocationsForDevice(
        [FromQuery] Guid deviceId,
        CancellationToken ct)
    {
        var dev = await _coreDb.Devices
            .Include(d => d.School)
            .FirstOrDefaultAsync(d => d.Id == deviceId, ct);

        if (dev == null)
            return NotFound("Device not found in core DB.");

        if (dev.SchoolId == null)
            return BadRequest("Device has no school assigned.");

        var locations = await _coreDb.StorageLocations
            .Where(x => x.SchoolId == dev.SchoolId
                        && x.Category == dev.Category
                        && x.Area == StorageArea.Phase2IctCenter
                        && x.IsActive)
            .Select(x => new { id = x.Id, code = x.LocationCode, name = x.Name })
            .ToListAsync(ct);

        return Ok(new
        {
            deviceId = dev.Id,
            schoolName = dev.School!.Name,
            emis = dev.School.EmisCode,
            category = dev.Category.ToString(),
            locations
        });
    }

    public record MoveRequest(Guid DeviceId, int StorageLocationId, string? Reason);

    // 3) Move device to selected location
    [HttpPost("move")]
    public async Task<IActionResult> MoveDevice([FromBody] MoveRequest req, CancellationToken ct)
    {
        var devExists = await _coreDb.Devices.AnyAsync(d => d.Id == req.DeviceId, ct);
        if (!devExists) return NotFound("Device not found.");

        var locExists = await _coreDb.StorageLocations.AnyAsync(l => l.Id == req.StorageLocationId && l.IsActive, ct);
        if (!locExists) return NotFound("Location not found or inactive.");

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        await _locationService.MoveDeviceAsync(
            req.DeviceId,
            req.StorageLocationId,
            req.Reason ?? "Manual ICT allocation",
            userId,
            ct);

        return Ok(new { success = true });
    }
}
```

✅ This controller:

* Is **restricted to** `IctAllocator`.
* **Does not** touch `Phase2Device.Stage`, QA, Receipting, or Dispatch.
* Only moves devices in the new location tables.

---

## ✅ Step 5 – ICT Allocation Dashboard (HTML + JS)

> Project: Phase 2 UI
> File: `Modules/Phase2/UI/ict-allocation-dashboard.html` (new file)

Basic version:

```html
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8" />
    <title>ICT Allocation Dashboard</title>
    <link rel="stylesheet" href="/css/site.css" />
</head>
<body>
<div class="container">
    <h1>ICT Allocation Dashboard</h1>

    <!-- Search -->
    <section>
        <h2>Search Device</h2>
        <form id="searchForm">
            <input type="text" id="serialInput" placeholder="Scan or type serial" required />
            <button type="submit">Search</button>
        </form>
        <div id="deviceInfo" style="margin-top:1rem;"></div>
    </section>

    <!-- Allocation -->
    <section id="allocationSection" style="display:none; margin-top:2rem;">
        <h2>Allocate / Move Device</h2>
        <div id="currentLocation"></div>

        <label for="locationSelect">Select new location (zone/shelf):</label>
        <select id="locationSelect"></select>

        <br />

        <label for="reasonInput">Reason (optional):</label>
        <input type="text" id="reasonInput" placeholder="e.g. Moved to Laptop Zone A" />

        <br />
        <button id="moveButton">Move Device</button>
        <div id="moveResult" style="margin-top:1rem;"></div>
    </section>
</div>

<script>
    let currentDeviceId = null;

    const searchForm = document.getElementById('searchForm');
    const serialInput = document.getElementById('serialInput');
    const deviceInfo = document.getElementById('deviceInfo');
    const allocationSection = document.getElementById('allocationSection');
    const locationSelect = document.getElementById('locationSelect');
    const reasonInput = document.getElementById('reasonInput');
    const moveButton = document.getElementById('moveButton');
    const moveResult = document.getElementById('moveResult');
    const currentLocationDiv = document.getElementById('currentLocation');

    searchForm.addEventListener('submit', async (e) => {
        e.preventDefault();
        const serial = serialInput.value.trim();
        if (!serial) return;

        deviceInfo.textContent = 'Loading...';
        allocationSection.style.display = 'none';
        moveResult.textContent = '';

        const res = await fetch(`/api/phase2/allocation/search?serial=${encodeURIComponent(serial)}`);
        if (!res.ok) {
            const text = await res.text();
            deviceInfo.textContent = `Error: ${text}`;
            return;
        }

        const data = await res.json();
        deviceInfo.innerHTML = `
            <p><strong>Serial:</strong> ${data.serial}</p>
            <p><strong>Stage:</strong> ${data.phase2Stage ?? 'n/a'}</p>
            <p><strong>School:</strong> ${data.schoolName ?? 'n/a'} (${data.emis ?? 'no EMIS'})</p>
            <p><strong>Category:</strong> ${data.category ?? 'n/a'}</p>
        `;

        if (!data.coreDeviceId || !data.schoolId) {
            deviceInfo.innerHTML += `<p style="color:red;">Device is not properly linked to a school/core record. Fix Phase 0/Devices first.</p>`;
            return;
        }

        currentDeviceId = data.coreDeviceId;

        const loc = data.currentLocation;
        if (loc) {
            currentLocationDiv.innerHTML = `
                <p><strong>Current Location:</strong> ${loc.code} – ${loc.name} (${loc.area})</p>
            `;
        } else {
            currentLocationDiv.innerHTML = `<p><strong>Current Location:</strong> Not set.</p>`;
        }

        const locRes = await fetch(`/api/phase2/allocation/locations?deviceId=${encodeURIComponent(currentDeviceId)}`);
        if (!locRes.ok) {
            const text = await locRes.text();
            moveResult.textContent = `Error loading locations: ${text}`;
            return;
        }

        const locData = await locRes.json();
        locationSelect.innerHTML = '';
        locData.locations.forEach(l => {
            const opt = document.createElement('option');
            opt.value = l.id;
            opt.textContent = `${l.code} – ${l.name}`;
            locationSelect.appendChild(opt);
        });

        allocationSection.style.display = locData.locations.length ? 'block' : 'none';
    });

    moveButton.addEventListener('click', async () => {
        if (!currentDeviceId) return;
        const locId = parseInt(locationSelect.value, 10);
        const reason = reasonInput.value.trim();

        moveResult.textContent = 'Moving...';

        const res = await fetch('/api/phase2/allocation/move', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                deviceId: currentDeviceId,
                storageLocationId: locId,
                reason: reason || null
            })
        });

        if (!res.ok) {
            const text = await res.text();
            moveResult.textContent = `Error: ${text}`;
            return;
        }

        moveResult.textContent = 'Device moved successfully.';
    });
</script>
</body>
</html>
```

---

## ✅ Step 6 – Menu / Navigation (optional)

> Project: Phase 2 UI shell / sidebar partial

Add a link to the new dashboard, visible only to `IctAllocator` (depending on how you currently handle role-based UI).

Example:

```html
<li class="nav-item role-ict-allocator">
    <a href="/phase2/ict-allocation-dashboard.html">ICT Allocation</a>
</li>
```

Then in your layout JS, show/hide `.role-ict-allocator` based on the user roles returned by `/api/auth/current-user`.

---

## ✅ Step 7 – Seed Sample StorageLocations (for testing)

You can seed via code or direct SQL. Example concept:

* Global:

  * `ICT-STAGING` (Area = Phase2IctCenter, SchoolId = null, Category = Unknown)
* Per school `EMIS500123`:

  * `EMIS500123-LAP-A01` – Laptop Zone A
  * `EMIS500123-LAP-A02` – Laptop Zone B

Example C# seeding snippet:

```csharp
if (!await _coreDb.StorageLocations.AnyAsync())
{
    var school = await _coreDb.Schools.FirstOrDefaultAsync(s => s.EmisCode == "500123");

    _coreDb.StorageLocations.AddRange(
        new StorageLocation
        {
            Name = "ICT Staging",
            LocationCode = "ICT-STAGING",
            Area = StorageArea.Phase2IctCenter,
            Category = DeviceCategory.Unknown,
            IsActive = true
        },
        new StorageLocation
        {
            Name = "Laptop Zone A",
            LocationCode = "EMIS500123-LAP-A01",
            SchoolId = school?.SchoolId,
            Area = StorageArea.Phase2IctCenter,
            Category = DeviceCategory.Laptop,
            IsActive = true
        }
    );

    await _coreDb.SaveChangesAsync();
}
```

---

## 🧪 Quick Test Plan (v1)

1. **Login** as `ict.allocator` (or whichever user you assigned to `IctAllocator`).
2. Navigate to `/phase2/ict-allocation-dashboard.html`.
3. Take a serial that:

   * Exists in `Phase2DbContext.Devices` (Phase 2),
   * Exists in core `Devices` with a valid `SchoolId`.
4. Search the serial:

   * You should see Phase 2 stage, school name, EMIS, category.
   * If not linked to a school/core record, fix Phase 0 import / core device.
5. Confirm current location:

   * If first time, it may say “Not set”.
6. Pick a zone from the dropdown (e.g. `EMIS500123-LAP-A01`) and click **Move Device**.
7. Check DB:

   * `DeviceLocations` has one row for this DeviceId with `IsCurrent = 1`.
   * `DeviceLocationHistory` has a movement record.
8. Try moving again:

   * `DeviceLocations` previous row becomes `IsCurrent = 0`, new row becomes `IsCurrent = 1`.
   * `DeviceLocationHistory` has another movement.

---

## 🚧 What This v1 Does *Not* Change (On Purpose)

* No changes to:

  * Phase 0 import logic,
  * Phase 1 receiving/GRV logic,
  * Phase 2 Receipting / Pre-Assessment / Assessment / QA logic,
  * Phase 3 Dispatch logic.

All of that continues as-is.

The **allocator** role is a **sidecar**:

* It uses the new location tables.
* It gives you a safe playground to:

  * Define your zones,
  * Test real physical movement,
  * Validate that your shelf layout + system model make sense.

Once you are happy with this and it’s stable, **v2** can:

* Start calling `LocationService` from Receipting, Pre-Assessment, QA, and Dispatch.
* Start using `DeviceLocation` in Phase 3 to drive picking and POD creation.

---

```
```
