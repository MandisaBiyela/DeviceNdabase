# DeviceDesk - System Credentials & Access Control

> **⚠️ SECURITY NOTICE**: This document contains default system credentials for development/testing purposes. In production environments, these passwords must be changed immediately after deployment.

---

## Universal Password

**All seeded accounts use the same default password**: `P@ssw0rd1!`

---

## Quick Reference - All System Users

| Role | Email | Full Name | Department | Landing Page |
|------|-------|-----------|------------|--------------|
| **Orders Clerk** | `orders.clerk@local` | Orders Clerk | Phase 0 - Procurement | `/phase0/new.html` |
| **Receiving Clerk** | `receiving.clerk@local` | Receiving Clerk | Phase 1 - Receiving | `/phase1/dashboard.html` |
| **ICT Clerk** | `ict.clerk@local` | ICT Clerk | Phase 2 - ICT Center | `/phase2/index.html` |
| **ICT Inspector** | `ict.inspector@local` | ICT Inspector | Phase 2 - ICT Center | `/phase2/index.html` |
| **ICT Technician** | `ict.technician@local` | ICT Technician | Phase 2 - ICT Center | `/phase2/index.html` |
| **ICT Manager** | `ict.manager@local` | ICT Manager | Phase 2 - ICT Center | `/phase2/index.html` |
| **ICT Allocator** | `ict.allocator@local` | ICT Allocator | Phase 2 - ICT Center | `/phase2/index.html` |
| **Dispatch Clerk** | `dispatch.clerk@local` | Dispatch Clerk | Phase 3 - Dispatch | `/dispatch/index.html` |
| **Super Admin** | `superadmin@local` | Super Admin | System Administration | `/superadmin/dashboard.html` |

---

## Phase 0 - Procurement Intake

### Orders Clerk

**Credentials**:
- Email: `orders.clerk@local`
- Password: `P@ssw0rd1!`
- Role: `OrdersClerk`
- Department: Phase 0 - Procurement

**Access**:
- Landing Page: `/phase0/new.html`
- Allowed Endpoints:
  - ✅ `GET /api/phase0/new/orders` - List batches as orders
  - ✅ `GET /api/phase0/new/orders/{id}` - Get batch details
  - ✅ All Phase 0 batch management endpoints

**Responsibilities**:
- Create and manage New Stock batches
- Create and manage R&R (Repair & Replace) batches
- Import device data from spreadsheets
- Prepare batch data for Phase 1 receiving
- Monitor procurement intake dashboard

**Workflow**:
1. Ingest new stock and R&R batches
2. Parse intake spreadsheets
3. Normalize device records
4. Expose batches as "orders" for Phase 1 selection

---

## Phase 1 - Receiving

### Receiving Clerk

**Credentials**:
- Email: `receiving.clerk@local`
- Password: `P@ssw0rd1!`
- Role: `ReceivingClerk`
- Department: Phase 1 - Receiving

**Access**:
- Landing Page: `/phase1/dashboard.html`
- Allowed Endpoints:
  - ✅ `GET /api/phase1/orders` - List Phase 0 batches ready for receiving
  - ✅ `POST /api/phase1/receiving/batches` - Create receiving batch
  - ✅ `GET /api/phase1/receiving/batches/{id}` - Get batch details
  - ✅ `GET /api/phase1/receiving/batches/{id}/blind-copy` - Generate blind copy PDF
  - ✅ `POST /api/phase1/receiving/batches/{batchId}/scan-item` - Scan device
  - ✅ `POST /api/phase1/receiving/batches/{batchId}/generate-grv` - Generate GRV
  - ✅ All Phase 1 R&R receiving endpoints
  - ✅ Reconciliation, document management, and export endpoints

**Responsibilities**:
- Select Phase 0 batches as orders
- Perform blind copy scanning against GRV
- Scan-in devices and reconcile counts
- Generate Goods Received Voucher (GRV)
- Monitor receiving dashboard
- Upload and manage supporting documents
- Hand over completed batches to Phase 2

**Workflow**:
1. Select batch from Phase 0 orders
2. Perform blind copy/scanning
3. Reconcile scanned vs expected quantities
4. Generate GRV
5. Hand over to ICT Center (Phase 2)

---

## Phase 2 - ICT Center

### ICT Clerk (Receiving Officer)

**Credentials**:
- Email: `ict.clerk@local`
- Password: `P@ssw0rd1!`
- Role: `IctClerk`
- Department: Phase 2 - ICT Center

