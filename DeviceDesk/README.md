DeviceDesk
Internal Device Lifecycle Management System

DeviceDesk is an enterprise-focused system designed to manage the full lifecycle of devices across structured operational environments.
It supports procurement, receiving, verification, allocation, repair tracking, storage transfers, and reporting workflows with audit-ready traceability.

Overview

DeviceDesk centralises device movement and status tracking to ensure operational visibility and accountability.
The system enforces structured data capture, role-based access control, and workflow integrity across departments.

Core Capabilities

• Device lifecycle tracking from receiving to dispatch
• Serial scanning and bulk allocation
• Repair and Return case management
• Storage and transfer monitoring
• Role-based dashboards
• Audit-friendly historical tracking
• Structured reporting support

Architecture

Backend
ASP.NET Core (.NET 8)

Database
SQL Server

ORM
Entity Framework Core (Code-First Migrations)

Structure
Layered architecture separating controllers, services, data access, and infrastructure concerns.

Repository Structure

Controllers – API endpoints and request handling
Services – Business logic layer
Data – Database context and persistence logic
Infrastructure – Cross-cutting utilities and integrations
Middleware – Custom request pipeline components
Migrations – EF Core schema migrations
Modules – Domain workflow areas
Phase0/UI – Workflow user interface layer
Scripts – Supporting utilities and helpers

Local Setup

Requirements
.NET SDK 8.x
SQL Server
Visual Studio or VS Code

Run

Clone the repository
Configure connection string in appsettings
Run database migrations
Start the application using dotnet run

Access Control

The system uses role-based access control to restrict visibility and actions based on operational responsibility.
Typical roles include Admin, Receiver, TechOps, Technician, and Manager.

Audit and Traceability

DeviceDesk maintains structured status transitions and historical records to support operational accountability and reporting.

Author

Thamsanqa Clive Ndelu
GitHub: https://github.com/Ndelu-Blose

LinkedIn: https://www.linkedin.com/in/thamsanqa-ndelu
