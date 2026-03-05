You’re right to call me out there — earlier I was answering too “generic Phase 2” and not treating ICT Allocator as a **first-class actor** like the others. I’ve gone through the `Phase2.zip` now (controllers, services, UI partials, ACCESS_CONTROL.md) and I’ll line this up properly with the actual codebase.

Below is a **Cursor-ready implementation pack** you can paste into a `.md` file and follow step-by-step.

---

## 🎯 Goal

Add a **new Phase 2 actor: ICT Allocator** with:

* Their **own role** and access control.
* A **proper dashboard page** that fits Phase 2’s look & feel (same layout as `scan-out.html` / `pre-assessment.html`).
* Ability to:

  * 🔍 Look up a device (serial/IMEI).
  * 📦 Allocate storage location for that device inside the ICT center.
  * ♻️ Re-allocate or clear storage.
  * 📋 See a mini list of recent allocations.

We’ll **reuse the existing Phase2Stage `SchoolAllocation`** to mark that a device has been allocated to per-school storage.

---

## 0. Context from the current Phase 2 module

From the zip:

* **Actors already documented** in `Phase2/ACCESS_CONTROL.md`:

  * `IctReceivingClerk`
  * `IctInspector`
  * `IctTechnician`
  * `IctClerk`
  * `IctManager`
* **Stages** in `Phase2/Models/Enums.cs` include `SchoolAllocation` (mentioned near other stages).
* UI pattern:

  * `Phase2/UI/index.html` provides the Phase 2 shell (brandbar, sidebar, main content area).
  * Pages like `pre-assessment.html`, `scan-out.html` are **partials loaded into `#mainContent`** and contain:

    * Bootstrap structure
    * Inline `<script>` with `fetch`/`apiPost` calls to `/api/phase2/...`.
* Dispatch uses `Phase2DispatchController` + `DispatchService`.

We’ll build ICT Allocator to match this exact style: a **partial page** + a **Phase 2 API controller + service**.

---

## 1. Domain model: add storage fields to `Phase2Device`

### 1.1 Update `Phase2/Models/Phase2Device.cs`

In `Phase2Device` (where you already have general device metadata, assessment, QA, dispatch, etc.), add **storage-related properties** near the other operational fields:

```csharp
// Step 5: Storage allocation inside ICT Centre
public string? StorageArea { get; set; }      // e.g. "Main Store", "Overflow A"
public string? StorageRow { get; set; }       // e.g. "Row 01"
public string? StorageRack { get; set; }      // e.g. "Rack B"
public string? StorageShelf { get; set; }     // e.g. "Shelf 3"
public string? StorageBin { get; set; }       // e.g. "Bin 12"
public string? StorageNotes { get; set; }     // Free-text notes
public DateTimeOffset? StorageAllocatedAt { get; set; }
public string? StorageAllocatedByUserId { get; set; }
```

> Keep it **string-only** for now so we don’t introduce new tables or relationships. This also avoids touching Identity from this module.

### 1.2 Ensure `Phase2Stage` has `SchoolAllocation`

In `Phase2/Models/Enums.cs` confirm that `Phase2Stage` contains something like:

```csharp
public enum Phase2Stage
{
    Receipting = 0,
    PreAssessment = 1,
    Assessment = 2,
    Repair = 3,
    Quality = 4,
    Dispatch = 5,
    Disposal = 6,
    SchoolAllocation = 7,   // already hinted in your file - keep / add this
    // ...
}
```

If `SchoolAllocation` is **not** there for any reason, add it with the next numeric value that doesn’t break existing seeds.

### 1.3 Migration for Phase 2

From the main repo root (Cursor terminal in `DeviceDesk`):

```bash
dotnet ef migrations add Phase2_StorageAllocationFields --context Phase2DbContext
dotnet ef database update --context Phase2DbContext
```

If migrations are in `Modules/Phase2/Data/Migrations`, this will drop the new migration file there and update the Phase 2 DB.

---

## 2. New backend service: `AllocationService`

Create a new file:

