# DeviceDesk – System Documentation

**Overview**
- Manages device lifecycle across four phases: Phase 0 (Procurement Intake), Phase 1 (Receiving), Phase 2 (ICT Center), Phase 3 (Dispatch).
- Tech stack: ASP.NET Core Web API, EF Core, Identity, Cookie auth; Bootstrap 5.3 and Bootstrap Icons 1.11; vanilla JS fragments for SPA-style navigation.
- Static UI served per phase, role-based landing and gating, API controllers for each workflow step.
- Database contexts: `DeviceDeskDbContext` (Phase 0), `Phase1DbContext` (Phase 1), `Phase2DbContext` (Phase 2), `ApplicationDbContext` (Identity).

**Authentication & Identity**
- Identity roles: `OrdersClerk`, `ReceivingClerk`, `Supervisor`, `Admin`, `IctClerk`, `IctInspector`, `IctTechnician`, `IctManager`, `DispatchClerk`.
- Seeded users and default password `P@ssw0rd1!`:
  - `orders.clerk@local` → Orders Clerk (Phase 0)
  - `receiving.clerk@local` → Receiving Clerk (Phase 1)
  - `ict.clerk@local`, `ict.inspector@local`, `ict.technician@local`, `ict.manager@local` → ICT Center roles (Phase 2)
  - `dispatch.clerk@local` → Dispatch Clerk (Phase 3)
- Role-based landing:
  - `OrdersClerk` → `/phase0/new.html`
  - `ReceivingClerk`/`Receiver` → `/phase1/dashboard.html`
  - ICT roles (`IctClerk`/`IctInspector`/`IctTechnician`/`IctManager`) → `/phase2/index.html`
  - `DispatchClerk` → `/dispatch/index.html`
- Auth endpoints:
  - `GET /api/auth/current-user` returns `{ fullName, role, ... }` for UI gating.
  - `POST /api/auth/logout` logs out and returns to login.

**Static UI Hosting**
- Phase 1 UI: `Modules/Phase1/Phase1/UI` mapped to `/phase1` (default files + static files).
- Phase 2 UI: `Modules/Phase2/UI` mapped to `/phase2`.
- Phase 3 UI (Dispatch): `Modules/Phase3/UI` mapped to `/dispatch`.
- Default routing:
  - `GET /` redirects to role-specific landing page.
  - `/phase1` and `/dispatch` redirect to their indexed hubs.

**Phase 0 – Procurement Intake**
- Purpose: Ingest new stock and R&R batches, prepare data for Phase 1 receiving.
- Services:
  - `CsvImportService` parses intake spreadsheets.
  - `DocumentService` handles document metadata.
  - `NewStockBatchService` manages new stock batch lifecycle.
  - `RnrBatchService` manages R&R batches.
- Controllers:
  - `Modules/Phase0/Phase0/Controllers/NewStockIntakeController.cs`
    - `GET /api/phase0/new/orders` list batches as “orders” for Phase 1 selection.
    - `GET /api/phase0/new/orders/{id}` get specific batch details for Phase 1.
- Key Concepts:
  - Batches treated as orders for Phase 1 receiving; supports manual entries and placeholder serial formats.
  - Bridges Phase 0→1 through endpoints consumed by Phase 1 receiving controller.

**Phase 1 – Receiving**
- Purpose: Receive stock against GRV, scan-in, blind copy, reconciliation, GRV generation.
- Services registered:
  - `ReceivingService`, `BlindCopyService`, `ScanningService`, `ReconciliationService`, `InventoryIntegrationService`, `GRVService`, `SpreadsheetParserService`, `NewStockScanningService`, `RnrBlindCopyService`, `RnrGrvService`, `ModelDrivenScanningService`.
- Controllers (examples):
  - `Modules/Phase1/Phase1/Controllers/ReceivingController.cs`
    - Integrates Phase 0 “orders” (new stock batches) via `GET /api/phase1/orders`.
  - `Modules/Phase1/Phase1/Controllers/RnrReceivingController.cs`
    - Comprehensive endpoints for R&R receiving, blind copy, reconciliation, and exports (large controller).
- UI:
  - Served at `/phase1` from `Modules/Phase1/Phase1/UI`.
  - Dashboard shows receiving workflows and GRV handling.
- Roles:
  - `Receiver`/`ReceivingClerk` access Phase 1; enforced via role gates and route guards in `Program.cs`.

**Phase 2 – ICT Center**
- Purpose: Receipting, pre-assessment, detailed inspection, quality assessment, disposal flow, audit, and basic user admin.
- Services:
  - `ReceiptingService`, `AssessmentService`, `QualityService`, `DisposalService`, `AuditService`; `IEmailSender` via `LoggingEmailSender` or `SmtpEmailSender`.
- Controllers:
  - `Phase2ReceiptingController.cs` – `POST /api/phase2/receipting` (Clerk).
  - `Phase2AssessmentController.cs` – pre-assessment and detailed inspection (Inspector/Technician).
  - `Phase2QualityController.cs` – `POST /api/phase2/quality` (Inspector).
  - `Phase2TechnicianController.cs` – technician queues and operations.
  - `Phase2DevicesController.cs` – `GET /api/phase2/devices/*` (all ICT roles).
  - `Phase2DisposalController.cs` – disposal request (Technician) and approval (Manager).
  - `Phase2AuditController.cs` – audit events and export.
  - `UserAdminController.cs` – basic user management (Clerk-only).
- UI:
  - Entry: `/phase2/index.html` from `Modules/Phase2/UI/index.html`.
  - Fragment-based navigation: receipting, pre-assessment, detailed inspection, quality, disposal-request, my-work (technician/inspector), devices (Clerk), users (Clerk).
  - Menu gating based on `current-user.role`; Inspector/Technician menus intentionally hide “View Devices”.
- Disposal Flow:
  - Technician: `POST /api/phase2/disposal/request` (reuses existing pending, blocks already-disposed).
  - Manager approval: `POST /api/phase2/disposal/approve` with `DisposalId`, `ManagerId`, `ManagerPin`, `ManagerSignature`.
  - `ApproveDisposalAsync` hashes PIN, stamps manager details, generates document path, sets `Phase2Stage.Disposal`.
