# ICT Allocator Student/Teacher Allocation - Testing Guide

## ✅ Implementation Status

**Completed**: December 3, 2024  
**Build Status**: ✅ Compilation successful  
**Linter Status**: ✅ No errors  
**Ready for**: Testing

---

## 📦 What Was Implemented

### Backend (Phase 2)
1. ✅ **Phase2AllocationModels.cs** - DTOs for allocation
2. ✅ **Phase2AllocationService.cs** - Business logic with validation
3. ✅ **AllocationController.cs** - Two new endpoints:
   - `GET /api/phase2/allocation/ready-for-assignment`
   - `POST /api/phase2/allocation/devices/{id}/assign`
4. ✅ **DI Registration** - Service registered in Program.cs

### Frontend (ICT Allocator Dashboard)
1. ✅ **ict-allocation-dashboard.html** - New menu items and modal
2. ✅ **ict-allocation-extensions.js** - View loading and allocation logic
3. ✅ **device-allocation.js** - Reusable allocation component (already created)

### Features
- ✅ QA-passed device filtering
- ✅ Student/Teacher toggle allocation
- ✅ Persal number validation
- ✅ Audit trail logging
- ✅ Role-based access (IctAllocator, IctManager, Admin)

---

## 🧪 Testing Prerequisites

### 1. Apply Database Migration

```powershell
# Stop the application first
cd DeviceDesk
dotnet ef migrations add Device_AddAllocationFields --context DeviceDeskDbContext
dotnet ef database update
```

### 2. Create Test User (if needed)

```sql
-- Verify IctAllocator role exists
SELECT * FROM AspNetRoles WHERE Name = 'IctAllocator';

-- Verify user exists
SELECT * FROM AspNetUsers WHERE Email = 'ict.allocator@local';

-- Check user role assignment
SELECT u.Email, r.Name
FROM AspNetUsers u
JOIN AspNetUserRoles ur ON u.Id = ur.UserId
JOIN AspNetRoles r ON r.Id = ur.RoleId
WHERE u.Email = 'ict.allocator@local';
```

**Login Credentials**:
- Email: `ict.allocator@local`
- Password: `P@ssw0rd1!`

### 3. Create Test Devices

```sql
-- Get devices that passed QA
SELECT TOP 10 
    Id,
    Serial,
    Zone,
    Stage,
    QaPassed,
    SchoolId,
    SchoolName
FROM Phase2Devices
WHERE QaPassed = 1
  AND Stage IN ('QualityAssessment', 'AwaitingDispatch')
  AND (DisposalRequested IS NULL OR DisposalRequested = 0)
ORDER BY UpdatedAt DESC;
```

---

## 🎯 Testing Scenarios

### Scenario 1: Access ICT Allocator Dashboard

**Steps**:
1. Navigate to: `http://localhost:5000/login.html` (adjust port as needed)
2. Login as: `ict.allocator@local` / `P@ssw0rd1!`
3. Should redirect to: `/phase2/index.html`
4. Click on ICT Allocation Dashboard link
5. Navigate to: `/phase2/ict-allocation-dashboard.html`

**Expected**:
- ✅ Dashboard loads
- ✅ Sidebar shows "ASSIGNMENT" section
- ✅ "Student/Teacher Allocation" menu item visible
- ✅ "Ready for Dispatch" menu item visible

### Scenario 2: View Devices Ready for Allocation

**Steps**:
1. In ICT Allocator Dashboard, click "Student/Teacher Allocation"

**Expected**:
- ✅ View loads with table of QA-passed devices
- ✅ Shows: Serial, School, Zone, Stage, Current Allocation, Action button
- ✅ If no devices: Shows "No QA-passed devices pending allocation"
- ✅ Each row has "Allocate" button

**API Call**:
```
GET /api/phase2/allocation/ready-for-assignment
```

**Sample Response**:
```json
[
  {
    "phase2DeviceId": 123,
    "serial": "SN-12345",
    "zone": "RnR",
    "stage": "QualityAssessment",
    "schoolId": 1,
    "schoolName": "Test Primary School",
    "qaPassed": true,
    "allocationType": 0,
    "studentName": null,
    "studentIdNumber": null,
    "teacherName": null,
    "teacherPersalNumber": null
  }
]
```