**`Modules/Phase2/Services/AllocationService.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DeviceDesk.Modules.Phase2.Data;
using DeviceDesk.Modules.Phase2.Models;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase2.Services;

public class AllocationService
{
    private readonly Phase2DbContext _db;
    private readonly AuditService _audit;

    public AllocationService(Phase2DbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<Phase2Device?> FindBySerialAsync(string serial)
    {
        if (string.IsNullOrWhiteSpace(serial)) return null;
        var s = serial.Trim();

        // You can adjust this to also check IMEI if your model has it
        return await _db.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.SerialNumber == s);
    }

    public async Task<AllocationDeviceDto?> GetDeviceSummaryAsync(string serial)
    {
        var device = await FindBySerialAsync(serial);
        if (device == null) return null;

        return new AllocationDeviceDto
        {
            Id = device.Id,
            SerialNumber = device.SerialNumber,
            CurrentStage = device.Stage,
            Model = device.ModelName,        // adjust to your actual property
            SchoolName = device.SchoolName,  // adjust if you have school info
            StorageArea = device.StorageArea,
            StorageRow = device.StorageRow,
            StorageRack = device.StorageRack,
            StorageShelf = device.StorageShelf,
            StorageBin = device.StorageBin,
            StorageNotes = device.StorageNotes,
            StorageAllocatedAt = device.StorageAllocatedAt
        };
    }

    public async Task AllocateAsync(string serial, StorageAllocationRequest request, string userId)
    {
        if (string.IsNullOrWhiteSpace(serial))
            throw new ArgumentException("Serial is required", nameof(serial));

        var s = serial.Trim();

        var device = await _db.Devices.FirstOrDefaultAsync(d => d.SerialNumber == s);
        if (device == null)
            throw new InvalidOperationException($"No device found for serial {s}");

        // Optional: only allow allocation for certain stages
        // e.g. after Quality or Dispatch-ready
        // if (device.Stage != Phase2Stage.Dispatch && device.Stage != Phase2Stage.SchoolAllocation)
        //     throw new InvalidOperationException("Device is not ready for storage allocation.");

        device.StorageArea = request.StorageArea?.Trim();
        device.StorageRow = request.StorageRow?.Trim();
        device.StorageRack = request.StorageRack?.Trim();
        device.StorageShelf = request.StorageShelf?.Trim();
        device.StorageBin = request.StorageBin?.Trim();
        device.StorageNotes = request.StorageNotes?.Trim();
        device.StorageAllocatedAt = DateTimeOffset.UtcNow;
        device.StorageAllocatedByUserId = userId;

        // Move to SchoolAllocation stage to show it has been allocated into per-school storage
        device.Stage = Phase2Stage.SchoolAllocation;

        await _db.SaveChangesAsync();

        await _audit.LogAsync(device.Id, "StorageAllocation",
            $"Allocated to {device.StorageArea} / {device.StorageRow} / {device.StorageRack} / {device.StorageShelf} / {device.StorageBin} by {userId}");
    }

    public async Task<IReadOnlyList<AllocationDeviceDto>> GetRecentAllocationsAsync(int take = 50)
    {
        if (take <= 0) take = 50;

        return await _db.Devices
            .AsNoTracking()
            .Where(d => d.StorageAllocatedAt != null)
            .OrderByDescending(d => d.StorageAllocatedAt)
            .Take(take)
            .Select(d => new AllocationDeviceDto
            {
                Id = d.Id,
                SerialNumber = d.SerialNumber,
                CurrentStage = d.Stage,
                Model = d.ModelName,
                SchoolName = d.SchoolName,
                StorageArea = d.StorageArea,
                StorageRow = d.StorageRow,
                StorageRack = d.StorageRack,
                StorageShelf = d.StorageShelf,
                StorageBin = d.StorageBin,
                StorageNotes = d.StorageNotes,
                StorageAllocatedAt = d.StorageAllocatedAt
            })
            .ToListAsync();
    }

    public async Task ClearAllocationAsync(string serial, string userId)
    {
        var s = serial?.Trim();
        if (string.IsNullOrWhiteSpace(s))
            throw new ArgumentException("Serial is required", nameof(serial));

        var device = await _db.Devices.FirstOrDefaultAsync(d => d.SerialNumber == s);
        if (device == null)
            throw new InvalidOperationException($"No device found for serial {s}");

        device.StorageArea = null;
        device.StorageRow = null;
        device.StorageRack = null;
        device.StorageShelf = null;
        device.StorageBin = null;
        device.StorageNotes = null;
        device.StorageAllocatedAt = null;
        device.StorageAllocatedByUserId = null;

        // Optionally: revert back to previous stage (e.g. Dispatch)
        // device.Stage = Phase2Stage.Dispatch;

        await _db.SaveChangesAsync();

        await _audit.LogAsync(device.Id, "StorageAllocation", $"Storage allocation cleared by {userId}");
    }
}

public class AllocationDeviceDto
{
    public Guid Id { get; set; }
    public string? SerialNumber { get; set; }
    public Phase2Stage CurrentStage { get; set; }
    public string? Model { get; set; }
    public string? SchoolName { get; set; }

    public string? StorageArea { get; set; }
    public string? StorageRow { get; set; }
    public string? StorageRack { get; set; }
    public string? StorageShelf { get; set; }
    public string? StorageBin { get; set; }
    public string? StorageNotes { get; set; }
    public DateTimeOffset? StorageAllocatedAt { get; set; }
}

public class StorageAllocationRequest
{
    public string? StorageArea { get; set; }
    public string? StorageRow { get; set; }
    public string? StorageRack { get; set; }
    public string? StorageShelf { get; set; }
    public string? StorageBin { get; set; }
    public string? StorageNotes { get; set; }
}
```

> Adjust `ModelName` / `SchoolName` to whatever your actual `Phase2Device` properties are called. Cursor can help you rename after you paste.

---

## 3. New API controller: `Phase2AllocationController`

Create:

**`Modules/Phase2/Controllers/Phase2AllocationController.cs`**

Pattern copied from `Phase2DispatchController`:

```csharp
using System;
using System.Threading.Tasks;
using DeviceDesk.Infrastructure.Identity;
using DeviceDesk.Modules.Phase2.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeviceDesk.Modules.Phase2.Controllers;

[ApiController]
[Route("api/phase2/allocation")]
[Authorize(Roles = UserRoles.IctAllocator)]
public class Phase2AllocationController : ControllerBase
{
    private readonly AllocationService _allocation;

    public Phase2AllocationController(AllocationService allocation)
    {
        _allocation = allocation;
    }

    private string GetCurrentUserId()
    {
        return User?.FindFirst("sub")?.Value
               ?? User?.FindFirst("uid")?.Value
               ?? User?.Identity?.Name
               ?? "unknown";
    }

    [HttpGet("device")]
    public async Task<IActionResult> GetDevice([FromQuery] string serial)
    {
        if (string.IsNullOrWhiteSpace(serial))
            return BadRequest(new { message = "Serial is required" });

        var device = await _allocation.GetDeviceSummaryAsync(serial);
        if (device == null)
            return NotFound(new { message = "Device not found" });

        return Ok(device);
    }

    [HttpPost("allocate")]
    public async Task<IActionResult> Allocate([FromBody] StorageAllocationRequest request, [FromQuery] string serial)
    {
        if (string.IsNullOrWhiteSpace(serial))
            return BadRequest(new { message = "Serial is required" });

        var userId = GetCurrentUserId();

        try
        {
            await _allocation.AllocateAsync(serial, request, userId);
            var updated = await _allocation.GetDeviceSummaryAsync(serial);
            return Ok(new
            {
                message = "Storage allocated successfully.",
                device = updated
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("clear")]
    public async Task<IActionResult> Clear([FromQuery] string serial)
    {
        if (string.IsNullOrWhiteSpace(serial))
            return BadRequest(new { message = "Serial is required" });

        var userId = GetCurrentUserId();

        try
        {
            await _allocation.ClearAllocationAsync(serial, userId);
            return Ok(new { message = "Storage allocation cleared." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("recent")]
    public async Task<IActionResult> Recent([FromQuery] int take = 50)
    {
        var items = await _allocation.GetRecentAllocationsAsync(take);
        return Ok(items);
    }
}
```