**Access**:
- Landing Page: `/phase2/index.html`
- Allowed Endpoints:
  - ✅ `POST /api/phase2/receipting` - Create receipt, scan devices, verify GRV
  - ✅ `GET /api/phase2/devices/*` - Query devices
  - ✅ `GET /api/phase2/users?role=IctTechnician` - View technician list (user admin)

**Responsibilities**:
- Receive stock from Phase 1 against GRV
- Scan incoming devices
- Verify device counts
- Accept stock into ICT Center
- Categorize devices to New Stock Zone or R&R Zone
- Manage technician assignments (basic user admin)

**Workflow Step**: Step 1.1-1.4 (Receiving & Verification)

**API Example**:
```json
POST /api/auth/login
{
  "Email": "ict.clerk@local",
  "Password": "P@ssw0rd1!"
}
```

---

### ICT Inspector (Quality Control)

**Credentials**:
- Email: `ict.inspector@local`
- Password: `P@ssw0rd1!`
- Role: `IctInspector`
- Department: Phase 2 - ICT Center

**Access**:
- Landing Page: `/phase2/index.html`
- Allowed Endpoints:
  - ✅ `POST /api/phase2/assessment/pre` - Pre-assessment (visual/functional check)
  - ✅ `POST /api/phase2/quality` - Quality assessment after repair
  - ✅ `GET /api/phase2/devices/*` - Query devices

**Responsibilities**:
- Perform initial visual/functional checks (pre-assessment)
- Set `PreAssessmentPassed` flag (true/false)
- Conduct quality assessment checklist after repairs
- Pass/fail devices post-repair
- Route failed devices back to technician for rework
- Track rework cycles

**Workflow Steps**: 
- Step 1.5 (Pre-Assessment)
- Step 3 (Quality Assessment)

**API Example**:
```json
POST /api/auth/login
{
  "Email": "ict.inspector@local",
  "Password": "P@ssw0rd1!"
}
```

---

### ICT Technician (Repair Specialist)

**Credentials**:
- Email: `ict.technician@local`
- Password: `P@ssw0rd1!`
- Role: `IctTechnician`
- Department: Phase 2 - ICT Center

**Access**:
- Landing Page: `/phase2/index.html`
- Allowed Endpoints:
  - ✅ `POST /api/phase2/assessment/detailed` - Detailed inspection, warranty check, repair routing
  - ✅ `POST /api/phase2/disposal/request` - Request disposal for unrepairable devices
  - ✅ `POST /api/phase2/disposal/approve` - Submit manager approval (on behalf of manager)
  - ✅ `GET /api/phase2/devices/*` - Query devices

**Responsibilities**:
- Scan device and retrieve device information
- Check warranty status
- Assess repairability
- Categorize issues:
  - Hardware issues
  - Software issues
  - No issues found
  - Quarantine required
- Route devices for repair
- Request disposal for unrepairable/uneconomical devices
- Manage "My Work" queue

**Workflow Steps**: 
- Step 2.1-2.4 (Detailed Inspection)
- Step 2.5 (Disposal Request)

**Notes**:
- Can only perform detailed inspection after `PreAssessmentPassed` is set (true or false)
- Requests disposal but cannot approve (requires ICT Manager)

**API Example**:
```json
POST /api/auth/login
{
  "Email": "ict.technician@local",
  "Password": "P@ssw0rd1!"
}
```

---

### ICT Manager (Authorization & Oversight)

**Credentials**:
- Email: `ict.manager@local`
- Password: `P@ssw0rd1!`
- Role: `IctManager`
- Department: Phase 2 - ICT Center

**Access**:
- Landing Page: `/phase2/index.html`
- Allowed Endpoints:
  - ✅ `GET /api/phase2/devices/*` - Query devices
  - ✅ `GET /api/phase2/audit/events` - View audit logs
  - ✅ `GET /api/phase2/audit/export` - Export audit data
  - ✅ Disposal approval via technician screen (PIN + signature)

**Responsibilities**:
- Review disposal requests
- Approve disposal using unique PIN + signature
- Authorize disposal document generation
- Access audit reporting
- Export audit logs
- Oversee ICT Center operations

**Workflow Step**: Step 2.6-2.7 (Disposal Authorization)

**Notes**:
- Managers approve disposal on the technician's screen
- Technician calls `POST /api/phase2/disposal/approve` while manager enters PIN and signature
- Manager PIN is hashed (SHA256) before storage
- Managers do not directly call backend endpoints for approvals