- Key Endpoints (summary):
  - `POST /api/phase2/receipting` (Clerk) – GRV verification and acceptance.
  - `POST /api/phase2/assessment/pre` (Inspector) – pre-assessment.
  - `POST /api/phase2/assessment/detailed` (Technician) – inspection and routing.
  - `POST /api/phase2/quality` (Inspector) – quality assessment and rework tracking.
  - `GET /api/phase2/devices?stage=...&zone=...` – device queries (all ICT roles).
- Audit:
  - Logs actions with user, timestamp, device serial; export endpoints available to `IctManager`.
- Implementation Decisions & Fixes:
  - Detailed Inspection guard updated: backend blocks only when `PreAssessmentPassed == null`; both `true` and `false` proceed.
  - Disposal request returns `{ DisposalId, Reused }` and reuses existing pending request.
  - Technician detail “Back” button uses SPA-aware function (`goBackToTechMyWork()`).
  - Inspector/Technician menus do not include “View Devices”.

**Phase 3 – Dispatch**
- Purpose: Handle dispatch-ready devices, POD creation, delivery workflows.
- Controller:
  - `Modules/Phase3/Controllers/DispatchController.cs`
    - `[Authorize(Roles = DispatchClerk,Admin)]`.
    - Lists ready devices grouped by source; now includes both `AwaitingDispatch` and `Dispatch` stages for backward compatibility.
    - Creates PODs, moves stage to `SchoolAllocation`; safely sets scan-out fields only if null.
- UI:
  - Served at `/dispatch` from `Modules/Phase3/UI`.
  - Role-based landing for `DispatchClerk`.
- Integration with Phase 2:
  - ICT Clerk scan-out sets `Stage = AwaitingDispatch`.
  - Dispatch lists consider `AwaitingDispatch` primarily, with backward-compatible inclusion of `Dispatch`.

**Database & Migrations**
- Migrations applied on startup for `DeviceDeskDbContext`, `Phase1DbContext`, and `Phase2DbContext`.
- Identity seeding runs after migrations; resilient to failures.
- Snapshot models under `Migrations/*`; ensure connection strings in `appsettings.*.json`.

**Role-Based Page Guards**
- In `Program.cs`, static page requests (`/phase0`, `/phase1`, `/phase2`, `/dispatch`) are checked:
  - Unauthenticated → redirected to `/login.html?returnUrl=...`.
  - Authenticated but wrong role → redirected to their dashboard.
  - Guards ensure UI routes align with role policies; backend controllers also enforce authorization.

**Operational Workflow Summary**
- Phase 0: Intake batches → expose as orders for Phase 1.
- Phase 1: Receive against GRV → scan devices → blind copy → reconcile → generate GRV.
- Phase 2:
  - Clerk: Receipting and device/user admin.
  - Inspector: Pre-assessment, quality assessment, rework cycle.
  - Technician: Detailed inspection, warranty checks, disposal request.
  - Manager: Disposal approval (PIN + signature).
  - Audit: Full trail of actions.
- Phase 3: Dispatch device handling, POD creation, allocation; integrates with Phase 2’s scan-out.

**Key File Map**
- Backend
  - Phase 0: `Modules/Phase0/Phase0/Controllers/*`, `Modules/Phase0/Phase0/Services/*`
  - Phase 1: `Modules/Phase1/Phase1/Controllers/*`, `Modules/Phase1/Phase1/Services/*`
  - Phase 2: `Modules/Phase2/Controllers/*`, `Modules/Phase2/Services/*`, `Modules/Phase2/Models/*`, `Modules/Phase2/Data/Phase2DbContext.cs`
  - Phase 3: `Modules/Phase3/Controllers/*`, `Modules/Phase3/Services/*`
  - Infrastructure: `Infrastructure/Identity/*`, `Infrastructure/Data/*`, `Controllers/AuthController.cs`
  - Utilities: `Services/*`, `Middleware/*`
- Frontend
  - Phase 1 UI: `Modules/Phase1/Phase1/UI/*` served at `/phase1`
  - Phase 2 UI: `Modules/Phase2/UI/*` served at `/phase2`
  - Phase 3 UI: `Modules/Phase3/UI/*` served at `/dispatch`

**Testing & Verification**
- Login:
  - `http://localhost:5170/login.html` using seeded accounts; confirm redirection to role landing pages.
- Phase 2 Detailed Inspection:
  - With `PreAssessmentPassed: true/false` → `POST /api/phase2/assessment/detailed` returns `200` (no `InvalidOperationException`).
  - Missing `PreAssessmentPassed` (null) → blocked with explanatory message.
- Disposal Flow:
  - Technician request for already-disposed device → blocked.
  - Repeated disposal request → returns `Reused: true`, surfaces pending status badge.
  - Manager approval → requires `ManagerPin` + `ManagerSignature`, moves to `Disposal`.
- Dispatch:
  - Ready list shows devices in `AwaitingDispatch` (and `Dispatch` for legacy).
  - POD creation moves devices to `SchoolAllocation` without overwriting ICT scan-out data if present.

**Notes & Conventions**
- Source of truth for Phase 2 UI is `Modules/Phase2/UI/index.html`; deploy copies to `/phase2/index.html`.
- Fragment navigation does not use browser history; “Back” actions are explicit functions in the dashboard context.
- Enforce backend `[Authorize(Roles=...)]` policies; UI gating is not a substitute.

**Environment & Configuration**
- Ensure `app.UseStaticFiles()`, `AddControllers()`, `MapControllers()`, and static mappings for `/phase1`, `/phase2`, `/dispatch` exist.
- Email:
  - Configure `SmtpEmailSender` via `Email` section in config; otherwise `LoggingEmailSender` logs only.
- Swagger (dev):
  - Available at `/dev/swagger` when `Environment.IsDevelopment()`.

**Appendix – Seeded Accounts**
- Orders: `orders.clerk@local` / `P@ssw0rd1!`
- Receiving: `receiving.clerk@local` / `P@ssw0rd1!`
- ICT Center:
  - Clerk: `ict.clerk@local` / `P@ssw0rd1!`
  - Inspector: `ict.inspector@local` / `P@ssw0rd1!`
  - Technician: `ict.technician@local` / `P@ssw0rd1!`
  - Manager: `ict.manager@local` / `P@ssw0rd1!`
- Dispatch: `dispatch.clerk@local` / `P@ssw0rd1!`

# DeviceDesk – System Documentation
// ... existing code ...

