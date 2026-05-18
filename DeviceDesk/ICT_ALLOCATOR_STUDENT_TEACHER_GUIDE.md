# ICT Allocator - Student/Teacher Allocation Guide

## ✅ Implementation Complete

**Date**: December 3, 2024  
**Location**: Phase 2 - ICT Allocator Dashboard  
**Role**: `IctAllocator`, `IctManager`, `Admin`

---

## 📦 What Was Implemented

### Backend Components

1. **Phase2AllocationModels.cs** ✅
   - `Phase2AllocationListItemDto` - Device list with allocation info
   - Properly maps Phase2Device fields (SchoolId, QaPassed)

2. **Phase2AllocationService.cs** ✅
   - `IPhase2AllocationService` interface
   - `AssignStudentTeacherAsync()` - Core allocation logic
   - Validation: QA-passed, not disposed, numeric persal
   - Audit logging
   - Already registered in DI (Program.cs line 142)

3. **AllocationController.cs** (Phase 2) ✅
   - `GET /api/phase2/allocation/ready-for-assignment` - Get QA-passed devices
   - `POST /api/phase2/allocation/devices/{id}/assign` - Assign allocation
   - Role-based auth: IctAllocator, IctManager, Admin

### Frontend Components

4. **ict-allocation-dashboard.html** ✅
   - Menu section added: "ASSIGNMENT"
   - "Student/Teacher Allocation" menu item
   - "Ready for Dispatch" menu item
   - Allocation modal already present
   - device-allocation.js script included

5. **ict-allocation-dashboard.js** ✅
   - `loadStudentTeacherAllocationView()` - Main view
   - `renderStudentTeacherAllocationTable()` - Table rendering
   - `openAllocationModal()` - Modal handler
   - `saveAllocation()` - API submission
   - `loadReadyForDispatchView()` - Future enhancement stub

---

## 🚀 How to Use

### Step 1: Login as ICT Allocator

Navigate to: `http://localhost:5000/login.html`

**Credentials**:
- Email: `ict.allocator@local`
- Password: `P@ssw0rd1!`

You'll be redirected to: `/phase2/index.html`

### Step 2: Access Student/Teacher Allocation

From the ICT Allocator dashboard sidebar, click:

**ASSIGNMENT** → **Student/Teacher Allocation**

### Step 3: View QA-Passed Devices

The page shows devices that:
- ✅ Have passed Quality Assessment (`QaPassed = true`)
- ✅ Are in `QualityAssessment` or `AwaitingDispatch` stage
- ✅ Are not disposed
- ✅ Don't have allocation yet (`AllocationType = None`)

### Step 4: Allocate a Device

1. Click **Allocate** button next to a device
2. Modal opens showing device details
3. Select allocation type:
   - **None** - No allocation
   - **Student** - Allocate to a student
   - **Teacher** - Allocate to a teacher
4. Fill in required fields:
   - **Student**: Name + ID Number
   - **Teacher**: Name + Persal Number (numeric only)
5. Click **Save Allocation**

### Step 5: Verify Allocation

- Device should disappear from the "ready for assignment" list
- Check database to verify:

```sql
SELECT 
    SerialNumber,
    AllocationType,
    StudentName,
    StudentIdNumber,
    TeacherName,
    TeacherPersalNumber,
    AllocatedAt,
    AllocatedByUserId
FROM Devices
WHERE SerialNumber = 'YOUR_SERIAL_HERE';
```

---

## 🧪 Testing Scenarios

### Test 1: Allocate to Student

**Steps**:
1. Login as `ict.allocator@local`
2. Navigate to Student/Teacher Allocation
3. Click Allocate on any device
4. Select "Student"
5. Enter:
   - Student Name: "John Doe"
   - Student ID Number: "20240001"
6. Save

**Expected**:
- Success message shown
- Device removed from list
- Database shows `AllocationType = 1`, student fields populated

### Test 2: Allocate to Teacher

**Steps**:
1. Same as above, but select "Teacher"
2. Enter:
   - Teacher Name: "Jane Smith"
   - Teacher Persal Number: "1234567" (numeric only)
