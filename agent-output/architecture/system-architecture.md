# BTAzureTools — System Architecture

Status: **CURRENT** (initialized)

Last updated: 2026-02-05

## Changelog

| Date | Change | Rationale | Related |
|---|---|---|---|
| 2026-02-05 | Established baseline architecture for multi-tool Azure console app | Provide clean layering + extensible tool discovery | N/A |

## Purpose

BTAzureTools is a .NET 10 console host for multiple Azure management tools. It provides:

- A reusable Azure authentication + selection layer (tenant/subscription/resource selection)
- A consistent CLI UX using Spectre.Console
- A tool plug-in model so new tools can be added with minimal host changes
- Clean separation between CLI orchestration, services, and domain models

## High-Level Architecture

### Layers

1. **Host / CLI**
   - Bootstraps DI, loads tool modules, routes commands
   - Owns Spectre.Console prompts and output formatting

2. **Application Services**
   - Orchestrate workflows (selection, confirmation, execution)
   - Call Azure/Graph/SQL adapters through interfaces

3. **Domain**
   - Tool-agnostic models (TenantRef, SubscriptionRef, PrincipalRef, PermissionProfile, etc.)
   - Policies/validation rules that do not talk to external systems

4. **Infrastructure (Azure/Graph/SQL)**
   - Azure.Identity token credentials
   - Azure.ResourceManager.* clients (SQL, subscriptions)
   - Microsoft.Graph client for principal lookup
   - Microsoft.Data.SqlClient execution for T-SQL

### Boundaries and Dependencies (rules)

- CLI depends on Application Services + Domain, but not on infrastructure implementations directly.
- Infrastructure implements interfaces defined in Core/Domain.
- Tools are composed from application service workflows; they should not perform raw console IO except through a CLI abstraction.

## Components

### Tool Host

- **Tool registry**: discovers tools and exposes metadata (name, description, command)
- **Tool runner**: executes a tool with a standard execution context, cancellation token, and structured results

### Shared Azure selection

- Tenant selection
- Subscription selection
- Resource selection (SQL server/database, later other resource types)

### SQL Entra Permission Tool (first tool)

Capabilities:

- Find Entra principal (user by email; managed identity/service principal by identifier)
- Choose permission profile
- Temporarily set SQL Server Entra admin to the current operator
- Connect to Azure SQL DB using AAD access token and apply T-SQL scripts
- Restore or optionally leave the admin configuration

## Runtime Flows

### Host startup

1. Build configuration
2. Register core services (logging, clock, correlation IDs)
3. Discover and register tool modules
4. Run Spectre.Console command app / router

### Common selection flow

1. Acquire bootstrap credential (interactive/device code) capable of listing tenants
2. List tenants -> user selects tenant
3. Create tenant-scoped credential
4. List subscriptions -> user selects subscription
5. Resource selection per tool (SQL server + DB for first tool)

### SQL Entra permissions tool flow

1. Select tenant
2. Select subscription
3. Search/select Azure SQL Server and database
4. Select principal:
   - Entra user by email
   - Managed identity/service principal by name/objectId/clientId
5. Select permission profile
6. Confirm selections
7. Temporarily change SQL Server Entra admin to current operator (Graph /me)
8. Connect to DB using AAD token and run permission scripts
9. Restore admin (default) or leave as-is (explicit opt-in)

## Data Boundaries

- **Secrets/PII**: avoid printing tokens; treat emails and object IDs as PII-adjacent (display only when necessary).
- **Correlation**: generate a run ID per invocation and include in logs.

## Observability (Normal vs Debug)

### Normal (always-on)

- Tool invocation start/success/failure with:
  - tool name/version
  - run ID
  - selected subscription ID
  - selected resource IDs (ARM IDs) — safe by default
- Azure dependency calls:
  - operation name (e.g., SetSqlAadAdmin)
  - duration
  - success/failure

### Debug (opt-in)

- Graph queries (endpoint + filter shape, not raw PII)
- T-SQL script names executed (not full text)
- ARM request IDs for support correlation

## Decisions

### D1 — Use a module-based tool registration model

**Context**: Many tools will be added over time. We want low coupling between the host and each tool.

**Choice**: Each tool assembly provides an `IToolModule` that registers its commands + services into DI. The host discovers modules by scanning loaded assemblies (or a curated list).

**Alternatives**:
- Hard-coded registration in Program.cs (simple but scales poorly)
- MEF-style composition (more complex)

**Consequences**:
- Tools become self-contained and testable
- Host remains small and stable

### D2 — Prefer Generic Host + DI

**Choice**: Use `Microsoft.Extensions.Hosting` and DI as the composition root.

**Consequences**:
- Consistent lifecycle, cancellation, logging
- Easy to add configuration providers later

## Problem Areas (design debt registry)

- Tenant selection correctness can be tricky with interactive credentials; ensure the bootstrap credential and tenant-scoped credential behavior is validated early.
- Graph lookup for managed identities maps to service principals; UX needs clear identifiers to avoid selecting the wrong principal.