## System Scope & Objectives
- Centralizes device lifecycle management across Procurement Intake (Phase 0), Receiving (Phase 1), ICT Center (Phase 2), and Dispatch (Phase 3).
- Provides role-based web UI and API supporting auditability, workflow staging, and authorizations (e.g., disposal approval).
- Ensures consistent data flow: intake → receiving → assessment/repair → quality → dispatch.

## Roles & Use Cases
- Orders Clerk (Phase 0)
  - Create and manage New Stock and R&R batches.
  - Prepare batch data for Phase 1 selection via “orders” endpoints.
- Receiving Clerk (Phase 1)
  - Select Phase 0 batch as an “order”, scan-in devices against GRV, reconcile counts, generate GRV.
  - Monitor dashboards for receiving progress.
- ICT Clerk (Phase 2)
  - Receipting: accept devices into ICT Center with GRV verification.
  - Admin: view device queries, manage technicians.
- ICT Inspector (Phase 2)
  - Pre-assessment: initial visual/functional checks; determine readiness for detailed inspection.
  - Quality assessment: verify repair outcomes, decide pass/fail, trigger rework cycles.
- ICT Technician (Phase 2)
  - Detailed inspection: warranty checks, repair routing, request disposal when unrepairable/uneconomical.
  - My Work queue management: pick tasks, update statuses.
- ICT Manager (Phase 2)
  - Disposal approval: authorize requests via PIN + signature; finalizes disposal records and device stage.
  - Access audit reporting/export.
- Dispatch Clerk (Phase 3)
  - List ready devices (AwaitingDispatch/Dispatch), create PODs, allocate to schools/customers.
  - Respect scan-out data originating from ICT Clerk.

## Business Processes
- Phase 0 – Intake
  - Create/import batches; normalize device rows; expose batches as “orders” for Phase 1.
- Phase 1 – Receiving
  - Select batch → perform blind copy/scanning → reconcile counts → generate GRV → hand over to Phase 2.
- Phase 2 – ICT Center
  - Step 1: Receipting (ICT Clerk)
    - Verify GRV and accept stock; categorize zones (New Stock vs R&R).
  - Step 1.5: Pre-assessment (Inspector)
    - Visual/functional checks; record pass/fail; unblock detailed inspection by setting `PreAssessmentPassed` (true/false).
  - Step 2: Detailed inspection (Technician)
    - Warranty determination; repairability; component categorization; request disposal if needed.
  - Step 3: Quality assessment (Inspector)
    - Validate repairs; pass/fail; increment rework count; loop back to technician if required.
  - Disposal flow (Technician → Manager)
    - Technician submits request; system reuses any pending request; Manager approves with PIN + signature; device moves to `Phase2Stage.Disposal`.
  - Audit
    - Log user, action, device serial, timestamp, and details across all steps.
- Phase 3 – Dispatch
  - ICT Clerk scan-out moves devices to `AwaitingDispatch`.
  - Dispatch listing shows both `AwaitingDispatch` and legacy `Dispatch`.
  - POD creation moves to `SchoolAllocation`, setting scan-out fields only if previously unset.

## Business Rules
- Receipting
  - Devices must be receipted before they appear in assessment/technician queues.
  - Prevent duplicates and ensure required fields (e.g., GRV references).
- Pre-assessment
  - Devices in disposal or already in deep technician workflows are excluded by design.
  - `PreAssessmentPassed` must be set (true/false) for downstream progression.
- Detailed inspection
  - Backend guard blocks only when `PreAssessmentPassed == null` (null means not completed).
  - Warranty devices route externally; non-warranty proceed to internal repair or disposal.
- Quality assessment
  - Failures increment `ReworkCount` and return to technician queue; passes move forward.
- Disposal
  - Request for already disposed devices is blocked.
  - Pending request reuse: new requests return `{ DisposalId, Reused: true }` without duplicating records.
  - Manager approval requires `ManagerPin` (stored as hash), `ManagerSignature`; sets `IsApproved`, `ApprovedAt`, and moves device to `Phase2Stage.Disposal`.
- Dispatch
  - Ready list prioritizes `AwaitingDispatch` but includes `Dispatch` for backward compatibility.
  - POD creation updates stage and respects existing scan-out data.

## Domain Model Overview (Selected)
- Phase2Device
  - Identifiers: `DeviceId`, `Serial`.
  - Stage/Zone: `Stage` (e.g., Receipting, Technician, Quality, Disposal, AwaitingDispatch), `Zone` (NewStock/R&R).
  - Receipting: `IctClerkId`, `ReceivingDate`, `VerificationStatus`.
  - Assessment: `PreAssessmentPassed`, `PreAssessmentInspectorId`; `TechnicianId`, `InspectionDate`, `UnderWarranty`, `Repairable`, `RepairCategory`, `DisposalRequested`.
  - Quality: `QaPassed`, `QaInspectorId`, `ReworkCount`.
  - Dispatch: scan-out fields used pre-POD (if implemented).
- DisposalRecord
  - Request: `RequestedBy`, `RequestedAt`, `Reason`.
  - Approval: `ApprovedBy`, `ManagerSignature`, `ManagerPinHash`, `ApprovedAt`, `IsApproved`.
  - `DocumentPath` for authorization references.
- AuditLog
  - `UserId`, `Action`, `DeviceId`, `DeviceSerial`, `Details`, `Timestamp`.
- Phase 0 Batches (New/R&R)
  - Batch metadata, counts, and devices used to bridge to Phase 1 “orders”.

## API Surface (Representative Endpoints & Roles)
- Auth
  - `GET /api/auth/current-user` → UI role gating.
  - `POST /api/auth/logout`.
- Phase 0
  - `GET /api/phase0/new/orders` → list batches as orders (for Phase 1).
  - `GET /api/phase0/new/orders/{id}` → batch details.
- Phase 1
  - `GET /api/phase1/orders` → list Phase 0 batches ready for receiving (transformed format).
  - Extensive endpoints in `RnrReceivingController` for scanning, reconciliation, exports.
- Phase 2
  - Receipting (ICT Clerk): `POST /api/phase2/receipting`.
  - Assessment:
    - Pre-assessment (Inspector): `POST /api/phase2/assessment/pre`.
    - Detailed inspection (Technician): `POST /api/phase2/assessment/detailed`.
  - Quality (Inspector): `POST /api/phase2/quality`.
  - Devices (All ICT roles): `GET /api/phase2/devices/*` and queries such as `serial`, `stage`, `zone`.
  - Disposal:
    - Request (Technician): `POST /api/phase2/disposal/request`.
    - Approve (Manager): `POST /api/phase2/disposal/approve`.
  - Audit (Manager):
    - Events: `GET /api/phase2/audit/events`.
    - Export: `GET /api/phase2/audit/export`.
  - User Admin (Clerk): `GET /api/phase2/users?role=IctTechnician` (and related CRUD).