> If you have a common `Phase2BaseController` that handles user id, you can inherit from that instead of `ControllerBase` and reuse its helper.

### 3.1 Wire up AllocationService in DI

In `Program.cs` where other Phase 2 services are registered (e.g. `DispatchService`, `QualityService`, etc.), add:

```csharp
builder.Services.AddScoped<AllocationService>();
```

---

## 4. Access control & roles

### 4.1 Update `ACCESS_CONTROL.md` (Phase 2)

In `Phase2/ACCESS_CONTROL.md`, add a new actor section:

```md
### ICT Allocator (Phase 2)

**Role:** `IctAllocator`

**Primary responsibilities:**

- Assign storage locations to devices inside the ICT Centre.
- Re-allocate devices when they are moved between rows/racks/bins.
- Provide quick visibility of where devices are stored per school before dispatch.

**Permissions (Phase 2):**

- `GET /api/phase2/allocation/device`
- `POST /api/phase2/allocation/allocate`
- `POST /api/phase2/allocation/clear`
- `GET /api/phase2/allocation/recent`
```

### 4.2 Add role constant

In `Infrastructure/Identity/UserRoles.cs` (or wherever your role constants live), add:

```csharp
public const string IctAllocator = "IctAllocator";
```

### 4.3 Seed role + optional test user (optional but recommended)

Where you seed roles/users (e.g. `ApplicationDbContextSeed` or similar):

* Ensure `IctAllocator` is created as an Identity role.
* Optionally create a demo user:

```csharp
// pseudo:
await CreateRoleAsync("IctAllocator");
await CreateUserWithRoleAsync("allocator@demo.local", "IctAllocator");
```

This part depends on your existing seeding patterns, so let Cursor help you search for where `IctClerk` / `IctTechnician` are seeded and copy that pattern.

---

## 5. Frontend: ICT Allocator dashboard page

### 5.1 Sidebar navigation entry

Wherever your Phase 2 sidebar for ICT actors is defined (in the full repo it’s likely something like `Modules/Phase2/UI/partials/sidebar.html` or a similar partial):

Add a menu item:

```html
<li class="nav-item">
  <a href="#" class="nav-link" data-page="allocator.html">
    <i class="bi bi-box-seam me-2"></i>
    ICT Storage Allocator
  </a>
</li>
```

And ensure your main `index.html` loader knows how to load `allocator.html` into `#mainContent` when `data-page="allocator.html"` is clicked (same pattern as `pre-assessment.html`, `scan-out.html`).

### 5.2 New partial: `Modules/Phase2/UI/allocator.html`

Create this as a **content partial only**, like `pre-assessment.html` and `scan-out.html` (no `<html>`, `<body>`, brandbar, etc.).

