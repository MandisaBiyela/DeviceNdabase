# Device Allocation Feature - Testing Guide

## ✅ Build Status
- **Compilation**: Success
- **Linter**: No errors
- **Status**: Ready for testing

## 🔧 Setup Required

### 1. Create Database Migration
The application is currently running, so you'll need to:

```powershell
# Stop the application first, then:
cd DeviceDesk
dotnet ef migrations add Device_AddAllocationFields --context DeviceDeskDbContext
dotnet ef database update
```

### 2. Restart Application
After migration is applied, restart the application.

## 🧪 Testing Scenarios

### Scenario 1: RnR Workflow - Allocate Single Device

**Endpoint**: `POST /api/phase1/rnr/batches/{batchId}/allocate-device`

**Request Body** (Student):
```json
{
  "deviceId": "your-device-guid-here",
  "allocationType": 1,
  "studentName": "John Doe",
  "studentIdNumber": "20240001"
}
```

**Request Body** (Teacher):
```json
{
  "deviceId": "your-device-guid-here",
  "allocationType": 2,
  "teacherName": "Jane Smith",
  "teacherPersalNumber": "1234567"
}
```

**Expected Response**:
```json
{
  "success": true,
  "message": "Device allocation saved"
}
```

### Scenario 2: RnR Workflow - Bulk Allocation

**Endpoint**: `POST /api/phase1/rnr/batches/{batchId}/allocate-bulk`

**Request Body**:
```json
{
  "batchId": "batch-guid-here",
  "allocations": [
    {
      "deviceId": "device-1-guid",
      "allocationType": 1,
      "studentName": "Student One",
      "studentIdNumber": "20240001"
    },
    {
      "deviceId": "device-2-guid",
      "allocationType": 2,
      "teacherName": "Teacher One",
      "teacherPersalNumber": "1234567"
    }
  ]
}
```

**Expected Response**:
```json
{
  "success": true,
  "message": "Bulk allocation saved for 2 devices"
}
```

### Scenario 3: Get Current Allocations

**Endpoint**: `GET /api/phase1/rnr/batches/{batchId}/allocations`

**Expected Response**:
```json
[
  {
    "deviceId": "device-guid",
    "allocationType": 1,
    "studentName": "John Doe",
    "studentIdNumber": "20240001",
    "teacherName": null,
    "teacherPersalNumber": null
  }
]
```

### Scenario 4: New Stock Workflow

Same endpoints with different base path:
- `POST /api/phase1/newstock/batches/{batchId}/allocate-device`
- `POST /api/phase1/newstock/batches/{batchId}/allocate-bulk`
- `GET /api/phase1/newstock/batches/{batchId}/allocations`

## 🧪 Database Verification

After allocation, check the `Devices` table:

```sql
SELECT 
    Id,
    SerialNumber,
    AllocationType,
    StudentName,
    StudentIdNumber,
    TeacherName,
    TeacherPersalNumber,
    AllocatedAt,
    AllocatedByUserId
FROM Devices
WHERE AllocationType > 0;
```

**Expected Results**:
- `AllocationType` = 0 (None), 1 (Student), or 2 (Teacher)
- For Student: `StudentName` and `StudentIdNumber` populated, Teacher fields null
- For Teacher: `TeacherName` and `TeacherPersalNumber` populated, Student fields null
- `AllocatedAt` timestamp present
- `AllocatedByUserId` contains user who made the allocation

## 🎯 Business Rules to Verify

### ✅ Rule 1: Exclusive Allocation
When a device is allocated to a student:
- ✅ `AllocationType` = 1
- ✅ `StudentName` and `StudentIdNumber` are set
- ✅ `TeacherName` and `TeacherPersalNumber` are NULL

When switched to teacher:
- ✅ `AllocationType` = 2
- ✅ `TeacherName` and `TeacherPersalNumber` are set
- ✅ `StudentName` and `StudentIdNumber` are NULL

### ✅ Rule 2: Optional Allocation
- ✅ Devices with `AllocationType` = 0 (None) should work normally
- ✅ RnR/New Stock workflows should not be blocked if allocation is empty

