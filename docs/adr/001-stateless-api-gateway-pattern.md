# ADR 001: Stateless Cloud API Processing Gateway Architecture

## Status
**Accepted** — Architectural Invariant

## Context
`BlazorEnterprise.Api` is an independent Minimal API host deployed to provide secure, high-throughput cloud ingestion, file transformations, and S2S rules evaluation.

In traditional monolithic designs, API projects frequently inject database contexts (`AppDbContext`) and domain persistence services directly. However, in our distributed enterprise topology:
1. `BlazorEnterprise.Api` must operate as a horizontally scalable, stateless micro-gateway that can scale to dozens of cloud container instances without exhausting PostgreSQL connection pools.
2. File transformations and business rules execution must be strictly decoupled from relational persistence transactions.
3. Machine-to-machine integrations and webhook ingestions require strict token clearance without database overhead on every ping.

## Decision
We enforce strict architectural isolation on `BlazorEnterprise.Api.csproj`:
- **Allowed Project References**:
  - `BlazorEnterprise.RulesEngine` (stateless rule evaluation and business calculations)
  - `BlazorEnterprise.Shared` (DTOs, DataListTypes enums, contracts)
- **Forbidden Project References**:
  - `BlazorEnterprise.Persistence` (0 database dependencies)
  - `BlazorEnterprise.Core` (domain entity graphs remain isolated in Worker and Web)

### Architectural Flow:
```
External Client / S2S Caller
             │
             ▼
  ┌──────────────────────┐
  │   BlazorEnterprise.Api   │ ── Stateless Minimal API Gateway
  │ (RulesEngine+Shared) │ ── Non-database token clearance & payload validation
  └──────────────────────┘
             │
             │ Publishes encrypted payload / batch event
             ▼
  ┌──────────────────────┐
  │  Distributed Worker  │ ── Background host with Persistence & EF Core
  │ (Jobs + Persistence) │ ── Performs database commits & tenant isolation
  └──────────────────────┘
```

## Consequences
- **Positive**:
  - **Zero Database Contention**: API scaling does not increase database connection pressure.
  - **Fail-Closed Security Boundary**: API compromises cannot execute direct database queries or bypass EF Core interceptors.
  - **Blazing Fast Ingestion**: Request validation and rules evaluations complete in sub-millisecond memory without I/O waits.
- **Negative / Constraints**:
  - API endpoints requiring database queries must delegate asynchronously through event queues (`IJobEventQueue`) or invoke S2S internal services.