- Phase 3 (Dispatch Clerk)
  - Device listing: `GET /api/phase3/dispatch/devices` grouped by source, filtered for `AwaitingDispatch`/`Dispatch`.
  - POD creation and allocation endpoints (moves to `SchoolAllocation`).

## Methods & Responsibilities (Key Service Operations)
- AssessmentService
  - `DetailedInspectionAsync(...)` enforces pre-assessment completion (`PreAssessmentPassed != null`); routes repair/disposal/warranty.
  - Pre-assessment method handles `PreAssessmentPassed`, inspector attribution, and notes.
- DisposalService
  - `RequestDisposalAsync(...)` creates or reuses pending request; blocks already-disposed devices; returns `(DisposalId, Reused)`.
  - `ApproveDisposalAsync(...)` hashes `ManagerPin` (SHA256), stamps manager fields, generates `DocumentPath`, sets device `Stage = Disposal`.
  - `ListPendingByTechnicianAsync(...)` supports technician queues.
- ReceiptingService
  - Integrates Phase 1 scanning records, verifies GRV, and records ICT Center intake with `ReceivingDate`, `VerificationStatus`.
- QualityService
  - Evaluates technician outcomes; sets `QaPassed`; updates `ReworkCount` and stage.
- DispatchController (Phase 3)
  - Ready list query prefers `AwaitingDispatch` but includes `Dispatch`.
  - POD creation transitions to `SchoolAllocation` and respects existing scan-out fields.

## Security & Authorization
- Backend authorization via `[Authorize(Roles = ...)]` on controllers/actions; UI menus are gated but not trusted alone.
- Static page guards in `Program.cs` redirect unauthenticated users to `login.html` and wrong-role users to their dashboards.
- Disposal approval requires additional human-in-the-loop verification (PIN + signature) beyond role membership.

## UI Architecture
- Phase 2 dashboard (`/phase2/index.html`)
  - Loads `currentUser` via `GET /api/auth/current-user`.
  - Role-based sidebar; fragment loaders:
    - `loadReceiptingView()`, `loadPreAssessmentView()`, `loadDetailedInspectionView()`, `loadQualityView()`,
      `loadDisposalRequestView()`, `loadTechMyWorkView()`, `loadInspectorMyWorkView()`, `loadDevicesView()`.
  - `renderWithScripts(targetId, html)` injects fragments and executes inline scripts in order.
  - No browser history routing; back navigation uses explicit functions (e.g., `goBackToTechMyWork()`).
- Phase 1 and Phase 3 UIs served at `/phase1` and `/dispatch` respectively from `Modules/.../UI`.

## Configuration & Environment
- Database contexts:
  - `DeviceDeskDbContext`, `Phase1DbContext`, `Phase2DbContext`, `ApplicationDbContext`.
- Migrations applied at startup; identity is seeded regardless of partial migration issues.
- Email sender:
  - `SmtpEmailSender` configured via `Email` section in `appsettings.*.json`, else `LoggingEmailSender`.
- Swagger (dev only):
  - `GET /dev/swagger` with endpoint `/dev/swagger/v1/swagger.json`.

## Error Handling & Troubleshooting
- Pre-assessment null guard:
  - Backend updated to allow `PreAssessmentPassed: true/false`; block only when `null`.
- Disposal request duplicates:
  - Reuse pending request (`Reused: true`) and show appropriate UI badge.
- Stream errors:
  - “Cannot access a closed Stream” were symptomatic of earlier `InvalidOperationException` during detailed inspection, resolved by the guard fix.
- Access issues:
  - Verify role membership and ensure `GET /api/auth/current-user` returns correct role; UI depends on it.

## Data Lifecycle & Transitions
- Intake → Receiving: Phase 0 batches exposed to Phase 1.
- Receiving → ICT: GRV-based acceptance; devices enter Phase 2 queues.
- ICT processing: assessment → inspection → quality → disposal or forward progression.
- ICT → Dispatch: scan-out sets `AwaitingDispatch`; POD creation finalizes allocation.

## Integration Points
- Phase 0→1: batch-as-order endpoints; Phase 1 transforms batch objects for UI compatibility.
- Phase 1→2: GRV/scanning data leveraged in `ReceiptingService` to accept stock.
- Phase 2→3: stage transitions and scan-out fields consumed by Dispatch listing and POD creation.

## Known Decisions & Fixes
- Detailed inspection backend guard changed to block only when `PreAssessmentPassed == null`.
- Disposal flow returns `Reused` and prevents duplicate records; UI surfaces status badges.
- Inspector/Technician menus hide “View Devices” to maintain role focus.
- Technician detail back navigation made SPA-aware to avoid broken history buttons.

## Run & Debug Guide
- Launch:
  - `dotnet run` at the project root; visit `http://localhost:5170/`.
- Login:
  - Use seeded accounts; confirm redirection to role-specific landing pages.
- Validate flows:
  - Phase 2 detailed inspection returns `200` when `PreAssessmentPassed: true/false` and blocks when `null`.
  - Disposal request shows reuse/new messaging; approval moves to `Disposal`.
  - Dispatch lists `AwaitingDispatch` devices; POD moves stage to `SchoolAllocation`.

## Glossary
- GRV: Goods Received Voucher; formal record used during receiving.
- POD: Proof of Delivery; dispatch document finalizing allocation.
- R&R: Repair and Replace; batch type indicating refurbishment workflow.
- AwaitingDispatch: interim stage after ICT scan-out, before dispatch POD.

## Appendix – Identity & Roles
- Seeded roles:
  - `OrdersClerk`, `ReceivingClerk`, `Supervisor`, `Admin`, `IctClerk`, `IctInspector`, `IctTechnician`, `IctManager`, `DispatchClerk`.
- Seeded users (password `P@ssw0rd1!`):
  - `orders.clerk@local`, `receiving.clerk@local`, `ict.clerk@local`, `ict.inspector@local`, `ict.technician@local`, `ict.manager@local`, `dispatch.clerk@local`.

