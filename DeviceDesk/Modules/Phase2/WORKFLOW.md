# ICT Center Phase 2 - Complete Workflow Implementation

## Overview
Phase 2 implements the complete ICT Center workflow with audit trails, role-based actions, and manager PIN authorization for disposal.

---

## Actors & Responsibilities

| Actor | Role | Responsibilities |
|-------|------|------------------|
| **ICT Clerk** | Receiving Officer | Scan devices, verify GRV, accept stock, categorize zones |
| **Inspector** | Quality Control | Pre-assessment (Step 1.5) and Quality Assessment (Step 3) |
| **Technician** | Repair Specialist | Detailed inspection, warranty checks, repairs, disposal requests |
| **ICT Manager** | Authorization | Approve disposal with PIN + signature |

---

## Step 1: Receiving & Pre-Assessment

### 1.1-1.4: Receipting (ICT Clerk)
**Endpoint**: `POST /api/phase2/receipting`

**Request**:
```json
{
  "GrvNumber": "GRV-2025-001",
  "ClerkId": "clerk@ict.local",
  "Items": [
    { "Serial": "SN-12345", "Zone": "NewStock" },
    { "Serial": "SN-67890", "Zone": "RnR" }
  ]
}
```

**Actions**:
- Scans devices against GRV
- Verifies count
- Accepts stock into ICT Center
- Categorizes to New Stock Zone or R&R Zone
- Logs: `ClerkId`, `ReceivingDate`, `VerificationStatus`

---

### 1.5: Pre-Assessment (Inspector)
**Endpoint**: `POST /api/phase2/assessment/pre`

**Request**:
```json
{
  "DeviceId": 1,
  "Passed": true,
  "InspectorId": "inspector@ict.local",
  "Notes": "Visual check OK"
}
```

**Routing**:
- **Passed = true**: Device → Detailed Inspection Queue
- **Passed = false**: Device → Detailed Inspection Queue (for diagnosis)

**Data Captured**: `PreAssessmentPassed`, `PreAssessmentInspectorId`

---

## Step 2: Detailed Inspection (Technician)

### 2.1-2.4: Inspection & Categorization
**Endpoint**: `POST /api/phase2/assessment/detailed`

**Request Example 1 - Under Warranty**:
```json
{
  "DeviceId": 1,
  "TechnicianId": "tech@ict.local",
  "UnderWarranty": true,
  "Repairable": null,
  "Category": "WarrantyReturn",
  "Notes": "Send to manufacturer",
  "DocumentRef": "RMA-123456",
  "Destination": null
}
```
→ Device Stage: `WarrantyReturn` (locked in system, sent to manufacturer)

**Request Example 2 - Hardware Repair**:
```json
{
  "DeviceId": 2,
  "TechnicianId": "tech@ict.local",
  "UnderWarranty": false,
  "Repairable": true,
  "Category": "HardwareFailure",
  "Notes": "Motherboard issue",
  "DocumentRef": null,
  "Destination": null
}
```
→ Device Stage: `HardwareDept`

**Request Example 3 - No Issues (School Allocation)**:
```json
{
  "DeviceId": 3,
  "TechnicianId": "tech@ict.local",
  "UnderWarranty": false,
  "Repairable": null,
  "Category": "NoIssuesFound",
  "Notes": "Ready for R&R allocation",
  "DocumentRef": null,
  "Destination": "SchoolAllocation"
}
```
→ Device Stage: `SchoolAllocation`

**Categories**:
- `HardwareFailure` → `HardwareDept`
- `SoftwareIssueUpgrade` → `SoftwareDept`
- `NoIssuesFound` → `Dispatch` or `SchoolAllocation` (based on `Destination`)
- `Quarantine` → `Quarantine` (awaiting parts/software)
- `WarrantyReturn` → `WarrantyReturn`

**Data Captured**: `TechnicianId`, `InspectionDate`, `UnderWarranty`, `Repairable`, `RepairCategory`

---

### 2.5: Disposal Request (Technician)
**Endpoint**: `POST /api/phase2/disposal/request`

**Request**:
```json
{
  "DeviceId": 4,
  "TechnicianId": "tech@ict.local",
  "Reason": "Beyond repair - motherboard and screen damaged"
}
```

**Response**:
```json
{
  "DisposalId": 10
}
```

**Actions**:
- Creates disposal request
- Sets `DisposalRequested = true`
- Status: `IsApproved = false` (pending manager approval)

---

### 2.6-2.7: Manager Approval (ICT Manager)
**Endpoint**: `POST /api/phase2/disposal/approve`

**Request**:
```json
{
  "DisposalId": 10,
  "ManagerId": "manager@ict.local",
  "ManagerPin": "1234",
  "ManagerSignature": "John Doe"
}
```

**Actions**:
- Verifies disposal request exists
- Hashes manager PIN (SHA256)
- Records signature
- Sets `IsApproved = true`
- Generates disposal document path
- Updates device stage to `Disposal`

**Security**: PIN is hashed before storage; signature captured for audit trail.

**Data Captured**: `ApprovedBy`, `ManagerSignature`, `ManagerPinHash`, `ApprovedAt`, `DocumentPath`

---

## Step 3: Quality Assessment (Inspector)

### 3.1-3.4: QA Checklist
**Endpoint**: `POST /api/phase2/quality`

**Request - Pass**:
```json
{
  "DeviceId": 2,
  "InspectorId": "inspector@ict.local",
  "Passed": true,
  "Notes": "All tests passed"
}
```
→ Device Stage: `Dispatch`

