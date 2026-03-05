# DeviceDesk - New Stock Upload Migration Summary
**Date:** November 4, 2025  
**Purpose:** Enable order-style CSV uploads without Serial/IMEI for Phase 0 New Stock

---

## 📦 2025-11-19 – ICT Allocation Storage Tables

To support the Phase 2 ICT allocator workflow, run the new core migration and seed at least one storage zone per school + device category.

### Migration

1. Ensure the new migration exists locally: `20251119074926_AddStorageLocationsAndDeviceLocations`.
2. Apply it to the core database:
   ```bash
   dotnet ef database update -c DeviceDeskDbContext
   ```
   This adds:
   - `StorageLocations`
   - `DeviceLocations`
   - `DeviceLocationHistory`
   - `Devices.Category` column (defaults to `DeviceCategory.Unknown`)

### Sample StorageLocation Seed

```csharp
if (!await coreDb.StorageLocations.AnyAsync())
{
    var school = await coreDb.Schools.FirstOrDefaultAsync(s => s.EmisCode == "500123");

    coreDb.StorageLocations.AddRange(
        new StorageLocation
        {
            Name = "ICT Staging",
            LocationCode = "ICT-STAGING",
            Category = DeviceCategory.Unknown,
            Area = StorageArea.Phase2IctCenter,
            IsActive = true
        },
        new StorageLocation
        {
            Name = "Laptop Zone A",
            LocationCode = "EMIS500123-LAP-A01",
            SchoolId = school?.SchoolId,
            Category = DeviceCategory.Laptop,
            Area = StorageArea.Phase2IctCenter,
            IsActive = true
        }
    );

    await coreDb.SaveChangesAsync();
}
```

Assign the appropriate `DeviceCategory` on each `Device` record (defaults to `Unknown`) so location filtering returns the right shelves.

---

## 📊 Database Changes

### Tables Modified

#### 1. **Devices Table**
Added 3 new columns:
```sql
ALTER TABLE [dbo].[Devices] ADD [DeviceType] NVARCHAR(50) NULL;
ALTER TABLE [dbo].[Devices] ADD [Description] NVARCHAR(500) NULL;
ALTER TABLE [dbo].[Devices] ADD [OrderNumber] NVARCHAR(50) NULL;
```

**Purpose:**
- `DeviceType`: Laptop, Desktop, Tablet, Chromebook, Other
- `Description`: Device description from CSV
- `OrderNumber`: Links device to order from Phase 0

#### 2. **DeviceImportBatch Table**
Added 1 new column:
```sql
ALTER TABLE [dbo].[DeviceImportBatch] ADD [OrderNumber] NVARCHAR(50) NULL;
```

**Purpose:**
- Links entire batch to an order number

### Migration Files
- `Migrations/AddOrderStyleFields.sql` - Devices table updates
- `Migrations/AddOrderStyleFields_v2.sql` - DeviceImportBatch table updates

---

## 🔧 Code Changes

### Backend (C#)

#### 1. **DeviceDeskDbContext.cs**
**Location:** `Infrastructure/Data/DeviceDeskDbContext.cs`

**Device Model Updated:**
```csharp
public class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? SerialNumber { get; set; }
    public string? IMEI { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? DeviceType { get; set; }        // NEW
    public string? Description { get; set; }       // NEW
    public string? OrderNumber { get; set; }       // NEW
    public string Source { get; set; } = "RNR";
    public long? SchoolId { get; set; }
    public DateTimeOffset ImportedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? BatchId { get; set; }
}
```

**DeviceImportBatch Model Updated:**
```csharp
public class DeviceImportBatch
{
    public Guid BatchId { get; set; } = Guid.NewGuid();
    public string Source { get; set; } = "RNR";
    public long? SchoolId { get; set; }
    public string? FileName { get; set; }
    public string? OrderNumber { get; set; }       // NEW
    public int Total { get; set; }
    public int Added { get; set; }
    public int Duplicates { get; set; }
    public int Invalid { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
```

#### 2. **CsvImportService.cs**
**Location:** `Modules/Phase0/Services/CsvImportService.cs`

**Key Changes:**
- Detects CSV format (device-style vs order-style)
- Supports both Serial/IMEI and OrderNumber/DeviceType/Quantity formats
- Generates unique placeholder serials using GUID
- Populates new fields (DeviceType, Description, OrderNumber)