```html
<div class="container-fluid py-4">
  <div class="d-flex justify-content-between align-items-center mb-4">
    <div>
      <h2 class="mb-0">ICT Storage Allocator</h2>
      <small class="text-muted">
        Look up devices and assign storage locations inside the ICT Centre.
      </small>
    </div>
    <div>
      <span class="badge bg-secondary" id="allocatorRoleLabel">ICT Allocator</span>
    </div>
  </div>

  <div class="row">
    <!-- Left: lookup & device info -->
    <div class="col-lg-5 mb-4">
      <div class="card shadow-sm h-100">
        <div class="card-header bg-white">
          <h5 class="mb-0">1. Look up device</h5>
        </div>
        <div class="card-body">
          <form id="lookupForm" class="mb-3">
            <div class="mb-3">
              <label for="serialInput" class="form-label">
                Serial / IMEI
              </label>
              <input type="text" id="serialInput" class="form-control"
                     placeholder="Scan or type serial / IMEI" required />
            </div>
            <button type="submit" class="btn btn-primary">
              <i class="bi bi-search me-1"></i> Find device
            </button>
          </form>

          <div id="lookupAlert" class="alert d-none" role="alert"></div>

          <div id="deviceSummary" class="border rounded p-3 d-none">
            <h6 class="mb-2">Device summary</h6>
            <dl class="row mb-0">
              <dt class="col-4">Serial</dt>
              <dd class="col-8" id="summarySerial"></dd>

              <dt class="col-4">Model</dt>
              <dd class="col-8" id="summaryModel"></dd>

              <dt class="col-4">School</dt>
              <dd class="col-8" id="summarySchool"></dd>

              <dt class="col-4">Stage</dt>
              <dd class="col-8" id="summaryStage"></dd>

              <dt class="col-4">Stored at</dt>
              <dd class="col-8" id="summaryStorage"></dd>
            </dl>
          </div>
        </div>
      </div>
    </div>

    <!-- Right: allocation form -->
    <div class="col-lg-7 mb-4">
      <div class="card shadow-sm h-100">
        <div class="card-header bg-white d-flex justify-content-between align-items-center">
          <h5 class="mb-0">2. Allocate storage</h5>
          <small class="text-muted" id="allocationSerialLabel">No device selected</small>
        </div>
        <div class="card-body">
          <form id="allocationForm">
            <div class="row">
              <div class="col-md-6 mb-3">
                <label for="storageArea" class="form-label">Area / Store</label>
                <input type="text" id="storageArea" class="form-control"
                       placeholder="e.g. Main Store" />
              </div>
              <div class="col-md-3 mb-3">
                <label for="storageRow" class="form-label">Row</label>
                <input type="text" id="storageRow" class="form-control"
                       placeholder="e.g. 01" />
              </div>
              <div class="col-md-3 mb-3">
                <label for="storageRack" class="form-label">Rack</label>
                <input type="text" id="storageRack" class="form-control"
                       placeholder="e.g. B" />
              </div>
            </div>

            <div class="row">
              <div class="col-md-3 mb-3">
                <label for="storageShelf" class="form-label">Shelf</label>
                <input type="text" id="storageShelf" class="form-control"
                       placeholder="e.g. 3" />
              </div>
              <div class="col-md-3 mb-3">
                <label for="storageBin" class="form-label">Bin</label>
                <input type="text" id="storageBin" class="form-control"
                       placeholder="e.g. 12" />
              </div>
              <div class="col-md-6 mb-3">
                <label for="storageNotes" class="form-label">Notes</label>
                <textarea id="storageNotes" class="form-control" rows="2"
                          placeholder="Optional notes (e.g. fragile, high priority)"></textarea>
              </div>
            </div>

            <div class="d-flex gap-2">
              <button type="submit" class="btn btn-success" id="btnAllocate" disabled>
                <i class="bi bi-check-circle me-1"></i> Save allocation
              </button>
              <button type="button" class="btn btn-outline-secondary" id="btnClearStorage" disabled>
                <i class="bi bi-eraser me-1"></i> Clear storage
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>
  </div>

  <!-- Recent allocations -->
  <div class="row">
    <div class="col-12">
      <div class="card shadow-sm">
        <div class="card-header bg-white d-flex justify-content-between align-items-center">
          <h5 class="mb-0">3. Recent allocations</h5>
          <button class="btn btn-sm btn-outline-primary" id="btnRefreshRecent">
            <i class="bi bi-arrow-clockwise me-1"></i> Refresh
          </button>
        </div>
        <div class="card-body">
          <div class="table-responsive">
            <table class="table table-sm align-middle" id="recentTable">
              <thead>
                <tr>
                  <th>Allocated at</th>
                  <th>Serial</th>
                  <th>Model</th>
                  <th>School</th>
                  <th>Location</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td colspan="5" class="text-muted text-center">
                    No allocations loaded yet.
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  </div>
</div>

<script>
  const allocApiBase = "/api/phase2/allocation";
  let currentSerial = null;

  function showLookupAlert(message, type = "info") {
    const alert = document.getElementById("lookupAlert");
    alert.textContent = message;
    alert.className = "alert alert-" + type;
    alert.classList.remove("d-none");
  }

  function clearLookupAlert() {
    const alert = document.getElementById("lookupAlert");
    alert.classList.add("d-none");
  }

  function setDeviceSummary(device) {
    document.getElementById("deviceSummary").classList.remove("d-none");
    document.getElementById("summarySerial").textContent = device.serialNumber || "";
    document.getElementById("summaryModel").textContent = device.model || "";
    document.getElementById("summarySchool").textContent = device.schoolName || "";
    document.getElementById("summaryStage").textContent = device.currentStage || "";

    const locParts = [];
    if (device.storageArea) locParts.push(device.storageArea);
    if (device.storageRow) locParts.push("Row " + device.storageRow);
    if (device.storageRack) locParts.push("Rack " + device.storageRack);
    if (device.storageShelf) locParts.push("Shelf " + device.storageShelf);
    if (device.storageBin) locParts.push("Bin " + device.storageBin);

    document.getElementById("summaryStorage").textContent =
      locParts.length ? locParts.join(" · ") : "Not allocated";

    // populate form
    document.getElementById("storageArea").value = device.storageArea || "";
    document.getElementById("storageRow").value = device.storageRow || "";
    document.getElementById("storageRack").value = device.storageRack || "";
    document.getElementById("storageShelf").value = device.storageShelf || "";
    document.getElementById("storageBin").value = device.storageBin || "";
    document.getElementById("storageNotes").value = device.storageNotes || "";

    document.getElementById("allocationSerialLabel").textContent =
      device.serialNumber ? "Allocating: " + device.serialNumber : "No device selected";

    document.getElementById("btnAllocate").disabled = !device.serialNumber;
    document.getElementById("btnClearStorage").disabled = !device.serialNumber;
  }

  async function fetchDevice(serial) {
    clearLookupAlert();
    document.getElementById("deviceSummary").classList.add("d-none");
    document.getElementById("btnAllocate").disabled = true;
    document.getElementById("btnClearStorage").disabled = true;
    document.getElementById("allocationSerialLabel").textContent = "Loading...";

    const resp = await fetch(allocApiBase + "/device?serial=" + encodeURIComponent(serial));
    if (resp.ok) {
      const device = await resp.json();
      currentSerial = device.serialNumber;
      setDeviceSummary(device);
    } else {
      const err = await resp.json().catch(() => ({}));
      showLookupAlert(err.message || "Device not found.", "warning");
      currentSerial = null;
      document.getElementById("allocationSerialLabel").textContent = "No device selected";
    }
  }

  async function allocateStorage(event) {
    event.preventDefault();
    if (!currentSerial) {
      showLookupAlert("Look up a device first.", "warning");
      return;
    }

    clearLookupAlert();

    const body = {
      storageArea: document.getElementById("storageArea").value,
      storageRow: document.getElementById("storageRow").value,
      storageRack: document.getElementById("storageRack").value,
      storageShelf: document.getElementById("storageShelf").value,
      storageBin: document.getElementById("storageBin").value,
      storageNotes: document.getElementById("storageNotes").value
    };

    const resp = await fetch(allocApiBase + "/allocate?serial=" + encodeURIComponent(currentSerial), {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify(body)
    });

    const data = await resp.json().catch(() => ({}));

    if (resp.ok) {
      showLookupAlert(data.message || "Storage allocated.", "success");
      if (data.device) {
        setDeviceSummary(data.device);
      } else {
        await fetchDevice(currentSerial);
      }
      await loadRecentAllocations();
    } else {
      showLookupAlert(data.message || "Failed to allocate storage.", "danger");
    }
  }

  async function clearStorage() {
    if (!currentSerial) {
      showLookupAlert("Look up a device first.", "warning");
      return;
    }

    clearLookupAlert();

    const resp = await fetch(allocApiBase + "/clear?serial=" + encodeURIComponent(currentSerial), {
      method: "POST"
    });

    const data = await resp.json().catch(() => ({}));

    if (resp.ok) {
      showLookupAlert(data.message || "Storage cleared.", "success");
      await fetchDevice(currentSerial);
      await loadRecentAllocations();
    } else {
      showLookupAlert(data.message || "Failed to clear storage.", "danger");
    }
  }

  async function loadRecentAllocations() {
    const tbody = document.querySelector("#recentTable tbody");
    tbody.innerHTML = '<tr><td colspan="5" class="text-muted text-center">Loading...</td></tr>';

    const resp = await fetch(allocApiBase + "/recent");
    if (!resp.ok) {
      tbody.innerHTML = '<tr><td colspan="5" class="text-muted text-center">Failed to load allocations.</td></tr>';
      return;
    }

    const items = await resp.json();
    if (!items || !items.length) {
      tbody.innerHTML = '<tr><td colspan="5" class="text-muted text-center">No recent allocations.</td></tr>';
      return;
    }

    tbody.innerHTML = "";
    for (const item of items) {
      const tr = document.createElement("tr");

      const when = item.storageAllocatedAt
        ? new Date(item.storageAllocatedAt).toLocaleString()
        : "";

      const locParts = [];
      if (item.storageArea) locParts.push(item.storageArea);
      if (item.storageRow) locParts.push("Row " + item.storageRow);
      if (item.storageRack) locParts.push("Rack " + item.storageRack);
      if (item.storageShelf) locParts.push("Shelf " + item.storageShelf);
      if (item.storageBin) locParts.push("Bin " + item.storageBin);

      tr.innerHTML = `
        <td>${when}</td>
        <td>${item.serialNumber || ""}</td>
        <td>${item.model || ""}</td>
        <td>${item.schoolName || ""}</td>
        <td>${locParts.join(" · ")}</td>
      `;
      tbody.appendChild(tr);
    }
  }

  document.getElementById("lookupForm").addEventListener("submit", function (e) {
    e.preventDefault();
    const serial = document.getElementById("serialInput").value;
    if (!serial) {
      showLookupAlert("Enter or scan a serial / IMEI.", "warning");
      return;
    }
    fetchDevice(serial);
  });

  document.getElementById("allocationForm").addEventListener("submit", allocateStorage);
  document.getElementById("btnClearStorage").addEventListener("click", clearStorage);
  document.getElementById("btnRefreshRecent").addEventListener("click", loadRecentAllocations);

  // Initial load of recent allocations
  loadRecentAllocations();
</script>
```

