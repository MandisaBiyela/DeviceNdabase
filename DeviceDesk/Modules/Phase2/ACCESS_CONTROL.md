# Phase 2 - ICT Center Role-Based Access Control

## User Accounts & Credentials

All accounts use password: **P@ssw0rd1!**

| Role | Email | Full Name | Department |
|------|-------|-----------|------------|
| **ICT Clerk** | `ict.clerk@local` | ICT Clerk | Phase 2 - ICT Center |
| **ICT Inspector** | `ict.inspector@local` | ICT Inspector | Phase 2 - ICT Center |
| **ICT Technician** | `ict.technician@local` | ICT Technician | Phase 2 - ICT Center |
| **ICT Manager** | `ict.manager@local` | ICT Manager | Phase 2 - ICT Center |

---

## Role-Based Endpoint Access

### ICT Clerk (Receiving Officer)
**Role**: `IctClerk`

**Allowed Endpoints**:
- ✅ `POST /api/phase2/receipting` - Create receipt, scan devices, verify GRV
- ✅ `GET /api/phase2/devices/*` - Query devices

**Workflow Step**: Step 1.1-1.4 (Receiving & Verification)

**Responsibilities**:
- Scan incoming stock against GRV
- Verify count
- Accept stock into ICT Center
- Categorize to New Stock Zone or R&R Zone

---

### ICT Inspector (Quality Control)
**Role**: `IctInspector`

**Allowed Endpoints**:
- ✅ `POST /api/phase2/assessment/pre` - Pre-assessment (visual/functional check)
- ✅ `POST /api/phase2/quality` - Quality assessment after repair
- ✅ `GET /api/phase2/devices/*` - Query devices

**Workflow Steps**: 
- Step 1.5 (Pre-Assessment)
- Step 3 (Quality Assessment)

**Responsibilities**:
- Perform initial visual/functional checks
- Conduct quality assessment checklist
- Pass/fail devices post-repair
- Route failed devices back to technician for rework

---

### ICT Technician (Repair Specialist)
**Role**: `IctTechnician`

**Allowed Endpoints**:
- ✅ `POST /api/phase2/assessment/detailed` - Detailed inspection, warranty check, repair routing
- ✅ `POST /api/phase2/disposal/request` - Request disposal for unrepairable devices
- ✅ `GET /api/phase2/devices/*` - Query devices

**Workflow Steps**: 
- Step 2.1-2.4 (Detailed Inspection)
- Step 2.5 (Disposal Request)

**Responsibilities**:
- Scan device and retrieve info
- Check warranty status
- Assess repairability
- Categorize issues (Hardware/Software/No Issue/Quarantine)
- Request disposal for unrepairable devices

---

### ICT Manager (Authorization)
**Role**: `IctManager`

**Allowed Endpoints**:
- ✅ `GET /api/phase2/devices/*` - Query devices

Note: Managers approve disposal on the technician's screen. The technician calls `POST /api/phase2/disposal/approve` while the manager enters PIN and signature in the request body. Managers do not directly call backend endpoints for approvals.

**Workflow Step**: Step 2.6-2.7 (Disposal Authorization)

**Responsibilities**:
- Review disposal requests
- Approve disposal using unique PIN + signature
- Authorize disposal document generation

---

## Access Control Matrix

| Endpoint | Clerk | Inspector | Technician | Manager |
|----------|-------|-----------|------------|---------|
| `POST /api/phase2/receipting` | ✅ | ❌ | ❌ | ❌ |
| `POST /api/phase2/assessment/pre` | ❌ | ✅ | ❌ | ❌ |
| `POST /api/phase2/assessment/detailed` | ❌ | ❌ | ✅ | ❌ |
| `POST /api/phase2/quality` | ❌ | ✅ | ❌ | ❌ |
| `POST /api/phase2/disposal/request` | ❌ | ❌ | ✅ | ❌ |
| `POST /api/phase2/disposal/approve` | ❌ | ❌ | ✅ | ❌ |
| `GET /api/phase2/devices/*` | ✅ | ✅ | ✅ | ✅ |

---

## Login & Authentication

### How to Login

1. Navigate to: `http://localhost:5170/login.html`
2. Enter email and password
3. Upon successful login, users are redirected to: `/phase2/index.html`

### Example Login Requests

**ICT Clerk**:
```json
POST /api/auth/login
{
  "Email": "ict.clerk@local",
  "Password": "P@ssw0rd1!"
}
```

**ICT Inspector**:
```json
POST /api/auth/login
{
  "Email": "ict.inspector@local",
  "Password": "P@ssw0rd1!"
}
```

**ICT Technician**:
```json
POST /api/auth/login
{
  "Email": "ict.technician@local",
  "Password": "P@ssw0rd1!"
}
```

**ICT Manager**:
```json
POST /api/auth/login
{
  "Email": "ict.manager@local",
  "Password": "P@ssw0rd1!"
}
```

---

## Authorization Errors

If a user tries to access an endpoint they're not authorized for, they'll receive:

**Response**: `403 Forbidden`
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.3",
  "title": "Forbidden",
  "status": 403
}
```

---

## Testing Role-Based Access

### 1. Login as ICT Clerk
```bash
# Login
POST /api/auth/login
{
  "Email": "ict.clerk@local",
  "Password": "P@ssw0rd1!"
}

# Should succeed
POST /api/phase2/receipting
{
  "GrvNumber": "GRV-001",
  "ClerkId": "ict.clerk@local",
  "Items": [...]
}

# Should fail (403 Forbidden)
POST /api/phase2/assessment/pre
```

### 2. Login as ICT Inspector
```bash
# Login
POST /api/auth/login
{
  "Email": "ict.inspector@local",
  "Password": "P@ssw0rd1!"
}

# Should succeed
POST /api/phase2/assessment/pre
{
  "DeviceId": 1,
  "Passed": true,
  "InspectorId": "ict.inspector@local",
  "Notes": "OK"
}

# Should fail (403 Forbidden)
POST /api/phase2/receipting
```

### 3. Login as ICT Technician
```bash
# Login
POST /api/auth/login
{
  "Email": "ict.technician@local",
  "Password": "P@ssw0rd1!"
}

# Should succeed
POST /api/phase2/assessment/detailed
{
  "DeviceId": 1,
  "TechnicianId": "ict.technician@local",
  "UnderWarranty": false,
  "Category": "HardwareFailure",
  ...
}

# Should fail (403 Forbidden)
POST /api/phase2/quality
```

### 4. Login as ICT Manager
```bash
# Login
POST /api/auth/login
{
  "Email": "ict.manager@local",
  "Password": "P@ssw0rd1!"
}

# Should succeed
POST /api/phase2/disposal/approve
{
  "DisposalId": 1,
  "ManagerId": "ict.manager@local",
  "ManagerPin": "1234",
  "ManagerSignature": "John Doe"
}

# Should fail (403 Forbidden)
POST /api/phase2/assessment/detailed
```

---

## Security Notes

1. **Authentication Required**: All Phase 2 endpoints require authentication
2. **Role-Based Authorization**: Each endpoint restricted to specific roles
3. **Manager PIN**: Hashed (SHA256) before storage
4. **Audit Trail**: All actions logged with user ID and timestamp
5. **Session Management**: Cookie-based authentication with 8-hour expiration

---

## Next Steps

1. **Restart the app** to ensure roles and users are seeded
2. **Test login** with each role
3. **Verify access control** by attempting unauthorized actions
4. **Build UI** that shows/hides features based on user role
