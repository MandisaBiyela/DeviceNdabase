# Phase 2 - ICT Center Login Credentials

All users have the same password: **P@ssw0rd1!**

## ICT Center Roles & Accounts

| Role | Email | Full Name | Department | Responsibilities |
|------|-------|-----------|------------|------------------|
| **ICT Clerk** | `ict.clerk@local` | ICT Clerk | Phase 2 - ICT Center | Receipting & Verification (GRV scanning, stock acceptance) |
| **ICT Inspector** | `ict.inspector@local` | ICT Inspector | Phase 2 - ICT Center | Pre-Assessment (Visual/functional checks) |
| **ICT Technician** | `ict.technician@local` | ICT Technician | Phase 2 - ICT Center | Detailed Inspection (Warranty checks, repair routing) |
| **ICT Quality Inspector** | `ict.qa@local` | ICT Quality Inspector | Phase 2 - ICT Center | Quality Assessment (Pass/fail, rework decisions) |
| **ICT Manager** | `ict.manager@local` | ICT Manager | Phase 2 - ICT Center | Disposal Approval (Manager authorization) |

## Access Control

- **Receipting**: Only `IctClerk` can create receipts
- **Pre-Assessment**: Only `IctInspector` can perform pre-assessment
- **Detailed Inspection**: Only `IctTechnician` can perform detailed inspection
- **Quality Assessment**: Only `IctQualityInspector` can record quality results
- **Disposal Approval**: Only `IctManager` can approve disposal
- **Device Queries**: All ICT Center roles can view devices

## Login Flow

1. Navigate to: `http://localhost:5170/login.html`
2. Enter email and password
3. Upon successful login, ICT Center users are redirected to: `/phase2/index.html`

## API Endpoints (Role-Protected)

- `POST /api/phase2/receipting` → **IctClerk**
- `POST /api/phase2/assessment/pre` → **IctInspector**
- `POST /api/phase2/assessment/detailed` → **IctTechnician**
- `POST /api/phase2/quality` → **IctQualityInspector**
- `POST /api/phase2/disposal/approve` → **IctManager**
- `GET /api/phase2/devices/*` → **All ICT roles**
