# DeviceDesk (DeviceNdabase)

Unified device tracking and stock management for school readiness projects, ICT deployments, and warehouse operations.

This repository contains the **DeviceDesk** application under the `DeviceDesk/` folder.

## Features

- Stock receiving and registration
- Bulk device imports (Excel / CSV)
- Serial number and IMEI tracking
- Procurement orders and close-out reporting (Phase 0)
- AI-assisted document ingest for receiving (Phase 1)
- Blind transfers, allocation, picking slips, and repairs (Phase 2)
- Dispatch, POD, and collection slips (Phase 3)
- SuperAdmin imported-device management
- Role-based dashboards and React admin shell at `/app`

## User Roles

- SuperAdmin
- Admin
- Receiver / Receiving Clerk
- TechOps / ICT roles
- Storage Manager / Allocator
- Dispatcher / Dispatch Clerk
- Orders Clerk

## Tech Stack

- ASP.NET Core (.NET 8)
- SQL Server + Entity Framework Core
- Static phase UIs + optional React frontend (`DeviceDesk/frontend`)

## Getting Started

1. Open `DeviceDesk/DeviceDesk.netcore.sln` (or run from `DeviceDesk/`).
2. Set the SQL Server connection string in `appsettings.Development.json`.
3. Run `dotnet run` from `DeviceDesk/` — migrations apply on startup.
4. For the React shell: `cd DeviceDesk/frontend && npm install && npm run build` (or publish with the csproj SPA target).

## Contributors

- Mandisa Biyela — [DeviceNdabase](https://github.com/MandisaBiyela/DeviceNdabase)
- Thamsanqa Ndelu — [Ndelu-Blose/DeviceDesk](https://github.com/Ndelu-Blose/DeviceDesk)