**API Example**:
```json
POST /api/auth/login
{
  "Email": "ict.manager@local",
  "Password": "P@ssw0rd1!"
}
```

---

### ICT Allocator (Storage Management)

**Credentials**:
- Email: `ict.allocator@local`
- Password: `P@ssw0rd1!`
- Role: `IctAllocator`
- Department: Phase 2 - ICT Center

**Access**:
- Landing Page: `/phase2/index.html`
- Allowed Endpoints:
  - ✅ `POST /api/phase2/allocation/assign` - Assign storage locations
  - ✅ `GET /api/phase2/allocation/zones` - View available zones
  - ✅ `GET /api/phase2/devices/*` - Query devices

**Responsibilities**:
- Assign storage locations to devices
- Manage device-to-student/teacher allocation
- Track storage zones and capacity
- Generate allocation reports

**API Example**:
```json
POST /api/auth/login
{
  "Email": "ict.allocator@local",
  "Password": "P@ssw0rd1!"
}
```

---

## Phase 2 - Access Control Matrix

| Endpoint | ICT Clerk | ICT Inspector | ICT Technician | ICT Manager | ICT Allocator |
|----------|-----------|---------------|----------------|-------------|---------------|
| `POST /api/phase2/receipting` | ✅ | ❌ | ❌ | ❌ | ❌ |
| `POST /api/phase2/assessment/pre` | ❌ | ✅ | ❌ | ❌ | ❌ |
| `POST /api/phase2/assessment/detailed` | ❌ | ❌ | ✅ | ❌ | ❌ |
| `POST /api/phase2/quality` | ❌ | ✅ | ❌ | ❌ | ❌ |
| `POST /api/phase2/disposal/request` | ❌ | ❌ | ✅ | ❌ | ❌ |
| `POST /api/phase2/disposal/approve` | ❌ | ❌ | ✅* | ❌ | ❌ |
| `GET /api/phase2/audit/*` | ❌ | ❌ | ❌ | ✅ | ❌ |
| `POST /api/phase2/allocation/*` | ❌ | ❌ | ❌ | ❌ | ✅ |
| `GET /api/phase2/devices/*` | ✅ | ✅ | ✅ | ✅ | ✅ |

*Technician submits approval on behalf of manager with manager's PIN + signature

---

## Phase 3 - Dispatch

### Dispatch Clerk

**Credentials**:
- Email: `dispatch.clerk@local`
- Password: `P@ssw0rd1!`
- Role: `DispatchClerk`
- Department: Phase 3 - Dispatch

**Access**:
- Landing Page: `/dispatch/index.html`
- Allowed Endpoints:
  - ✅ `GET /api/dispatch/devices` - List dispatch-ready devices
  - ✅ `GET /api/dispatch/devices/{id}` - Get device details
  - ✅ `POST /api/dispatch/pods` - Create Proof of Delivery (POD)
  - ✅ `GET /api/phase3/dispatch/ready-list` - View ready devices grouped by source
  - ✅ `GET /api/dispatch/pods/{podNumber}` - Get POD details
  - ✅ `POST /api/dispatch/pods/{podNumber}/signed-pod` - Upload signed POD

**Responsibilities**:
- List devices ready for dispatch
- Filter by `AwaitingDispatch` and legacy `Dispatch` stages
- Create PODs for school allocations
- Allocate devices to schools/customers
- Manage delivery workflows
- Upload signed PODs
- Respect ICT scan-out data

**Workflow**:
1. View dispatch-ready devices (grouped by source)
2. Create POD for school allocation
3. Move devices to `SchoolAllocation` stage
4. Preserve existing ICT scan-out fields
5. Generate POD and Delivery Note documents

**Integration with Phase 2**:
- ICT Clerk scan-out sets `Stage = AwaitingDispatch`
- Dispatch lists prioritize `AwaitingDispatch`
- Backward-compatible with legacy `Dispatch` stage

**API Example**:
```json
POST /api/auth/login
{
  "Email": "dispatch.clerk@local",
  "Password": "P@ssw0rd1!"
}
```

---

## System Administration

### Super Admin

**Credentials**:
- Email: `superadmin@local`
- Password: `P@ssw0rd1!`
- Role: `SuperAdmin`
- Department: System Administration

**Access**:
- Landing Page: `/superadmin/dashboard.html`
- **Full System Access** - All endpoints across all phases