3. Save

**Expected**:
- Success message shown
- Database shows `AllocationType = 2`, teacher fields populated
- Student fields are NULL

### Test 3: Validation - Non-Numeric Persal

**Steps**:
1. Select "Teacher"
2. Enter persal number: "ABC123" (contains letters)
3. Save

**Expected**:
- Error: "Persal number must be numeric"
- Allocation not saved

### Test 4: Device Not QA-Passed

**Setup**:
```sql
-- Create test device without QA pass
UPDATE Phase2Devices SET QaPassed = NULL WHERE Serial = 'TEST-001';
```

**Expected**:
- Device does NOT appear in ready-for-assignment list
- If trying to allocate via API: "Device must be QA-passed before allocation"

### Test 5: Disposed Device

**Setup**:
```sql
UPDATE Phase2Devices SET DisposalRequested = 1, Stage = 25 WHERE Serial = 'TEST-002';
```

**Expected**:
- Device does NOT appear in list
- API error: "Cannot allocate disposed devices"

---

## 📊 API Endpoints

### Get Ready Devices
```http
GET /api/phase2/allocation/ready-for-assignment
Authorization: Bearer {token}
Role: IctAllocator, IctManager, Admin
```

**Response**:
```json
[
  {
    "phase2DeviceId": 123,
    "serial": "SN-12345",
    "zone": "RnR",
    "stage": "QualityAssessment",
    "schoolId": 1,
    "schoolName": "Test School",
    "qaPassed": true,
    "allocationType": 0,
    "studentName": null,
    "studentIdNumber": null,
    "teacherName": null,
    "teacherPersalNumber": null
  }
]
```

### Assign Allocation
```http
POST /api/phase2/allocation/devices/123/assign
Authorization: Bearer {token}
Role: IctAllocator, IctManager, Admin
Content-Type: application/json

{
  "deviceId": "device-guid-from-core-devices",
  "allocationType": 1,
  "studentName": "John Doe",
  "studentIdNumber": "20240001"
}
```

**Response**:
```json
{
  "success": true,
  "message": "Allocation saved successfully"
}
```

---

## 🔍 Business Rules Enforced

### ✅ Validation Rules

1. **QA Requirement**: Device must have `QaPassed = true`
2. **Stage Requirement**: Device must be in `QualityAssessment` or `AwaitingDispatch`
3. **Disposal Block**: Cannot allocate if `DisposalRequested = true` or `Stage = Disposal`
4. **Exclusive Allocation**: Student OR Teacher, never both
5. **Persal Format**: Teacher persal number must be numeric
6. **Serial Lookup**: Phase2Device must have matching core Device record

### ✅ Data Integrity

- When allocating to Student: Teacher fields set to NULL
- When allocating to Teacher: Student fields set to NULL
- When setting to None: All allocation fields cleared
- Timestamp and user ID captured for audit trail
- Phase2 audit log entry created

---

## 🐛 Troubleshooting

### Issue: Menu items not visible

**Solution**: Verify you're logged in as `IctAllocator` role
```sql
SELECT u.Email, r.Name as Role
FROM AspNetUsers u
JOIN AspNetUserRoles ur ON u.Id = ur.UserId
JOIN AspNetRoles r ON ur.RoleId = r.Id
WHERE u.Email = 'ict.allocator@local';
```

### Issue: "No devices pending allocation"

**Possible causes**:
1. No devices have passed QA yet
2. All QA-passed devices are already allocated
3. Devices are in wrong stage

**Debug SQL**:
```sql
SELECT 
    Id, Serial, Zone, Stage, QaPassed, DisposalRequested
FROM Phase2Devices
WHERE QaPassed = 1 
  AND Stage IN (20, 26)  -- QualityAssessment, AwaitingDispatch
  AND (DisposalRequested IS NULL OR DisposalRequested = 0);
```

### Issue: "Core device record not found"

**Cause**: Phase2Device.Serial doesn't match any Device.SerialNumber

**Solution**: Ensure devices were properly created in core Devices table during Phase 1 receiving

### Issue: Modal doesn't open