## Appendix – Health & Diagnostics
- `DbHealthStartupLogger` applies migrations and reports status at startup.
- `DbHealthController` (`/api/db`) exposes health checks over `DeviceDeskDbContext`, `Phase1DbContext`, `Phase2DbContext`, and `ApplicationDbContext`.
// ... existing code ...

# DeviceDesk – System Documentation
// ... existing code ...

## API Catalog (Observed)

- AuthController
  - GET `/api/auth/current-user`
    - Roles: Authenticated
    - Returns current user profile and roles for UI gating.
  - POST `/api/auth/logout`
    - Roles: Authenticated
    - Clears auth session and redirects to login.

- Phase 0 – New Stock Intake
  - GET `/api/phase0/new/orders`
    - Roles: `OrdersClerk`, `Admin`
    - Lists batches prepared as “orders” for Phase 1 selection.
  - GET `/api/phase0/new/orders/{id}`
    - Roles: `OrdersClerk`, `Admin`
    - Returns specific batch details in order-like format.

- Phase 1 – Receiving
  - GET `/api/phase1/orders`
    - Roles: `ReceivingClerk`, `Admin`
    - Lists Phase 0 batches ready for receiving (transformed for UI).
  - R&R Receiving Controller (large)
    - Roles: `ReceivingClerk`, `Admin`
    - Endpoints cover blind copy, scanning, reconciliation, exports (see `RnrReceivingController.cs`).

- Phase 2 – ICT Center
  - Devices API
    - GET `/api/phase2/devices` (query by `serial`, `stage`, `zone`)
    - Roles: `IctClerk`, `IctInspector`, `IctTechnician`, `IctManager`
    - Returns device lists/details for ICT users.
  - Receipting
    - POST `/api/phase2/receipting`
    - Roles: `IctClerk`
    - Receives devices into ICT Center with GRV verification.
  - Assessment
    - POST `/api/phase2/assessment/pre`
      - Roles: `IctInspector`
      - Records pre-assessment (visual/functional), sets `PreAssessmentPassed`.
    - POST `/api/phase2/assessment/detailed`
      - Roles: `IctTechnician`
      - Runs detailed inspection, routes outcomes (repair/disposal/warranty).
  - Quality
    - POST `/api/phase2/quality`
    - Roles: `IctInspector`
    - Records QA outcomes; drives rework or pass-forward.
  - Disposal
    - POST `/api/phase2/disposal/request`
      - Roles: `IctTechnician`
      - Creates or reuses a pending disposal request; blocks already-disposed devices.
    - POST `/api/phase2/disposal/approve`
      - Roles: `IctManager`
      - Approves disposal (requires manager PIN + signature), moves device to `Disposal` stage.
  - Audit (Phase 2)
    - GET `/api/phase2/audit/events`
      - Roles: `IctManager`, `Admin`
      - Returns audit logs for ICT actions.
    - GET `/api/phase2/audit/export`
      - Roles: `IctManager`, `Admin`
      - Exports audit data.

- Phase 3 – Dispatch
  - Dispatch listing
    - GET `/api/phase3/dispatch/devices`
    - Roles: `DispatchClerk`, `Admin`
    - Lists devices ready to dispatch; includes `AwaitingDispatch` and legacy `Dispatch` stages for compatibility.
  - POD creation
    - Roles: `DispatchClerk`, `Admin`
    - Creates POD and moves device to `SchoolAllocation` (see `DispatchController.cs` for exact route), preserving ICT scan-out fields if already set.

// ... existing code ...

## Data Models (Key Entities)

- Phase2Device (ICT Center)
  - Identity: `DeviceId`, `Serial`.
  - Lifecycle:
    - Receipting: `IctClerkId`, `ReceivingDate`, `VerificationStatus`, `Zone`.
    - Assessment: `PreAssessmentPassed` (bool?), `PreAssessmentInspectorId`, notes.
    - Technician: `TechnicianId`, `InspectionDate`, `UnderWarranty`, `Repairable`, `RepairCategory`, `DisposalRequested`.
    - Quality: `QaPassed`, `QaInspectorId`, `ReworkCount`.
    - Dispatch: `Stage` transitions including `AwaitingDispatch` and downstream stages.
  - Behavior: stage-based routing across ICT workflows.

- DisposalRecord
  - Request: `DisposalId`, `DeviceId`, `RequestedBy`, `RequestedAt`, `Reason`.
  - Approval: `ApprovedBy`, `ManagerSignature`, `ManagerPinHash`, `ApprovedAt`, `IsApproved`.
  - Document: `DocumentPath` for authorization artifacts.

- AuditLog
  - `UserId`, `Action`, `DeviceId`, `DeviceSerial`, `Details`, `Timestamp`.

- Phase 0 Batch (New/R&R)
  - Batch metadata: `BatchId`, `OrderNumber`, `SupplierName`, `FileName`, `Added`, `CreatedAt`.
  - Devices aggregated for Phase 1 selections.

Note: Exact field names/types reside in `Modules/Phase2/Models` and contexts; the above reflects the operational schema used in services and controllers.

// ... existing code ...

## Core Methods & Behaviors

- AssessmentService
  - `DetailedInspectionAsync(...)`
    - Guard: allows when `PreAssessmentPassed` is set (`true` or `false`), blocks only when `null`.
    - Outcome routing: warranty, repairable, disposal request triggers, stage updates.
  - Pre-assessment method
    - Records `PreAssessmentPassed`, inspector attribution, notes, and eligibility for detailed inspection.

- DisposalService
  - `RequestDisposalAsync(...)`
    - Blocks when device already in `Disposal`; reuses any pending disposal record.
    - Returns `(DisposalId, Reused)` for UI messaging.
  - `ApproveDisposalAsync(...)`
    - Hashes manager PIN (SHA256), stamps manager fields, generates `DocumentPath`.
    - Moves device to `Phase2Stage.Disposal`; ensures audit entry.
  - `ListPendingByTechnicianAsync(...)`
    - Drives technician “My Work” queues for disposal pending items.

- ReceiptingService
  - Integrates Phase 1 scanning/GRV data; sets `ReceivingDate` and `VerificationStatus`.
  - Ensures receipting precedes appearance in other ICT queues.

- QualityService
  - Writes QA status (`QaPassed`), updates `ReworkCount`, loops back to technician or finalizes.

- DispatchController (Phase 3)
  - Ready device query filters `AwaitingDispatch` and `Dispatch` stages.
  - POD creation moves to `SchoolAllocation`; preserves ICT scan-out fields if present.