**Request - Fail (Rework)**:
```json
{
  "DeviceId": 2,
  "InspectorId": "inspector@ict.local",
  "Passed": false,
  "Notes": "Boot test failed"
}
```
→ Device routed back to `HardwareDept` or `SoftwareDept` based on `RepairCategory`
→ `ReworkCount` incremented

**Routing on Failure**:
- If `RepairCategory = HardwareFailure` → `HardwareDept`
- If `RepairCategory = SoftwareIssueUpgrade` → `SoftwareDept`
- Default → `HardwareDept`

**Repeated Failures**: `ReworkCount` tracked; technician may request disposal if device repeatedly fails QA.

**Data Captured**: `QaPassed`, `QaInspectorId`, `ReworkCount`

---

## Audit Trail

Every action is logged in `AuditLogs` table:
- `UserId`: Who performed the action
- `Action`: Type of action (e.g., "ReceiptCreated", "PreAssessment", "DetailedInspection", "QualityAssessment", "DisposalRequested", "DisposalApproved")
- `DeviceId` / `DeviceSerial`: Which device
- `Details`: Additional context
- `Timestamp`: When it occurred

**Query audit logs**: `GET /api/phase2/audit?deviceId=...` (to be implemented if needed)

---

## Stock Zones & Device Flow

```
Receive → Pre-Assessment → Detailed Inspection
                                ↓
                    ┌───────────┴───────────┐
                    ↓                       ↓
            Under Warranty          Not Under Warranty
                    ↓                       ↓
            Warranty Return         Repairable?
                                            ↓
                                    ┌───────┴───────┐
                                    ↓               ↓
                                  YES              NO
                                    ↓               ↓
                        Hardware/Software    Disposal Request
                        /No Issue/Quarantine       ↓
                                    ↓         Manager Approval
                            Quality Assessment      ↓
                                    ↓           Disposal
                            ┌───────┴───────┐
                            ↓               ↓
                          Pass            Fail
                            ↓               ↓
                    Dispatch/School    Rework (back to dept)
                      Allocation
```

---

## Key Security & Business Rules

1. **Disposal Authorization**: Two-step process
   - Technician requests
   - Manager approves with unique PIN + signature
   - PIN is hashed (SHA256) before storage

2. **Warranty Handling**: Devices under warranty → manufacturer (no internal repair)

3. **Audit Trail**: Every action logged with user ID, timestamp, device serial, and action details

4. **Rework Tracking**: Quality failures increment `ReworkCount`; repeated failures may trigger disposal

5. **Zone Categorization**: Devices sorted into New Stock or R&R zones at receipt

6. **Document References**: Warranty RMA numbers, disposal authorization documents tracked

---

## Database Schema Summary

### Phase2Device (Main Entity)
- Serial, Zone, Stage
- **Step 1**: `IctClerkId`, `ReceivingDate`, `VerificationStatus`, `PreAssessmentPassed`, `PreAssessmentInspectorId`
- **Step 2**: `TechnicianId`, `InspectionDate`, `UnderWarranty`, `Repairable`, `RepairCategory`, `DisposalRequested`
- **Step 3**: `QaPassed`, `QaInspectorId`, `ReworkCount`

### DisposalRecord
- `RequestedBy` (Technician), `RequestedAt`, `Reason`
- `ApprovedBy` (Manager), `ManagerSignature`, `ManagerPinHash`, `ApprovedAt`, `IsApproved`
- `DocumentPath`

### AuditLog
- `UserId`, `Action`, `DeviceId`, `DeviceSerial`, `Details`, `Timestamp`

---

## API Endpoints Summary

| Endpoint | Method | Actor | Purpose |
|----------|--------|-------|---------|
| `/api/phase2/receipting` | POST | Clerk | Receive & categorize stock |
| `/api/phase2/assessment/pre` | POST | Inspector | Pre-assessment |
| `/api/phase2/assessment/detailed` | POST | Technician | Detailed inspection & routing |
| `/api/phase2/quality` | POST | Inspector | Quality assessment |
| `/api/phase2/disposal/request` | POST | Technician | Request disposal |
| `/api/phase2/disposal/approve` | POST | Manager | Approve disposal (PIN+signature) |
| `/api/phase2/devices/{id}` | GET | All | Get device details |
| `/api/phase2/devices?stage=...&zone=...` | GET | All | Query devices |

---

## Next Steps for UI Development

1. **Receipting Screen** (Clerk)
   - Scan GRV and device serials
   - Assign zones
   - Submit receipt

2. **Pre-Assessment Screen** (Inspector)
   - View received devices
   - Mark pass/fail with notes

3. **Detailed Inspection Screen** (Technician)
   - Scan device
   - Check warranty
   - Categorize issue
   - Request disposal if needed

4. **Quality Assessment Screen** (Inspector)
   - View devices post-repair
   - Run QA checklist
   - Pass/fail with notes

5. **Disposal Approval Modal** (Manager)
   - View pending disposal requests
   - Enter PIN + signature
   - Approve/reject

6. **Dashboard**
   - Device counts by stage
   - Pending approvals
   - Rework queue
   - Audit log viewer

---

## Testing the Workflow

1. **Create Receipt**
   ```bash
   POST /api/phase2/receipting
   ```

2. **Pre-Assessment**
   ```bash
   POST /api/phase2/assessment/pre
   ```

3. **Detailed Inspection**
   ```bash
   POST /api/phase2/assessment/detailed
   ```

4. **Quality Check**
   ```bash
   POST /api/phase2/quality
   ```

5. **Disposal Flow**
   ```bash
   POST /api/phase2/disposal/request
   POST /api/phase2/disposal/approve
   ```

All endpoints are live and ready for UI integration.