**Placeholder Serial Format:**
```
PENDING-{OrderNumber}-{DeviceType}-{UniqueGUID}
Example: PENDING-0002-Laptop-A1B2C3D4
```

#### 3. **NewStockIntakeController.cs**
**Location:** `Modules/Phase0/Controllers/NewStockIntakeController.cs`

**New Endpoints Added:**
- `GET /api/phase0/new/orders` - List batches as orders for Phase 1
- `GET /api/phase0/new/orders/{id}` - Get specific batch details

**Manual Entry Updated:**
- Supports order-style entries (DeviceType + Quantity)
- Generates unique placeholder serials
- Populates DeviceType field

#### 4. **SpreadsheetParserService.cs**
**Location:** `Modules/Phase1/Services/SpreadsheetParserService.cs`

**Key Changes:**
- Added logging support
- Detects batch type (New Stock vs Traditional RnR)
- Makes Serial optional when DeviceType + Quantity present
- Comprehensive validation and error reporting

**DeviceRow Model Updated:**
```csharp
public class DeviceRow
{
    public string? Serial { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? Description { get; set; }
    public string? OrderNumber { get; set; }       // NEW
    public string? DeviceType { get; set; }        // NEW
    public int? Quantity { get; set; }             // NEW
    public int RowNumber { get; set; }
}
```

#### 5. **NewStockImportRow.cs** (NEW FILE)
**Location:** `Modules/Phase1/Models/NewStockImportRow.cs`

```csharp
public class NewStockImportRow
{
    public string OrderNumber { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int RowNumber { get; set; }
}
```

#### 6. **EF Migration: Phase0_AddOrderStyleFields_Safe** (NEW)
**Location:** `Migrations/20251105140000_Phase0_AddOrderStyleFields_Safe.cs`

**What it does:**
- Adds `DeviceType`, `Description`, `OrderNumber` to `Devices` if missing
- Adds `OrderNumber` to `DeviceImportBatch` if missing
- Creates helpful indexes on `OrderNumber` (filtered `IS NOT NULL`)
- Conditionally renames legacy `Batches` → `DeviceImportBatch` if present

**Why:**
- Fixes “Invalid column name 'OrderNumber' / 'DeviceType'” on fresh databases
- Idempotent and safe to run on any environment

**How it works:** Uses conditional T-SQL inside `Up()` so it won’t fail if parts already exist.

### Frontend (JavaScript)

#### 1. **new.js**
**Location:** `Modules/Phase0/UI/js/new.js`

**Key Changes:**
- Replaced frontend CSV parsing with backend API call
- Now uploads file to `/api/phase0/new/import`
- Backend handles all parsing and validation

**Before:**
```javascript
// Parsed CSV in browser
const text = await f.text();
// ... complex parsing logic
```

**After:**
```javascript
// Upload to backend
const formData = new FormData();
formData.append('file', f);
const res = await fetch(`${location.origin}/api/phase0/new/import`, {
    method: 'POST',
    body: formData
});
```

#### 2. **new.html**
**Location:** `Modules/Phase0/UI/new.html`

**Key Changes:**
- Updated version parameter to force cache refresh (`?v=5`)
- Updated instructions to mention OrderNumber support

#### 3. **sample-new.csv**
**Location:** `Modules/Phase0/UI/sample-new.csv`

**Updated Format:**
```csv
OrderNumber,Brand,Model,DeviceType,Description,Quantity
0001,HP,EliteBook 840,Laptop,14-inch business laptop,10
0001,Dell,Latitude 5420,Laptop,Enterprise laptop,15
0001,Lenovo,ThinkPad X1,Laptop,Ultralight laptop,8
```

---

## 📋 Supported CSV Formats

### Format 1: Order-Style (NEW)
**For Phase 0 New Stock uploads**
```csv
OrderNumber,Brand,Model,DeviceType,Description,Quantity
0002,Asus,TUF Gaming A15,Laptop,Gaming laptop,7
0002,Lenovo,Yoga 7i,2-in-1,Convertible laptop,5
```

**Behavior:**
- Creates multiple devices based on Quantity
- Generates placeholder serials: `PENDING-0002-Laptop-A1B2C3D4`
- Populates DeviceType, Description, OrderNumber
- Serial is optional