// ... existing code ...

## Response Contracts & Statuses

- Success: `200 OK` with domain DTOs (devices, batches, audit entries).
- Validation failures: `400 Bad Request` (missing required fields, invalid state).
- Auth failures: `401 Unauthorized` / `403 Forbidden` for wrong/no role.
- Not found: `404 Not Found` (missing batch/device).
- Server errors: `500 Internal Server Error` with `details` message where applicable.

Examples:
- Detailed Inspection success after frontend/backend guard fix:
  - `POST /api/phase2/assessment/detailed` → `200 OK` when `PreAssessmentPassed` is `true` or `false`.
- Disposal Request:
  - New: `{ disposalId, reused: false }`
  - Existing pending: `{ disposalId, reused: true }`
  - Already disposed: `400 Bad Request` or `409 Conflict` depending on controller implementation.

// ... existing code ...

## End-to-End Workflow Sequences

- Receive Device (Phase 0 → 1 → 2)
  - Phase 0 creates batch → Phase 1 selects order → scans & reconciles → GRV generated → Phase 2 receipting accepts device, categorizes zone.

- ICT Processing (Phase 2)
  - Pre-assessment (Inspector) → sets `PreAssessmentPassed` → Detailed inspection (Technician) with warranty/repair/disposal → Quality assessment (Inspector) with pass or rework loops → Stage transitions recorded and audited.

- Disposal (Technician → Manager)
  - Technician requests disposal → system reuses pending if exists → Manager approves with PIN/signature → device moves to `Disposal` → document path recorded → audit trail.

- Dispatch (Phase 2 → 3)
  - ICT scan-out sets `AwaitingDispatch` → Dispatch listing shows ready items → POD creation → `SchoolAllocation` stage → final delivery records.

// ... existing code ...

## Configuration & Deployment

- Services registered per phase in `Program.cs`; static UI mappings:
  - `/phase1` → `Modules/Phase1/Phase1/UI`
  - `/phase2` → `Modules/Phase2/UI`
  - `/dispatch` → `Modules/Phase3/UI`
- Migrations applied on startup for `DeviceDeskDbContext`, `Phase1DbContext`, `Phase2DbContext`.
- Identity seeding creates roles and users with password `P@ssw0rd1!`.
- Email:
  - `SmtpEmailSender` configured via `Email` section; else `LoggingEmailSender`.
- Swagger (dev):
  - `/dev/swagger` with `v1` document; use for API exploration in development.

// ... existing code ..

// ... existing code ...

**Endpoint Catalog (Expanded)**

- This catalog lists HTTP method, route, expected auth roles, and notable payloads/responses for Phases 0, 1, and 3.

**Phase 0 – New Stock Intake**
- `POST /api/phase0/new/import`
  - Roles: Orders/Receiving (unrestricted in code)
  - Payload: `multipart/form-data` with file field `file` (CSV)
  - Response: `ImportResultDto` with counts (`added`, `duplicates`, `invalid`, etc.)
- `POST /api/phase0/new/import-manual`
  - Roles: Orders/Receiving
  - Payload: `form-data` with `itemsJson` (array of items) and optional `pack` file
  - Behaviors:
    - Device-style: uses `serial` or `imei`, deduped across batch and DB
    - Order-style: `deviceType` + `qty` creates placeholder serials `PENDING-MANUAL-<type>-<GUID8>`
  - Response: `{ batchId, added, duplicates, invalid, total, packUploaded }`
- `POST /api/phase0/new/documents`
  - Roles: Orders/Receiving
  - Payload: `file` and optional `docType` (default `PO`)
  - Response: `{ documentId, fileName, docType }`
- `GET /api/phase0/new/batches`
  - Roles: Orders/Receiving
  - Query: `page`, `pageSize`
  - Response: paginated batches with `Id`, `CreatedAt`, `SourceFileName`, `Items`
- `GET /api/phase0/new/batches/{id}/items`
  - Roles: Orders/Receiving
  - Query: `page`, `pageSize`, `q`
  - Response: paginated items with device fields and stats
- `GET /api/phase0/new/items`
  - Roles: Orders/Receiving
  - Query: `page`, `pageSize`, `q`, `from`, `to`
  - Response: paginated global list, stats, and batch file metadata
- `GET /api/phase0/new/orders`
  - Roles: Receiving
  - Response: batches formatted as “orders” for Phase 1 (`orderId`, `orderNumber`, `supplierName`, totals)
- `GET /api/phase0/new/orders/{id}`
  - Roles: Receiving
  - Response: order details, grouped devices `{ brand, model, deviceType, quantityExpected }`
- `GET /api/phase0/new/items/export`
  - Roles: Orders/Receiving
  - Query: `q`, `from`, `to`
  - Response: CSV file

**Phase 0 – RNR Intake**
- `POST /api/phase0/rnr/import`
  - Roles: Orders/Receiving
  - Payload: `multipart/form-data` with `file` (CSV/XLSX)
  - Response: `ImportResultDto`
- `POST /api/phase0/rnr/import-manual`
  - Roles: Orders/Receiving
  - Payload: `itemsJson`, optional `pack`
  - Response: `{ batchId, added, duplicates, invalid, total, packUploaded }`
- `POST /api/phase0/rnr/documents?batchId={guid}&docType=RNR_HANDOVER`
  - Roles: Orders/Receiving
  - Payload: `file`
  - Response: `{ documentId, fileName, docType }`
- `GET /api/phase0/rnr/collection-slips`
  - Roles: Receiving
  - Response: Phase 0 RNR batches in `PendingScan` for Phase 1
- `GET /api/phase0/rnr/batches`
  - Roles: Orders/Receiving
  - Query: `page`, `pageSize`
  - Response: paginated RNR batches
- `GET /api/phase0/rnr/batches/{id}/items`
  - Roles: Orders/Receiving
  - Query: `page`, `pageSize`, `q`
  - Response: batch items and quick stats
- `GET /api/phase0/rnr/items`
  - Roles: Orders/Receiving
  - Query: `page`, `pageSize`, `q`, `from`, `to`
  - Response: global RNR items view and stats
- `GET /api/phase0/rnr/items/export`
  - Roles: Orders/Receiving
  - Response: CSV file

**Phase 0 – Unified Listings**
- `GET /api/phase0/devices/{type}`
  - Roles: Orders/Receiving
  - Path param `type`: `rnr` | `new`
  - Query: `page`, `pageSize`, `q`, `from`, `to`
  - Response: paginated devices + stats