**Allowed Endpoints** (Summary):
- ✅ `GET /api/superadmin/dashboard/stats` - Dashboard statistics
- ✅ `GET /api/superadmin/dashboard/phase{0-3}/stats` - Phase-specific stats
- ✅ `GET /api/superadmin/devices` - All devices across all phases
- ✅ `GET /api/superadmin/devices/{serial}/lifecycle` - Device lifecycle tracking
- ✅ `GET /api/superadmin/audit` - Unified audit logs (all phases)
- ✅ `GET /api/superadmin/users` - User management
- ✅ `POST /api/superadmin/users` - Create users
- ✅ `PUT /api/superadmin/users/{id}` - Update users
- ✅ `DELETE /api/superadmin/users/{id}` - Delete users
- ✅ `POST /api/superadmin/users/{id}/toggle-active` - Activate/deactivate users
- ✅ `POST /api/superadmin/users/{id}/reset-password` - Reset user passwords
- ✅ `GET /api/superadmin/roles` - List all system roles
- ✅ `GET /api/superadmin/export/*` - Export data (devices, GRVs, PODs, audit logs, schools, drivers, vehicles)
- ✅ `GET /api/superadmin/imported-devices` - View imported devices
- ✅ `GET /api/superadmin/school-devices` - View devices by school
- ✅ `GET /api/superadmin/provincial-analytics` - Provincial analytics

**Responsibilities**:
- **User Management**:
  - Create, update, delete user accounts
  - Assign/remove roles
  - Activate/deactivate accounts
  - Reset passwords
  - View user activity
- **System Monitoring**:
  - Dashboard statistics across all phases
  - Device lifecycle tracking
  - Unified audit logs
  - Performance metrics
- **Data Export**:
  - Export devices, GRVs, PODs
  - Export audit logs
  - Export schools, drivers, vehicles
  - Export trips and dispatch data
- **Analytics**:
  - Phase-specific statistics
  - Provincial analytics
  - School device allocation reports
  - Imported device tracking
- **System Administration**:
  - Database health monitoring
  - Configuration management
  - Security oversight

**Special Permissions**:
- Can view and export data from all phases
- Can manage all user accounts (except cannot delete self)
- Can reset any user's password
- Can activate/deactivate user accounts
- Access to unified audit logs across all phases
- Can reseed imported devices

**API Example**:
```json
POST /api/auth/login
{
  "Email": "superadmin@local",
  "Password": "P@ssw0rd1!"
}
```

---

## System Roles Summary

| Role | Role Constant | Seeded User | Description |
|------|--------------|-------------|-------------|
| **Orders Clerk** | `OrdersClerk` | ✅ | Manages procurement orders and new stock batches |
| **Receiving Clerk** | `ReceivingClerk` | ✅ | Handles device receiving and GRV processing |
| **ICT Clerk** | `IctClerk` | ✅ | Receipts devices and performs initial verification |
| **ICT Inspector** | `IctInspector` | ✅ | Performs pre-assessment and quality checks |
| **ICT Technician** | `IctTechnician` | ✅ | Conducts detailed inspection and repair routing |
| **ICT Manager** | `IctManager` | ✅ | Manages ICT operations and approves disposals |
| **ICT Allocator** | `IctAllocator` | ✅ | Assigns storage locations to devices |
| **Dispatch Clerk** | `DispatchClerk` | ✅ | Manages dispatch operations and PODs |
| **Dispatch Driver** | `DispatchDriver` | ❌ | Handles delivery and proof of delivery |
| **Dispatch QA** | `DispatchQA` | ❌ | Performs quality checks on dispatch |
| **Dispatch Manager** | `DispatchManager` | ❌ | Oversees dispatch operations |
| **Supervisor** | `Supervisor` | ❌ | Supervisory role (not currently seeded) |
| **Admin** | `Admin` | ❌ | System administrator (not currently seeded) |
| **Super Admin** | `SuperAdmin` | ✅ | Full system access and user management |
| **Driver** | `Driver` | ❌ | General driver role (not currently seeded) |

**Notes**:
- ✅ = User account is automatically seeded on system startup
- ❌ = Role exists but no default user is seeded
- All roles are created on startup, but only specific users are seeded
- SuperAdmin can create additional users for any role

---

## Authentication Flow

### Login Process

1. Navigate to: `http://localhost:5170/login.html`
2. Enter email and password
3. Upon successful login:
   - **Orders Clerk** → `/phase0/new.html`
   - **Receiving Clerk** → `/phase1/dashboard.html`
   - **ICT Center roles** → `/phase2/index.html`
   - **Dispatch Clerk** → `/dispatch/index.html`
   - **Super Admin** → `/superadmin/dashboard.html`

