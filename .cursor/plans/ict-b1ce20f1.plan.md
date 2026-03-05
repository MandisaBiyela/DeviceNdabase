<!-- b1ce20f1-e04e-4e0b-9534-3b9eca6dcef3 5e233443-d4aa-46ba-be64-704947b41133 -->
# ICT Allocator Enhanced Dashboard - Early Pipeline Integration

## Critical Workflow Context

**Allocation happens EARLY in the pipeline:**

- Right after scan-in (Stage: `Receipting`) OR
- Right after pre-assessment (Stage: `PreAssessment`)
- **Allocation is ORTHOGONAL to process stages** - it does NOT change the device stage
- Allocation is blocked only when device has left ICT (Dispatch, Disposal, etc.)

## Architecture

- **Core Locations** (`DeviceDeskDbContext`): `StorageLocation`/`DeviceLocation` for high-level zone tracking
- **Phase 2 Storage** (`Phase2DbContext`): `DeviceStorageLocation` for detailed physical location (Building/Room/Rack/Shelf/Bin)
- Both models work together: core location links to Phase 2 storage details
- Storage fields track physical location independently of stage

## Implementation Steps

### 1. Data Model - Phase 2 Storage Details

**File:** `Modules/Phase2/Models/DeviceStorageLocation.cs` (NEW)

- Entity: `Phase2DeviceId`, `StorageLocationId` (optional), `Building`, `Room`, `Rack`, `Shelf`, `Bin`, `Notes`, `Status` (Active/Moved/Archived), `CreatedAt`, `UpdatedAt`, `CreatedByUserId`
- Foreign key to `Phase2Device`
- Unique constraint: one Active record per Phase2Device

**File:** `Modules/Phase2/Data/Phase2DbContext.cs` (UPDATE)

- Add `DbSet<DeviceStorageLocation> DeviceStorageLocations`
- Configure relationships, indexes, unique constraint

**Migration:** `dotnet ef migrations add Phase2_DeviceStorageLocation --context Phase2DbContext`

### 2. Service Layer - Allocation Service

**File:** `Modules/Phase2/Services/AllocationService.cs` (NEW)

- `FindDeviceBySerialAsync()` - search in Phase2DbContext
- `GetPhase2StorageAsync()` - get detailed storage for Phase2Device
- `AllocatePhase2StorageAsync()` - create/update DeviceStorageLocation
- **VALIDATION**: Only allow if Stage ∈ {Receipting, PreAssessment, Assessment, Repair, Quality}
- **DO NOT CHANGE STAGE** - allocation is orthogonal
- Block if Stage ∈ {Dispatch, AwaitingDispatch, Disposal, etc.}
- `ClearAllocationAsync()` - clear storage (only if device still in ICT)
- `GetPendingAllocationsAsync()` - **PRIORITY**: devices needing allocation (Stage ∈ {Receipting, PreAssessment} AND StorageArea IS NULL)
- `GetStorageOverviewAsync()` - aggregate counts by location
- `GetUnallocatedDevicesAsync()` - Phase2Devices without DeviceStorageLocation
- `GetSchoolsInStorageAsync()` - schools with devices in storage

### 3. API Controller - Enhanced Allocation Controller

**File:** `Modules/Phase2/Controllers/AllocationController.cs` (UPDATE)