- `GET /api/phase0/batches/{type}`
  - Roles: Orders/Receiving
  - Path param `type`: `rnr` | `new`
  - Query: `page`, `pageSize`
  - Response: paginated batch listing
- `GET /api/phase0/batches/{id}/items`
  - Roles: Orders/Receiving
  - Query: `page`, `pageSize`
  - Response: items in batch + stats
- `GET /api/phase0/batches/{id}/export`
  - Roles: Orders/Receiving
  - Response: CSV export for specific batch

**Phase 1 – Receiving (New Stock)**
- `GET /api/phase1/receiving/list`
  - Roles: Receiving
  - Response: all receiving batches with stats for the list page
- `GET /api/phase1/receiving/orders`
  - Roles: Receiving
  - Response: Phase 0 `NewStockBatch` in `PendingScan`, mapped to order-like DTO
- `GET /api/phase1/receiving/orders/{batchId}`
  - Roles: Receiving
  - Response: batch details + lines (`brand`, `model`, `deviceType`, `quantityOrdered`)
- `POST /api/phase1/receiving/batches`
  - Roles: Receiving
  - Payload: `CreateReceivingBatchRequest` (server-side DTO)
  - Response: `ReceivingBatchDto`
- `GET /api/phase1/receiving/batches/{id}`
  - Roles: Receiving
  - Response: `ReceivingBatchDto`
- `GET /api/phase1/receiving/batches/{id}/blind-copy`
  - Roles: Receiving
  - Response: PDF
- `POST /api/phase1/receiving/batches/{batchId}/documents?docType=INVOICE`
  - Roles: Receiving
  - Payload: `file`
  - Response: `{ documentId, fileName, docType }`
- `GET /api/phase1/receiving/batches/{batchId}/documents`
  - Roles: Receiving
  - Response: documents metadata
- `GET /api/phase1/receiving/documents/{documentId}/download`
  - Roles: Receiving
  - Response: document file download
- `DELETE /api/phase1/receiving/documents/{documentId}`
  - Roles: Receiving
  - Response: `{ message }`
- `POST /api/phase1/receiving/batches/{batchId}/spreadsheet`
  - Roles: Receiving
  - Payload: `file` (`.xlsx`, `.xls`, `.csv`)
  - Response: parse summary `{ message, documentId, fileName, totalRows, validRows, devices[], errors[] }`

**Phase 1 – RNR Receiving**
- `GET /api/phase1/rnr/health`
  - Roles: Receiving
  - Response: `{ status, timestamp, dbContext }`
- `GET /api/phase1/rnr/collection-slips`
  - Roles: Receiving
  - Response: Phase 0 RNR slips in `PendingScan` as dropdown data
- `GET /api/phase1/rnr/batches/{batchId}/blind-copy`
  - Roles: Receiving
  - Response: RNR blind copy PDF (PAN, hidden quantities)
- `POST /api/phase1/rnr/batches/{batchId}/scan-item`
  - Roles: Receiving
  - Payload: `{ brand, model, deviceType }`
  - Response: updated item and batch counters
- `POST /api/phase1/rnr/batches/{batchId}/generate-grv`
  - Roles: Receiving
  - Precondition: batch `Verified`
  - Response: GRV PDF stream (also sets status to `GRVIssued`)
- `GET /api/phase1/rnr/batches/{batchId}/expected-serials`
  - Roles: Receiving
  - Response: `{ expected: [normalizedSerials] }`
- `POST /api/phase1/rnr/batches/{id}/scan`
  - Roles: Receiving
  - Payload: `{ serial, clerk? }`
  - Behavior: normalizes, rejects unexpected; records only matched scans
- `POST /api/phase1/rnr/batches/{id}/complete-scanning`
  - Roles: Receiving
  - Response: `{ ok, missing[], unexpected[], nextUrl }`
- `GET /api/phase1/rnr/batches/{id}/summary`
  - Roles: Receiving
  - Response: counts and `missingList` (unexpected always 0; rejected)
- `GET /api/phase1/rnr/batches/{id}/header`
  - Roles: Receiving
  - Response: header meta (slip number, school, counts)
- `GET /api/phase1/rnr/batches/{id}/reconcile`
  - Roles: Receiving
  - Response: `{ expected, scanned, matched, missing, unexpected, hasVariance, varianceCount }`
- `POST /api/phase1/rnr/batches/{id}/complete`
  - Roles: Receiving
  - Response: `{ status: "Completed" }`
- `GET /api/phase1/rnr/batches/{id}/blind-transfer`
  - Roles: Receiving
  - Response: HTML blind transfer copy listing expected items
- `GET /api/phase1/rnr/batches/{id}/items`
  - Roles: Receiving
  - Response: scanned items listing (with scan markers)
- `GET /api/phase1/rnr/batches/{batchId}/scans`
  - Roles: Receiving
  - Response: scan records with status

**Phase 1 – Reconciliation & Inventory**
- `POST /api/phase1/reconciliation/start-scanning`
  - Roles: Receiving
  - Payload: `StartScanningRequest`
  - Response: `ReconciliationStatusDto`
- `POST /api/phase1/reconciliation/complete-scanning`
  - Roles: Receiving
  - Payload: `CompleteScanningRequest`
  - Response: `ReconciliationStatusDto`
- `POST /api/phase1/reconciliation/submit-count`
  - Roles: Receiving
  - Payload: `SubmitCountRequest`
  - Response: `ReconciliationStatusDto`
- `POST /api/phase1/reconciliation/resolve-variance`
  - Roles: Receiving
  - Payload: `ResolveVarianceRequest`
  - Response: `ReconciliationStatusDto`
- `GET /api/phase1/reconciliation/status/{batchId}`
  - Roles: Receiving
  - Response: `ReconciliationStatusDto`
- `POST /api/phase1/reconciliation/generate-grv/{batchId}`
  - Roles: Receiving
  - Response: `GRVDto`
- `GET /api/phase1/reconciliation/grv/{grvId}/pdf`
  - Roles: Receiving
  - Response: GRV PDF stream
- `GET /api/phase1/inventory/stats`
  - Roles: Receiving
  - Response: `InventoryStatsDto` (Phase 0 + Phase 1)
- `GET /api/phase1/inventory/check-duplicates/{batchId}`
  - Roles: Receiving
  - Response: `{ duplicates: [string], count }`