### Authentication Endpoints

- **Login**: `POST /api/auth/login`
  - Request: `{ "Email": "user@local", "Password": "P@ssw0rd1!" }`
  - Response: Cookie-based session
  
- **Current User**: `GET /api/auth/current-user`
  - Returns: `{ "fullName", "role", "email", "department" }`
  - Used for UI role gating
  
- **Logout**: `POST /api/auth/logout`
  - Clears session and redirects to login

---

## Security Notes

### Password Policy

- **Default Password**: `P@ssw0rd1!` (for all seeded accounts)
- **Production Requirement**: Change all default passwords immediately
- **Password Requirements**:
  - Minimum 8 characters
  - At least one uppercase letter
  - At least one lowercase letter
  - At least one number
  - At least one special character

### Role-Based Access Control (RBAC)

- All API endpoints are protected with `[Authorize(Roles = "...")]`
- UI gating is supplementary - backend authorization is enforced
- Frontend checks current user role via `/api/auth/current-user`
- Unauthorized access returns 403 Forbidden

### Manager PIN Security

- ICT Manager PIN is used for disposal approval
- PIN is hashed using SHA256 before storage
- Manager must physically enter PIN on technician's screen
- Both PIN and signature required for approval

### Session Management

- Cookie-based authentication
- Session expires on logout
- Automatic redirect to login for unauthenticated requests
- Role-based landing page redirection

---

## Testing Credentials Quick Reference

### Minimal Test Set (One per phase)

```
Phase 0: orders.clerk@local / P@ssw0rd1!
Phase 1: receiving.clerk@local / P@ssw0rd1!
Phase 2: ict.clerk@local / P@ssw0rd1!
Phase 3: dispatch.clerk@local / P@ssw0rd1!
Admin: superadmin@local / P@ssw0rd1!
```

### Full ICT Center Test Set

```
Receipting: ict.clerk@local / P@ssw0rd1!
Pre-Assessment: ict.inspector@local / P@ssw0rd1!
Detailed Inspection: ict.technician@local / P@ssw0rd1!
Quality Assessment: ict.inspector@local / P@ssw0rd1!
Disposal Approval: ict.manager@local / P@ssw0rd1!
Storage Allocation: ict.allocator@local / P@ssw0rd1!
```

---

## Database Contexts

- **ApplicationDbContext**: Identity (users, roles, authentication)
- **DeviceDeskDbContext**: Phase 0 (procurement, orders, schools)
- **Phase1DbContext**: Phase 1 (receiving, GRVs, scanning)
- **Phase2DbContext**: Phase 2 (ICT Center, devices, assessments, disposals, audit)
- **Phase3DbContext**: Phase 3 (dispatch, PODs, trips, deliveries)
- **SuperAdminDbContext**: SuperAdmin (imported devices, analytics)

---

## Glossary

- **GRV**: Goods Received Voucher - formal record during receiving
- **POD**: Proof of Delivery - dispatch document finalizing allocation
- **R&R**: Repair and Replace - batch type for refurbishment workflow
- **AwaitingDispatch**: Interim stage after ICT scan-out, before dispatch POD
- **Blind Copy**: Receiving process where expected quantities are hidden
- **Pre-Assessment**: Initial visual/functional check before detailed inspection
- **Quality Assessment**: Post-repair verification to pass/fail devices
- **Rework**: Failed quality check requiring technician re-inspection
- **Disposal Request**: Technician request to dispose unrepairable device
- **Disposal Approval**: Manager authorization with PIN + signature

---

## Support & Documentation

- **Main Documentation**: `DeviceDesk/devicedesk.md`
- **Phase 2 Access Control**: `DeviceDesk/Modules/Phase2/ACCESS_CONTROL.md`
- **Phase 2 Credentials**: `DeviceDesk/Modules/Phase2/CREDENTIALS.md`
- **Phase 2 Workflow**: `DeviceDesk/Modules/Phase2/WORKFLOW.md`
- **Migration Summary**: `DeviceDesk/Migrations/MIGRATION_SUMMARY.md`
- **Quick Start**: `DeviceDesk/QUICK_START_RESEED.md`

---

## Changelog

- **2024-12**: Initial system deployment with all phases
- Added ICT Allocator role for storage management
- Added SuperAdmin dashboard with analytics
- Unified audit logging across all phases
- Enhanced user management with role assignments

---

**Document Version**: 1.0  
**Last Updated**: December 2024  
**Maintained By**: DeviceDesk System Team









