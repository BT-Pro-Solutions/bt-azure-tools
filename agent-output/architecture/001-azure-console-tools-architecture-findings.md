# 001 — Azure Console Tools Architecture Findings

Status: **open**

Date: 2026-02-05

## Context

Workspace contains a minimal .NET 10 console template intended to evolve into a multi-tool Azure management console.

## Outcome Summary

Recommended a layered architecture with:

- A Spectre.Console host + tool routing layer
- Shared selection/authentication workflows
- Tool modules that self-register into DI
- Infrastructure adapters for ARM, Graph, and SQL

## Critical Requirements Mapping

- Reusable auth + selection layer: addressed via dedicated selection services and credential provider.
- First tool (Azure SQL Entra permissions): captured as a tool module with a workflow service and infra adapters.
- Separation of concerns: CLI vs services vs domain models formalized.
- Future tools: module registration + tool registry.

## Integration Requirements

- TokenCredential strategy must support a "bootstrap" credential (to list tenants) and a tenant-scoped credential (to operate within selected tenant).
- Graph permissions: ensure consent and scopes for user/service principal lookup.
- SQL connectivity: use AAD access token for `https://database.windows.net//.default`.

## Verdict

**APPROVED_WITH_CHANGES**

### Must-haves before implementing

- Decide tool discovery strategy: reflection scan vs explicit module list (both supported; pick one for determinism).
- Decide credential UX: interactive browser vs device code vs env/managed identity; default should be explicit and predictable.

## Normal vs Debug Telemetry

- Normal: tool start/end, selected ARM resource IDs, durations, failure category.
- Debug: Graph query shapes + ARM request IDs + executed script identifiers.