### Scenario 3: Allocate Device to Student

**Steps**:
1. Click "Allocate" button on a device
2. Modal opens showing device serial and school
3. Select "Student" from dropdown
4. Enter Student Name: "John Doe"
5. Enter Student ID Number: "20240001"
6. Click "Save Allocation"

**Expected**:
- ✅ Modal opens correctly
- ✅ Device info displayed (school, zone, stage)
- ✅ Allocation dropdown shows: No Allocation, Student, Teacher
- ✅ Student fields appear when "Student" selected
- ✅ Teacher fields hidden
- ✅ On save: Modal closes, success message, list refreshes
- ✅ Device removed from "ready" list (now allocated)

**API Call**:
```
POST /api/phase2/allocation/devices/123/assign
Content-Type: application/json

{
  "deviceId": "device-guid-here",
  "allocationType": 1,
  "studentName": "John Doe",
  "studentIdNumber": "20240001"
}
```

**Database Verification**:
```sql
-- Check allocation in core Devices table
SELECT 
    SerialNumber,
    AllocationType,
    StudentName,
    StudentIdNumber,
    AllocatedAt,
    AllocatedByUserId
FROM Devices
WHERE SerialNumber = 'SN-12345';

-- Check audit log in Phase2
SELECT 
    DeviceSerial,
    UserId,
    Action,
    Details,
    Timestamp
FROM Phase2AuditLogs
WHERE DeviceSerial = 'SN-12345'
  AND Action = 'StudentTeacherAllocated'
ORDER BY Timestamp DESC;
```

### Scenario 4: Allocate Device to Teacher

**Steps**:
1. Click "Allocate" on a different device
2. Select "Teacher" from dropdown
3. Enter Teacher Name: "Jane Smith"
4. Enter Teacher Persal Number: "1234567"
5. Click "Save Allocation"

**Expected**:
- ✅ Teacher fields appear
- ✅ Student fields hidden
- ✅ Persal validation: Must be numeric
- ✅ On save: Success, device removed from list

**Invalid Persal Test**:
- Enter "ABC123" in persal field
- Should get error: "Persal number must be numeric"

### Scenario 5: Re-allocate Device

**Steps**:
1. Allocate device to student
2. Re-open allocation modal for same device
3. Change to "Teacher"
4. Save

**Expected**:
- ✅ Previous allocation shown in modal
- ✅ Can switch type
- ✅ Student fields cleared, teacher fields saved
- ✅ Audit log shows both allocations

### Scenario 6: View Ready for Dispatch

**Steps**:
1. Click "Ready for Dispatch" in sidebar

**Expected**:
- ✅ View loads (currently placeholder)
- ✅ Shows message about future implementation
- ✅ Eventually will show grouped allocated devices

---

## 🐛 Error Scenarios to Test

### Error 1: Device Not QA-Passed
**Setup**: Try to allocate device with `QaPassed = false`  
**Expected**: Error "Device must be QA-passed before allocation."

### Error 2: Device Already Disposed
**Setup**: Try to allocate device with `DisposalRequested = true`  
**Expected**: Error "Cannot allocate disposed devices."

### Error 3: Core Device Not Found
**Setup**: Phase2Device exists but no matching SerialNumber in Devices table  
**Expected**: Error "Core device record not found for serial: XXX"

### Error 4: Invalid Persal Format
**Setup**: Enter non-numeric persal (e.g., "ABC123")  
**Expected**: Error "Persal number must be numeric."

### Error 5: Missing Required Fields
**Setup**: Select "Student" but leave name blank  
**Expected**: Client-side validation error (from device-allocation.js validateAllocations)

---

## 📊 Database Verification Queries

### Check All Allocated Devices
```sql
SELECT 
    d.SerialNumber,
    d.SchoolName,
    d.AllocationType,
    CASE 
        WHEN d.AllocationType = 0 THEN 'None'
        WHEN d.AllocationType = 1 THEN 'Student: ' + ISNULL(d.StudentName, '') + ' (' + ISNULL(d.StudentIdNumber, '') + ')'
        WHEN d.AllocationType = 2 THEN 'Teacher: ' + ISNULL(d.TeacherName, '') + ' (Persal: ' + ISNULL(d.TeacherPersalNumber, '') + ')'
    END AS Allocation,
    d.AllocatedAt,
    d.AllocatedByUserId
FROM Devices d
WHERE d.AllocationType > 0
ORDER BY d.AllocatedAt DESC;
```