This gives you **exactly what you described**:

* Left panel: **look up device.**
* Right panel: **allocate / re-allocate storage.**
* Bottom panel: **recent allocations** overview.

The sidebar from `index.html` still wraps around this partial, so the allocator feels like a full actor just like the others.

---

## 6. Sanity checks & test flow

Once Cursor has applied all of that:

1. **Rebuild & migrate**

   ```bash
   dotnet build
   dotnet ef database update --context Phase2DbContext
   ```

2. **Create / confirm an `IctAllocator` user**

   * Ensure they have the `IctAllocator` role.
   * Log in as this user.

3. **Open Phase 2 → ICT Storage Allocator**

   * Sidebar should show **“ICT Storage Allocator”**.
   * Page should load with **3 sections** (lookup, allocate, recent).

4. **Test flow**

   * Scan a serial for a device that exists in Phase 2.

   * Confirm summary loads correctly.

   * Fill storage fields and **Save allocation**.

   * Confirm:

     * Summary updates with location.
     * Device stage becomes `SchoolAllocation` in DB.
     * Appears in **Recent allocations**.

   * Click **Clear storage**:

     * Confirm fields clear and stage reverts (if you chose to revert to Dispatch).




## 0. Goal (what this feature must do)

**ICT Allocation Dashboard must:**

1. Search a device by **serial**.
2. Show **device summary** (serial, model, school, phase status, current storage).
3. Allow user to **allocate/update physical storage**:

   * Building / Room
   * Rack / Shelf / Bin (or simple “LocationCode” if you prefer)
   * Optional notes
4. Show **current storage history** (last few allocations).
5. Have its own **Phase 2–style sidebar** with navigation:

* Storage(overview, or show whats in storage, schools in storage(dbo.schools willbe utilised abnd should be linked to collection slip aswell ))
   * Allocate Storage 
   * Storage Overview (list of all locations and counts)
   * Unallocated Devices
   * Unallocated Devices (Phase2 devices with no active DeviceStorageLocation).
   * more be creative what else would fit here now
   