- `POST /api/phase1/inventory/transfer/{batchId}`
  - Roles: Receiving
  - Response: `{ message, count }`

**Phase 3 – Dispatch**
- `GET /api/dispatch/devices`
  - Roles: `DispatchClerk`, `IctClerk`, `Admin`
  - Query: `zone`, `serial`
  - Response: devices in `Phase2Stage.Dispatch` (id, serial, zone, updatedAt)
- `GET /api/dispatch/devices/{id}`
  - Roles: `DispatchClerk`, `IctClerk`, `Admin`
  - Response: device details (id, serial, zone, stage, updatedAt)
- `POST /api/dispatch/pods`
  - Roles: `DispatchClerk`, `IctClerk`, `Admin`
  - Payload: `CreatePodRequest { StockType, SourceReference, SchoolName, District?, Emis?, DeviceIds[] }`
  - Side effects: updates devices to `SchoolAllocation`, sets `ScannedOut*` fields; generates POD + Delivery Note PDFs
  - Response: `CreatePodResponse { PodNumber, PodDocumentId, DeliveryNoteDocumentId, PodFileName, DeliveryNoteFileName, LinkedDevices }`
- `GET /api/phase3/dispatch/ready-list`
  - Roles: `DispatchClerk`, `IctClerk`, `Admin`
  - Response: grouped dispatch-ready sources `{ sourceType, sourceNumber, school, quantity, lastUpdated }`
- `GET /api/dispatch/pods/{podNumber}`
  - Roles: `DispatchClerk`, `IctClerk`, `Admin`
  - Response: POD metadata (ids, status, documents)
- `POST /api/dispatch/pods/{podNumber}/signed-pod`
  - Roles: `DispatchClerk`, `IctClerk`, `Admin`
  - Payload: `file` (`PDF/PNG/JPEG`)
  - Response: `{ documentId, fileName, contentType, uploadedAt }` and POD status set to `Signed`
- `GET /api/dispatch/pods/{podNumber}/signed-pod`
  - Roles: `DispatchClerk`, `IctClerk`, `Admin`
  - Response: signed POD file download
- `GET /api/dispatch/pods/{podNumber}/pod-pdf`
  - Roles: `DispatchClerk`, `IctClerk`, `Admin`
  - Response: POD PDF
- `GET /api/dispatch/pods/{podNumber}/delivery-note-pdf`
  - Roles: `DispatchClerk`, `IctClerk`, `Admin`
  - Response: Delivery Note PDF

**Core Data Models (Expanded)**
- `Infrastructure/Data/DeviceDeskDbContext.cs`
  - `Device`
    - Keys: `Id: Guid`
    - Identifiers: `SerialNumber`, `IMEI`
    - Classification: `Brand`, `Model`, `DeviceType`, `Description`
    - Source: `Source` (`"RNR"` | `"NEW"`), `OrderNumber?`
    - Links: `BatchId?`, `SchoolId?`
    - Timestamps: `ImportedAt`
  - `DeviceImportBatch`
    - Keys: `BatchId: Guid`
    - Source: `Source` (`"RNR"` | `"NEW"`), `OrderNumber?`, `FileName?`, `SchoolId?`
    - Counters: `Total`, `Added`, `Duplicates`, `Invalid`
    - Timestamps: `CreatedAt`
  - `Document`
    - Keys: `DocumentId: long`
    - Links: `BatchId?`, `SchoolId?`
    - File: `DocType`, `FileName`, `ContentType`, `FileData`, `UploadedAt`
  - `RnrBatch`
    - Keys: `BatchId: Guid`
    - Meta: `BatchNumber`, `CollectionSlipNumber`, `SchoolId?`, `SchoolName?`
    - Totals: `TotalQuantityExpected`, `TotalQuantityScanned`
    - Status: `RnrBatchStatus` (`PendingScan`, `ScanningInProgress`, `Verified`, `VarianceDetected`, `GRVIssued`, `Completed`, `Cancelled`)
    - Audit: `CreatedBy`, `CreatedAt`, `ConfirmedBy?`, `ConfirmedAt?`, `GRVNumber?`
    - Items: `List<RnrBatchItem>`
  - `RnrBatchItem`
    - Keys: `ItemId: Guid`, `BatchId: Guid`
    - Classification: `Brand?`, `Model?`, `DeviceType?`, `Description?`
    - Quantities: `QuantityExpected`, `QuantityScanned`
- `Modules/Phase0/Models/NewStockBatch.cs` (Phase 0 new stock)
  - Keys: `BatchId: Guid`
  - Meta: `BatchNumber`, `SupplierName`, `InvoiceNumber?`, `ExpectedDeliveryDate?`
  - Totals: `TotalQuantityExpected`, `TotalQuantityScanned`
  - Status: `NewStockBatchStatus` + `StatusText`
  - Items: line-level `Brand`, `Model`, `DeviceType`, `Description`, `QuantityExpected`, `QuantityScanned`

**End-to-End Sequence Flows**
- New Stock: Phase 0 → Phase 1
  - Upload CSV or manual items (`/api/phase0/new/import` or `import-manual`)
  - Phase 0 exposes batch as order (`/api/phase0/new/orders`, `/orders/{id}`)
  - Phase 1 creates receiving batch (`/api/phase1/receiving/batches`), generates blind copy
  - Scan devices and reconcile (`/api/phase1/reconciliation/*`)
  - Generate GRV (`/api/phase1/reconciliation/generate-grv/{batchId}`)
  - Transfer to inventory if needed (`/api/phase1/inventory/transfer/{batchId}`)
- RNR: Phase 0 → Phase 1
  - Upload slip CSV/XLSX or manual entry (`/api/phase0/rnr/import`, `import-manual`)
  - Phase 0 exposes collection slips (`/api/phase0/rnr/collection-slips`)
  - Phase 1 RNR scanning (`/api/phase1/rnr/*`), blind copy, complete scanning, GRV generation
- Dispatch: Phase 2 → Phase 3
  - Phase 2 devices at `Dispatch` stage listed (`/api/dispatch/devices`)
  - Create POD and Delivery Note (`/api/dispatch/pods`) with selected devices
  - Retrieve dispatch-ready sources (`/api/phase3/dispatch/ready-list`)
  - Upload signed POD (`/api/dispatch/pods/{podNumber}/signed-pod`) and serve documents

// ... existing code ...