### Check Phase2 Allocation Audit Trail
```sql
SELECT 
    al.DeviceSerial,
    al.UserId,
    al.Action,
    al.Details,
    al.Timestamp
FROM Phase2AuditLogs al
WHERE al.Action = 'StudentTeacherAllocated'
ORDER BY al.Timestamp DESC;
```

### Allocation Statistics by School
```sql
SELECT 
    d.SchoolName,
    SUM(CASE WHEN d.AllocationType = 1 THEN 1 ELSE 0 END) AS StudentsCount,
    SUM(CASE WHEN d.AllocationType = 2 THEN 1 ELSE 0 END) AS TeachersCount,
    COUNT(*) AS TotalAllocated
FROM Devices d
WHERE d.AllocationType > 0
GROUP BY d.SchoolName
ORDER BY TotalAllocated DESC;
```

---

## 🔧 Common Issues & Solutions

### Issue: "DeviceAllocation is not defined"
**Cause**: device-allocation.js not loaded  
**Solution**: Verify script tag in ict-allocation-dashboard.html:
```html
<script src="/shared/js/device-allocation.js?v=20251203"></script>
```

### Issue: "Failed to load devices: 404"
**Cause**: Endpoint not found  
**Solution**: Verify service registered in Program.cs and app restarted

### Issue: Modal doesn't open
**Cause**: Bootstrap not loaded or modal HTML missing  
**Solution**: Check bootstrap.bundle.min.js is included

### Issue: "Phase 2 device not found"
**Cause**: Using wrong ID or device doesn't exist  
**Solution**: Verify phase2DeviceId from the list matches database

### Issue: No devices shown in list
**Possible causes**:
1. No devices have QaPassed = true
2. Devices already allocated (AllocationType != 0)
3. Devices in wrong stages

**Solution**: Check database query results manually

---

## ✅ Success Criteria

Test is successful when:

- [x] Login as IctAllocator works
- [x] Dashboard loads with new menu items
- [ ] "Student/Teacher Allocation" view loads devices
- [ ] "Allocate" button opens modal
- [ ] Can allocate device to student
- [ ] Can allocate device to teacher
- [ ] Switching types clears previous fields
- [ ] Invalid persal number rejected
- [ ] Allocation saved to database
- [ ] Audit log created
- [ ] Device removed from pending list after allocation
- [ ] Ready for Dispatch view accessible

---

## 📝 Test Execution Checklist

### Pre-Test
- [ ] Stop running application
- [ ] Apply database migration
- [ ] Verify IctAllocator user exists
- [ ] Create test devices with QaPassed = true
- [ ] Restart application

### During Test
- [ ] Login successful
- [ ] Dashboard loads
- [ ] Menu items appear
- [ ] Devices list loads
- [ ] Modal opens
- [ ] Student allocation works
- [ ] Teacher allocation works
- [ ] Validation works
- [ ] Database updates correctly
- [ ] Audit log created

### Post-Test
- [ ] Verify database records
- [ ] Check audit logs
- [ ] Test with multiple devices
- [ ] Test error scenarios
- [ ] Verify allocation persists

---

## 🚀 Next Steps After Testing

1. **Enhance Ready for Dispatch View**: Show allocated devices grouped by school
2. **Add Bulk Allocation**: Allocate multiple devices at once
3. **Export Functionality**: Export allocation lists to Excel
4. **Integration with Phase 3**: Show allocation on POD/delivery notes
5. **Reports**: Allocation statistics and tracking
6. **School Rosters**: Import student/teacher lists for auto-matching

---

## 📞 Support

**Test completed by**: _________  
**Test date**: _________  
**Issues found**: _________  
**Status**: PASS / FAIL  

For issues, check:
- Browser console (F12) for JavaScript errors
- Application logs for backend errors
- Database for data persistence issues