- Keep existing endpoints (`/search`, `/locations`, `/move`)
- Update `/move` to validate stage before allowing allocation
- Add new endpoints:
- `GET /api/phase2/allocation/pending` - **PRIORITY**: main work queue
- `POST /api/phase2/allocation/allocate-detailed` - allocate with Building/Room/Rack/Shelf/Bin (validates stage, doesn't change it)
- `POST /api/phase2/allocation/clear` - clear allocation (only if device still in ICT)
- `GET /api/phase2/allocation/storage-overview` - location counts
- `GET /api/phase2/allocation/unallocated` - devices without storage
- `GET /api/phase2/allocation/schools-in-storage` - schools with stored devices

### 4. Frontend - Sidebar Navigation

**File:** `Modules/Phase2/UI/index.html` (UPDATE)

- Update `IctAllocator` sidebar (lines 133-146) to include:
- **Pending Allocations** (main work queue - default view)
- Allocate Storage (search by serial)
- Storage Overview
- Unallocated Devices
- Schools in Storage
- Add view loaders: `loadPendingAllocationsView()`, `loadAllocationView()`, `loadStorageOverviewView()`, `loadUnallocatedDevicesView()`, `loadSchoolsInStorageView()`
- Default view for IctAllocator: Pending Allocations

### 5. Frontend - New UI Pages

**File:** `Modules/Phase2/UI/pending-allocations.html` (NEW - PRIORITY)

- Main work queue for Allocator
- List devices: Stage ∈ {Receipting, PreAssessment} AND no storage
- Show: Serial, Stage, School, Model, ReceivingDate
- Quick action: "Allocate Now" → opens allocation form
- Auto-refresh or manual refresh

**File:** `Modules/Phase2/UI/allocator-dashboard.html` (UPDATE existing `ict-allocation-dashboard.html`)

- Support both core location selection AND detailed storage fields
- Add Building/Room/Rack/Shelf/Bin fields alongside LocationCode dropdown
- Show current stage and confirm allocation won't change it
- Display warning if device is in Dispatch/Disposal stage (allocation blocked)

**File:** `Modules/Phase2/UI/storage-overview.html` (NEW)

- Table/grid showing all StorageLocations with device counts
- Group by Area, School, Category
- Filter/search capabilities

**File:** `Modules/Phase2/UI/unallocated-devices.html` (NEW)

- List Phase2Devices without DeviceStorageLocation
- Show serial, stage, school info
- Filter by stage (Receipting, PreAssessment)
- Quick action to allocate storage

**File:** `Modules/Phase2/UI/schools-in-storage.html` (NEW)

- List schools with devices in storage
- Show device counts per school
- Show breakdown by stage

### 6. Dependency Injection

**File:** `Program.cs` (UPDATE)

- Register `AllocationService` if not already registered
- Ensure both contexts available to services

### 7. Testing & Validation

- Test allocation at Receipting/PreAssessment stages (doesn't change stage)
- Test allocation blocked at Dispatch/Disposal stages
- Verify Pending Allocations queue shows correct devices
- Test navigation between all pages
- Ensure data consistency between both contexts

## Key Design Decisions

1. **Early Pipeline Integration**: Allocation happens at Receipting/PreAssessment, NOT after QA
2. **Stage Independence**: Allocation does NOT change device stage - it's orthogonal to process flow
3. **Validation Rules**: Allow allocation in {Receipting, PreAssessment, Assessment, Repair, Quality}, block in {Dispatch, Disposal, etc.}
4. **Work Queue First**: Pending Allocations page is the primary work queue for Allocator role
5. **Dual Model Approach**: Core locations for high-level tracking, Phase 2 storage for detailed physical location
6. **Backward Compatibility**: Existing allocation flow continues to work, enhanced with optional detailed fields

## Files to Create/Modify

- **NEW**: `Modules/Phase2/Models/DeviceStorageLocation.cs`
- **NEW**: `Modules/Phase2/Services/AllocationService.cs`
- **NEW**: `Modules/Phase2/UI/pending-allocations.html` (PRIORITY)
- **NEW**: `Modules/Phase2/UI/storage-overview.html`
- **NEW**: `Modules/Phase2/UI/unallocated-devices.html`
- **NEW**: `Modules/Phase2/UI/schools-in-storage.html`
- **UPDATE**: `Modules/Phase2/Data/Phase2DbContext.cs`
- **UPDATE**: `Modules/Phase2/Controllers/AllocationController.cs`
- **UPDATE**: `Modules/Phase2/UI/index.html`
- **UPDATE**: `Modules/Phase2/UI/ict-allocation-dashboard.html`
- **UPDATE**: `Program.cs` (if needed)

### To-dos

- [ ] > **Allocation is an ICT task that happens as soon as devices arrive in the ICT centre (scan-in), either:**