Back-end: under **Phase 2**, using `Phase2DbContext`.
Front-end: under **Modules/Phase2/UI/**, following your existing look & feel (same topbar, fonts, buttons).

---

## 1. Files to create / update

Cursor, we’re going to touch these areas:

### Backend (C#)

1. `Modules/Phase2/Models/DeviceStorageLocation.cs`  **(NEW)**
2. `Modules/Phase2/Models/Enums.cs` or separate enum file  **(ADD StorageType/Status enums if needed)**
3. `Modules/Phase2/Phase2DbContext.cs`  **(ADD DbSet + model config)**
4. `Modules/Phase2/Services/AllocationService.cs`  **(NEW)**
5. `Modules/Phase2/Controllers/Phase2AllocationController.cs`  **(NEW)**

### Frontend (HTML + JS)

6. `wwwroot/phase2/allocator/index.html`  **(UPDATE – current page)**
7. `wwwroot/phase2/allocator/sidebar.html`  **(NEW – allocation sidebar)**
8. `wwwroot/js/phase2/allocator.js`  **(NEW – page logic)**

### Wiring

9. `Program.cs`  **(ensure Allocation routes are mapped, if Phase2 controllers are in a separate assembly/area)**
10. Migration file after model changes (created by `dotnet ef migrations add Phase2_StorageLocations`).

---

## 2. Data Model – physical storage

> Cursor: create a new entity for physical storage allocations in Phase 2.

**File:** `Modules/Phase2/Models/DeviceStorageLocation.cs`

```csharp
namespace DeviceDesk.Modules.Phase2.Models
{
    public class DeviceStorageLocation
    {
        public int Id { get; set; }

        // FK to your Phase2Device or Device entity – adjust type/name to match actual model
        public int Phase2DeviceId { get; set; }
        public Phase2Device Phase2Device { get; set; }

        // Basic storage fields – keep generic to match our UI
        public string Building { get; set; }
        public string Room { get; set; }
        public string Rack { get; set; }
        public string Shelf { get; set; }
        public string Bin { get; set; }

        public string Notes { get; set; }

        // Status (e.g. Active, Moved, Archived)
        public string Status { get; set; }  // or enum if you prefer

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}
```

**File:** `Modules/Phase2/Phase2DbContext.cs` – add DbSet and configure.

```csharp
public DbSet<DeviceStorageLocation> DeviceStorageLocations { get; set; }

protected override void OnModelCreating(ModelBuilder builder)
{
    base.OnModelCreating(builder);

    // ...existing config...

    builder.Entity<DeviceStorageLocation>(b =>
    {
        b.HasKey(x => x.Id);

        b.HasOne(x => x.Phase2Device)
            .WithMany() // or .WithMany(d => d.StorageLocations) if you add a collection
            .HasForeignKey(x => x.Phase2DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Property(x => x.Building).HasMaxLength(128);
        b.Property(x => x.Room).HasMaxLength(128);
        b.Property(x => x.Rack).HasMaxLength(64);
        b.Property(x => x.Shelf).HasMaxLength(64);
        b.Property(x => x.Bin).HasMaxLength(64);
        b.Property(x => x.Status).HasMaxLength(64);

        b.Property(x => x.CreatedAt)
            .HasConversion(
                v => v.UtcDateTime,
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        b.Property(x => x.UpdatedAt)
            .HasConversion(
                v => v.HasValue ? v.Value.UtcDateTime : (DateTime?)null,
                v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : (DateTime?)null);
    });
}
```

> After this:
> `dotnet ef migrations add Phase2_StorageLocations --context Phase2DbContext`
> `dotnet ef database update --context Phase2DbContext`

---

## 3. Backend Service – AllocationService

**File:** `Modules/Phase2/Services/AllocationService.cs`

```csharp
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DeviceDesk.Modules.Phase2.Models;
using DeviceDesk.Modules.Phase2.Data; // whatever namespace your Phase2DbContext is in
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase2.Services
{
    public class AllocationService
    {
        private readonly Phase2DbContext _db;

        public AllocationService(Phase2DbContext db)
        {
            _db = db;
        }

        public async Task<Phase2Device> FindDeviceBySerialAsync(string serial, CancellationToken ct = default)
        {
            serial = serial?.Trim();

            if (string.IsNullOrWhiteSpace(serial))
                return null;

            return await _db.Phase2Devices
                .Include(d => d.School) // adjust navigation to match your model
                .FirstOrDefaultAsync(d => d.SerialNumber == serial, ct);
        }

        public async Task<DeviceStorageLocation[]> GetRecentLocationsForDeviceAsync(int phase2DeviceId, int take = 5, CancellationToken ct = default)
        {
            return await _db.DeviceStorageLocations
                .Where(x => x.Phase2DeviceId == phase2DeviceId)
                .OrderByDescending(x => x.CreatedAt)
                .Take(take)
                .ToArrayAsync(ct);
        }

        public async Task<DeviceStorageLocation> AllocateAsync(
            int phase2DeviceId,
            string building,
            string room,
            string rack,
            string shelf,
            string bin,
            string notes,
            CancellationToken ct = default)
        {
            var device = await _db.Phase2Devices.FindAsync(new object[] { phase2DeviceId }, ct);
            if (device == null) throw new InvalidOperationException("Device not found.");

            var location = new DeviceStorageLocation
            {
                Phase2DeviceId = phase2DeviceId,
                Building = building?.Trim(),
                Room = room?.Trim(),
                Rack = rack?.Trim(),
                Shelf = shelf?.Trim(),
                Bin = bin?.Trim(),
                Notes = notes?.Trim(),
                Status = "Active",
                CreatedAt = DateTimeOffset.UtcNow
            };

            _db.DeviceStorageLocations.Add(location);
            await _db.SaveChangesAsync(ct);

            return location;
        }
    }
}
```

> Cursor: ensure namespace imports match the real project (`Phase2DbContext` namespace, `Phase2Device` location, etc.).

---

## 4. API Controller – Phase2AllocationController

**File:** `Modules/Phase2/Controllers/Phase2AllocationController.cs`

```csharp
using System.Threading;
using System.Threading.Tasks;
using DeviceDesk.Modules.Phase2.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeviceDesk.Modules.Phase2.Controllers
{
    [Route("api/phase2/allocation")]
    [ApiController]
    [Authorize(Roles = "ICTManager,ICTClerk,ICTTechnician")] // adjust roles to your system
    public class Phase2AllocationController : ControllerBase
    {
        private readonly AllocationService _allocationService;

        public Phase2AllocationController(AllocationService allocationService)
        {
            _allocationService = allocationService;
        }

        [HttpGet("device-by-serial")]
        public async Task<IActionResult> GetDeviceBySerial([FromQuery] string serial, CancellationToken ct)
        {
            var device = await _allocationService.FindDeviceBySerialAsync(serial, ct);
            if (device == null) return NotFound(new { message = "Device not found in Phase 2." });

            // Shape a DTO to avoid circular refs
            var result = new
            {
                device.Id,
                device.SerialNumber,
                Model = device.Model,          // adjust to actual property names
                SchoolName = device.School?.Name,
                Stage = device.Stage.ToString()
            };

            var locations = await _allocationService.GetRecentLocationsForDeviceAsync(device.Id, 5, ct);

            return Ok(new
            {
                Device = result,
                Locations = locations.Select(x => new
                {
                    x.Id,
                    x.Building,
                    x.Room,
                    x.Rack,
                    x.Shelf,
                    x.Bin,
                    x.Status,
                    CreatedAt = x.CreatedAt
                })
            });
        }

        public class AllocateRequest
        {
            public int Phase2DeviceId { get; set; }
            public string Building { get; set; }
            public string Room { get; set; }
            public string Rack { get; set; }
            public string Shelf { get; set; }
            public string Bin { get; set; }
            public string Notes { get; set; }
        }

        [HttpPost("allocate")]
        public async Task<IActionResult> Allocate([FromBody] AllocateRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var location = await _allocationService.AllocateAsync(
                request.Phase2DeviceId,
                request.Building,
                request.Room,
                request.Rack,
                request.Shelf,
                request.Bin,
                request.Notes,
                ct);

            return Ok(new
            {
                location.Id,
                location.Building,
                location.Room,
                location.Rack,
                location.Shelf,
                location.Bin,
                location.Status,
                location.CreatedAt
            });
        }
    }
}
```

**Program.cs** – make sure service + controller are wired:

```csharp
// DI
builder.Services.AddScoped<AllocationService>();

// Controllers are auto-discovered if you're using AddControllers(). 
// If Phase 2 is in an Area, keep existing routing as is.
```

---

## 5. Frontend – Sidebar for Allocation

**File:** `wwwroot/phase2/allocator/sidebar.html`

> Cursor: copy the **Phase 2 sidebar look and feel** (same classes, branding) and just adjust links.

Example structure (replace CSS classes with your existing ones):

```html
<nav class="sidebar">
    <div class="sidebar-header">
        <span class="sidebar-title">ICT Allocation</span>
    </div>
    <ul class="sidebar-menu">
        <li class="active">
            <a href="/phase2/allocator/index.html">Allocate Storage</a>
        </li>
        <li>
            <a href="/phase2/allocator/storage-overview.html">Storage Overview</a>
        </li>
        <li>
            <a href="/phase2/allocator/unallocated-devices.html">Unallocated Devices</a>
        </li>
        <li>
            <a href="/phase2/index.html">← Back to Phase 2 Home</a>
        </li>
    </ul>
</nav>
```

Then in `index.html` for the allocator, include this as a partial the same way other Phase 2 pages include their sidebar (e.g., through a `<div data-include=".../sidebar.html"></div>` or server-side partial, depending on how Phase 2 is currently doing it).

---

## 6. Frontend – Allocator page layout

**File:** `wwwroot/phase2/allocator/index.html`

> Cursor: take the **existing allocator dashboard markup** (the screenshot you saw) and wrap it in a 2-column layout: sidebar + main content. Keep the heading “Allocate Physical Storage Locations”.

Minimal structure:

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <title>ICT Allocation Dashboard - Phase 2</title>
    <link rel="stylesheet" href="/css/site.css" />
    <!-- include same CSS/JS as other Phase 2 pages -->
</head>
<body>
    <div class="layout">
        <!-- Sidebar -->
        <div id="sidebar-container"></div>

        <!-- Main content -->
        <main class="content">
            <header class="page-header">
                <h1>Allocate Physical Storage Locations</h1>
                <a href="/phase2/index.html" class="btn btn-outline-secondary">Back to Phase 2</a>
            </header>

            <!-- 1. Search Device -->
            <section class="card">
                <h2>1. Search Device</h2>
                <form id="searchForm">
                    <label for="serialInput">Serial Number</label>
                    <div class="input-group">
                        <input id="serialInput" class="form-control" placeholder="Scan or type serial" />
                        <button type="submit" class="btn btn-primary">Search</button>
                    </div>
                    <p class="hint">
                        Devices must exist in both Phase 2 and the core Device list with a linked school before allocation can be recorded.
                    </p>
                </form>
            </section>

            <!-- 2. Device Summary -->
            <section id="deviceSummarySection" class="card" style="display:none;">
                <h2>2. Device Summary</h2>
                <div id="deviceSummary"></div>
            </section>

            <!-- 3. Allocate Storage -->
            <section id="allocateSection" class="card" style="display:none;">
                <h2>3. Allocate Storage</h2>
                <form id="allocateForm">
                    <input type="hidden" id="phase2DeviceId" />
                    <div class="row">
                        <div class="col-md-4">
                            <label>Building</label>
                            <input class="form-control" id="buildingInput" />
                        </div>
                        <div class="col-md-4">
                            <label>Room</label>
                            <input class="form-control" id="roomInput" />
                        </div>
                        <div class="col-md-4">
                            <label>Rack</label>
                            <input class="form-control" id="rackInput" />
                        </div>
                    </div>

                    <div class="row mt-3">
                        <div class="col-md-4">
                            <label>Shelf</label>
                            <input class="form-control" id="shelfInput" />
                        </div>
                        <div class="col-md-4">
                            <label>Bin</label>
                            <input class="form-control" id="binInput" />
                        </div>
                        <div class="col-md-4">
                            <label>Notes</label>
                            <input class="form-control" id="notesInput" />
                        </div>
                    </div>

                    <div class="mt-3">
                        <button type="submit" class="btn btn-success">Save Allocation</button>
                    </div>
                </form>
            </section>

            <!-- 4. Recent Storage History -->
            <section id="historySection" class="card" style="display:none;">
                <h2>4. Recent Storage History</h2>
                <table class="table">
                    <thead>
                        <tr>
                            <th>When</th>
                            <th>Building</th>
                            <th>Room</th>
                            <th>Rack</th>
                            <th>Shelf</th>
                            <th>Bin</th>
                            <th>Status</th>
                        </tr>
                    </thead>
                    <tbody id="historyTableBody"></tbody>
                </table>
            </section>
        </main>
    </div>

    <script src="/js/helpers/include-partials.js"></script>
    <script>
        // Load sidebar partial into #sidebar-container
        includePartial("#sidebar-container", "/phase2/allocator/sidebar.html");
    </script>
    <script src="/js/phase2/allocator.js"></script>
</body>
</html>
```

> Cursor: replace `includePartial` with whatever mechanism Phase 2 uses to inject partials (you already have this for the standard Phase 2 sidebar).

---

## 7. Frontend Logic – allocator.js

**File:** `wwwroot/js/phase2/allocator.js`

```javascript
(async function () {
    const searchForm = document.getElementById("searchForm");
    const serialInput = document.getElementById("serialInput");
    const deviceSummarySection = document.getElementById("deviceSummarySection");
    const deviceSummary = document.getElementById("deviceSummary");
    const allocateSection = document.getElementById("allocateSection");
    const historySection = document.getElementById("historySection");
    const historyTableBody = document.getElementById("historyTableBody");
    const allocateForm = document.getElementById("allocateForm");
    const phase2DeviceIdInput = document.getElementById("phase2DeviceId");

    function showError(message) {
        alert(message); // replace with your toast/alert component
    }

    searchForm.addEventListener("submit", async function (e) {
        e.preventDefault();
        const serial = serialInput.value.trim();
        if (!serial) {
            showError("Please enter a serial number.");
            return;
        }

        try {
            const res = await fetch(`/api/phase2/allocation/device-by-serial?serial=${encodeURIComponent(serial)}`);
            if (res.status === 404) {
                showError("Device not found in Phase 2.");
                deviceSummarySection.style.display = "none";
                allocateSection.style.display = "none";
                historySection.style.display = "none";
                return;
            }
            if (!res.ok) {
                showError("Error searching device.");
                return;
            }

            const data = await res.json();
            const d = data.device || data.Device;

            phase2DeviceIdInput.value = d.id;

            deviceSummary.innerHTML = `
                <div><strong>Serial:</strong> ${d.serialNumber}</div>
                <div><strong>Model:</strong> ${d.model ?? ""}</div>
                <div><strong>School:</strong> ${d.schoolName ?? ""}</div>
                <div><strong>Stage:</strong> ${d.stage}</div>
            `;

            deviceSummarySection.style.display = "";
            allocateSection.style.display = "";
            historySection.style.display = "";

            // Render history
            historyTableBody.innerHTML = "";
            const locations = data.locations || data.Locations || [];
            locations.forEach(loc => {
                const tr = document.createElement("tr");
                tr.innerHTML = `
                    <td>${new Date(loc.createdAt).toLocaleString()}</td>
                    <td>${loc.building ?? ""}</td>
                    <td>${loc.room ?? ""}</td>
                    <td>${loc.rack ?? ""}</td>
                    <td>${loc.shelf ?? ""}</td>
                    <td>${loc.bin ?? ""}</td>
                    <td>${loc.status ?? ""}</td>
                `;
                historyTableBody.appendChild(tr);
            });
        } catch (err) {
            console.error(err);
            showError("Unexpected error while searching device.");
        }
    });

    allocateForm.addEventListener("submit", async function (e) {
        e.preventDefault();
        const payload = {
            phase2DeviceId: parseInt(phase2DeviceIdInput.value, 10),
            building: document.getElementById("buildingInput").value,
            room: document.getElementById("roomInput").value,
            rack: document.getElementById("rackInput").value,
            shelf: document.getElementById("shelfInput").value,
            bin: document.getElementById("binInput").value,
            notes: document.getElementById("notesInput").value
        };

        if (!payload.phase2DeviceId) {
            showError("Search for a device first.");
            return;
        }

        try {
            const res = await fetch("/api/phase2/allocation/allocate", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json"
                },
                body: JSON.stringify(payload)
            });

            if (!res.ok) {
                showError("Failed to save allocation.");
                return;
            }

            // Re-run search to refresh history + summary
            searchForm.dispatchEvent(new Event("submit"));
        } catch (err) {
            console.error(err);
            showError("Unexpected error while saving allocation.");
        }
    });
})();
```

> Cursor: adjust property names (`serialNumber`, `model`, `schoolName`) to match actual JSON returned by the controller.

---

## 8. Quick Test Checklist

Once Cursor finishes the changes:

1. **Build & Migrate**

   * `dotnet build` (solution root)
   * `dotnet ef database update --context Phase2DbContext`

2. **Run app**

   * `dotnet run --environment Development`

3. **Manual tests**

   * Go to `/phase2/allocator/index.html`
   * Confirm:

     * Sidebar appears with:

       * Allocate Storage (active)
       * Storage Overview
       * Unallocated Devices
       * Back to Phase 2
     * Search by a Phase 2 device serial that exists.
   * Expect:

     * Device summary shows correct school, model, stage.
     * Allocate Storage form is shown.
     * On Save:

       * No JS errors.
       * A new row appears in **Recent Storage History** with new location.
       * You can re-search the same serial and see history persist.

4. **Edge cases**

   * Search serial that **doesn’t exist** → get nice “Device not found in Phase 2.”
   * Leave serial empty → front-end validation.
   * Save with only Building/Room (others blank) → still works.

---

If you want, next step we can extend the pack to:

* **Storage Overview page** (group by Building/Room, counts).
* **Unallocated Devices** (Phase2 devices with no active DeviceStorageLocation).

But this pack should be enough to tell Cursor *exactly* what to do now for:

> 🔍 search → 📦 allocate → 📜 see history, inside a proper Phase 2 sidebar layout.