### Format 2: Device-Style (Traditional)
**For RnR and legacy uploads**
```csv
Serial,Brand,Model,IMEI,Description
SN001,HP,EliteBook 840,,Business laptop
SN002,Dell,Latitude 5420,,Enterprise laptop
```

**Behavior:**
- Creates one device per row
- Serial or IMEI required
- Traditional validation

---

## 🔄 Complete Workflow

### Phase 0: Upload
1. User uploads CSV with OrderNumber, Brand, Model, DeviceType, Description, Quantity
2. `CsvImportService` detects order-style format
3. Creates devices with placeholder serials
4. Saves to `Devices` and `DeviceImportBatch` tables
5. Returns success with counts

### Phase 1: Scanning
1. User selects "New Stock" source
2. System calls `/api/phase0/new/orders` to list available batches
3. User selects an order (e.g., "Order 0002")
4. System shows expected devices grouped by Brand/Model/DeviceType
5. User scans actual device serials
6. System replaces placeholder serials with actual serials
7. Batch marked as complete

---

## 🧪 Testing

### Test 1: Order-Style Upload
**File:** `Order_0002_Device_List.csv`
```csv
OrderNumber,Brand,Model,DeviceType,Description,Quantity
0002,Asus,TUF Gaming A15,Laptop,Gaming laptop,7
```

**Expected Result:**
- ✅ 7 devices created
- ✅ Each with unique serial: `PENDING-0002-Laptop-{GUID}`
- ✅ All have OrderNumber = "0002"
- ✅ All have DeviceType = "Laptop"

### Test 2: Device-Style Upload
**File:** `Traditional_Upload.csv`
```csv
Serial,Brand,Model,Description
SN001,HP,EliteBook 840,Business laptop
```

**Expected Result:**
- ✅ 1 device created
- ✅ SerialNumber = "SN001"
- ✅ DeviceType = NULL (optional for traditional)

### Test 3: Phase 1 Integration
**Endpoint:** `GET /api/phase0/new/orders`

**Expected Response:**
```json
[
  {
    "orderId": "guid...",
    "orderNumber": "0002",
    "supplierName": "Order 0002",
    "fileName": "Order_0002_Device_List.xlsx",
    "totalDevices": 20,
    "createdAt": "2025-11-04T16:01:28Z",
    "status": "Pending Scanning"
  }
]
```

---

## 🎯 Key Features

### 1. Dual Format Support
- ✅ Order-style (no serials, uses quantity)
- ✅ Device-style (with serials, traditional)

### 2. Unique Placeholder Serials
- ✅ GUID-based to prevent duplicates
- ✅ Contains OrderNumber and DeviceType for tracking
- ✅ Replaced during Phase 1 scanning

### 3. Comprehensive Logging
- ✅ Logs CSV header detection
- ✅ Logs column mapping
- ✅ Logs batch type detection
- ✅ Logs validation results

### 4. Phase 0 to Phase 1 Integration
- ✅ API endpoints to expose batches as "orders"
- ✅ Grouped device breakdown by Brand/Model/DeviceType
- ✅ Quantity expected vs scanned tracking

---

## 📝 API Endpoints Summary

### Phase 0 Endpoints
- `POST /api/phase0/new/import` - Upload CSV/Excel file
- `POST /api/phase0/new/import-manual` - Manual entry
- `GET /api/phase0/new/batches` - List batches
- `GET /api/phase0/new/batches/{id}/items` - Batch items
- `GET /api/phase0/new/orders` - List as orders for Phase 1
- `GET /api/phase0/new/orders/{id}` - Order details for Phase 1

### Phase 1 Endpoints (Existing)
- `GET /api/phase1/newstock/batches` - Pending batches
- `POST /api/phase1/newstock/batches/{id}/scan` - Scan device
- `POST /api/phase1/newstock/batches/{id}/confirm` - Confirm batch

---

## ✅ Migration Status

- ✅ Database schema updated
- ✅ Models updated
- ✅ Services updated
- ✅ Controllers updated
- ✅ Frontend updated
- ✅ Sample files updated
- ✅ API integration complete
- ✅ Tested and working

**Application is ready for production use!**