**Check**:
1. Bootstrap JS is loaded
2. `device-allocation.js` is loaded
3. Browser console for JavaScript errors

---

## 📈 Integration Points

### With Existing Physical Storage Allocation

The ICT Allocator dashboard now has TWO allocation types:

1. **Physical Storage** (Existing):
   - WHERE is the device? (Building, Room, Rack, Shelf, Bin)
   - Used for: Inventory management, finding devices in warehouse

2. **Student/Teacher Assignment** (New):
   - WHO will receive the device? (Student name/ID or Teacher name/Persal)
   - Used for: School records, delivery documentation, accountability

Both are **independent** and can be set separately.

### With Phase 3 Dispatch

When devices move to Phase 3 Dispatch:
- Allocation info travels with the device (on core Devices table)
- Dispatch can query allocation for POD generation
- Future enhancement: Include allocation on delivery notes

---

## 📝 Database Schema Reference

### Core Device Table

```sql
-- Allocation fields added to Devices table
AllocationType INT DEFAULT 0,  -- 0=None, 1=Student, 2=Teacher
StudentName NVARCHAR(MAX) NULL,
StudentIdNumber NVARCHAR(MAX) NULL,
TeacherName NVARCHAR(MAX) NULL,
TeacherPersalNumber NVARCHAR(MAX) NULL,
AllocatedAt DATETIMEOFFSET NULL,
AllocatedByUserId NVARCHAR(MAX) NULL
```

### Phase2 Audit Log

```sql
-- Sample audit entry
INSERT INTO Phase2AuditLogs (DeviceId, DeviceSerial, UserId, Action, Details, Timestamp)
VALUES (123, 'SN-12345', 'ict.allocator@local', 'StudentTeacherAllocated', 
        'AllocatedType=Student; Serial=SN-12345; School=Test School', GETUTCDATE());
```

---

## ✨ Key Features

- **Role-Based**: Only IctAllocator, IctManager, and Admin can allocate
- **QA-Gated**: Only devices that passed quality checks
- **Audit Trail**: Full tracking of allocations
- **Reusable Components**: Leverages shared device-allocation.js
- **Clean UI**: Integrated into existing ICT Allocator dashboard
- **Flexible**: Easy to add bulk allocation, import, export later

---

## 🎯 Next Steps

### Immediate (Ready Now)
1. ✅ Login as `ict.allocator@local`
2. ✅ Navigate to Student/Teacher Allocation
3. ✅ Allocate QA-passed devices
4. ✅ Verify allocations in database

### Short Term (Enhancements)
1. 📝 Add bulk allocation (select multiple devices, assign all to same student/teacher)
2. 📝 Add allocation search/filter (find devices allocated to specific person)
3. 📝 Add "Ready for Dispatch" view with grouping by school
4. 📝 Add export to Excel feature
5. 📝 Include allocation info in POD PDF generation

### Long Term (Advanced)
1. 💡 Bulk import from Excel (upload student/teacher roster)
2. 💡 Auto-match devices to students based on school roster
3. 💡 Allocation reports and analytics dashboard
4. 💡 Mobile-friendly allocation UI for warehouse tablets
5. 💡 Integration with school management information system

---

## 🔒 Security Notes

- Allocation endpoints require authentication
- Role-based authorization enforced at API level
- User ID captured for audit trail
- Persal number validation prevents SQL injection
- No PII exposed in logs (only references)

---

## 📞 Support

**Implementation files**:
- Models: `Modules/Phase2/Models/Phase2AllocationModels.cs`
- Service: `Modules/Phase2/Services/Phase2AllocationService.cs`
- Controller: `Modules/Phase2/Controllers/AllocationController.cs`
- UI: `Modules/Phase2/UI/ict-allocation-dashboard.html`
- JS: `Modules/Phase2/UI/js/ict-allocation-dashboard.js`
- Shared: `wwwroot/shared/js/device-allocation.js`

**For questions**:
- Database: Check `Devices` table schema
- API: Test endpoints with Postman/curl
- UI: Check browser console for errors
- Auth: Verify role assignment in AspNetUserRoles

---

**Ready for Production Use** 🚀