### ✅ Rule 3: Audit Trail
- ✅ `AllocatedAt` timestamp is recorded
- ✅ `AllocatedByUserId` captures who made the allocation
- ✅ Changes are logged (check application logs)

## 🖥️ UI Testing (JavaScript)

### Using Browser Console

1. Navigate to RnR verification page: `/phase1/rnr-verification.html?batchId=YOUR_BATCH_ID`

2. Open browser console (F12)

3. Test rendering allocation controls:
```javascript
// Render allocation form for a device
const html = DeviceAllocation.renderAllocationControls('device-guid', null);
console.log(html);

// Wire up event handlers
const container = document.querySelector('#some-container');
DeviceAllocation.wireUp(container);

// Collect allocations
const allocations = DeviceAllocation.collectAllocations(container);
console.log(allocations);

// Format for display
const device = {
    allocationType: 1,
    studentName: 'John Doe',
    studentIdNumber: '20240001'
};
console.log(DeviceAllocation.formatAllocationDisplay(device));
```

## 📊 Test Data Setup

### Create Test Devices (SQL)

```sql
-- Create a test device if needed
INSERT INTO Devices (Id, SerialNumber, Source, SchoolId, SchoolName, ImportedAt, BatchId)
VALUES 
    (NEWID(), 'TEST-SERIAL-001', 'RNR', 1, 'Test School', GETUTCDATE(), NULL);

-- Get the device ID for testing
SELECT Id, SerialNumber FROM Devices WHERE SerialNumber = 'TEST-SERIAL-001';
```

### Create Test RnR Batch (if needed)

Check Phase 0 tables for existing batches:
```sql
SELECT TOP 5 
    BatchId, 
    BatchNumber, 
    CollectionSlipNumber, 
    SchoolName, 
    Status 
FROM RnrBatches 
ORDER BY CreatedAt DESC;
```

## 🐛 Common Issues & Solutions

### Issue: "Device not found"
- **Cause**: Device GUID doesn't exist or wrong batch
- **Solution**: Verify device exists in Devices table and belongs to the batch

### Issue: "Build failed" when running migration
- **Cause**: Application is still running and has locks on files
- **Solution**: Stop the application completely before running migration

### Issue: 500 error on allocation endpoint
- **Cause**: Missing DeviceDeskDbContext injection in RnrGrvService
- **Solution**: Already fixed in implementation - ensure latest build

### Issue: Allocation not persisting
- **Cause**: Migration not applied yet
- **Solution**: Run `dotnet ef database update` as shown above

## ✅ Success Criteria

The implementation is successful when:
- [x] Code compiles without errors
- [ ] Database migration applies successfully
- [ ] Single device allocation works (both Student and Teacher)
- [ ] Bulk allocation saves multiple devices
- [ ] GET allocations returns correct data
- [ ] Switching allocation type clears previous fields
- [ ] Works for both RnR and New Stock workflows
- [ ] JavaScript components render and collect data correctly
- [ ] Display functions show allocation info properly

## 📝 Next Steps

1. **Stop the application** to release file locks
2. **Run the migration** to add database columns
3. **Restart the application**
4. **Test using the scenarios above**
5. **Verify database records** match expected values
6. **Test UI integration** on verification pages

---

**Implementation Files Modified**:
- ✅ `Infrastructure/Data/DeviceDeskDbContext.cs` - Model & Enum
- ✅ `Modules/Phase1/Models/AllocationModels.cs` - DTOs
- ✅ `Modules/Phase1/Services/RnrGrvService.cs` - Business logic
- ✅ `Modules/Phase1/Controllers/RnrReceivingController.cs` - RnR endpoints
- ✅ `Modules/Phase1/Controllers/NewStockScanningController.cs` - New Stock endpoints
- ✅ `wwwroot/shared/js/device-allocation.js` - JavaScript module
- ✅ `Modules/Phase1/UI/rnr-verification.html` - Script inclusion

**Ready for Production Testing** 🚀

