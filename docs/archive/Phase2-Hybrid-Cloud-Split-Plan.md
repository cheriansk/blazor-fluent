# Implementation Plan: Phase 2 Hybrid Cloud Rule & Calculation Engine Split

> [!NOTE]
> **Status: FULLY APPLIED AND ACTIVE** (Phases 1 & 2 completed).
> The solution is now partitioned into 9 core projects + 2 Aspire orchestration projects. Proprietary IP (`RulesEngine` + `Api`) is isolated in Company Azure with AES-256-GCM AEAD envelope encryption and mandatory caller-enforced correlation IDs.

## 1. Goal Description & Scope
This document outlines the architectural implementation plan and codebase blueprint for **Phase 2** (scheduled for execution next month), decomposing **BlazorEnterprise** into a distributed hybrid enterprise topology:
1. **Client Infrastructure**: The WebApp (`BlazorEnterprise.Web`), Database (`PostgreSQL`), and Persistence (`BlazorEnterprise.Persistence`) remain entirely on client infrastructure. The WebApp owns 100% of database operations (reads, writes, transactions, and startup auto-migrations via `Database.MigrateAsync()`).
2. **Tech Provider Cloud Network**: Hosts a stateless **Rule & Calculation Engine** (`BlazorEnterprise.Api` + `BlazorEnterprise.RulesEngine`). It receives state DTOs from the WebApp, evaluates proprietary business algorithms (Workflow transitions, Project Health, Calendar heuristics, deep validations), and returns processed decisions/results back to the WebApp to commit locally.
3. **Current Momentum Protection**: Provide clear development guidelines so that feature development continuing over the coming weeks remains fully compatible and ready for immediate Phase 2 extraction without rework.

---

## 2. Core Architecture Topology

```
+---------------------------------------------------------------------------------------------------------+
| CLIENT INFRASTRUCTURE (On-Premises / Client Private VPC)                                                |
|                                                                                                         |
|   +-------------------------------------------------------------------------------------------------+   |
|   |                                       BlazorEnterprise.Web                                          |   |
|   |                                                                                                 |   |
|   |  +---------------------------+     +-------------------------+     +-------------------------+  |   |
|   |  |     UI Layer (Fluent)     |     |   Service Orchestrator  |     |  Circuit Breaker Client |  |   |
|   |  |  (Pages & Components)     |     | (UserTask, Workflow...) |     |  (Polly / HTTP Handler) |  |   |
|   |  +-------------+-------------+     +------------+------------+     +------------+------------+  |   |
|   |                |                                |                               |               |   |
|   |                +--------------------------------+-------------------------------+               |   |
|   |                                                 |                                               |   |
|   |                                                 v                                               |   |
|   |                              +---------------------------------------+                          |   |
|   |                              |        BlazorEnterprise.Persistence       |                          |   |
|   |                              |    (AppDbContext, UnitOfWork, Npgsql) |                          |   |
|   |                              |   - 100% of DB Reads/Writes/Audits    |                          |   |
|   |                              |   - Auto-MigrateAsync() on Startup    |                          |   |
|   |                              +------------------+--------------------+                          |   |
|   +-------------------------------------------------|-----------------------------------------------+   |
|                                                     |                                                   |
|                                                     v Local TCP / UNIX Socket                           |
|                                  +---------------------------------------+                              |
|                                  |       Client PostgreSQL Database      |                              |
|                                  |   (All persistent data strictly here) |                              |
|                                  +---------------------------------------+                              |
+---------------------------------------------------------------------------------------------------------+
                                         |                         ^
              1. Send State / DTO Payload|                         | 4. Return Processed Decision /
                 + User JWT & License    |                         |    Calculation DTO
                 (Outbound HTTPS/2)      |                         |
                                         v                         |
+------------------------------------------------------------------+--------------------------------------+
| TECH PROVIDER CLOUD NETWORK                                                                             |
|                                                                                                         |
|   +--------------------------------------------------------------------------------------------------+  |
|   |                                         BlazorEnterprise.Api                                         |  |
|   |                                 (ASP.NET Core Web API / Endpoints)                               |  |
|   |   - Authenticates: Bearer User JWT + X-Tenant-License-Key                                        |  |
|   |   - Logs: Cryptographic Ledger (TxId + SHA-256 Hash - Zero PII)                                  |  |
|   +--------------------------------------------------+-----------------------------------------------+  |
|                                                      |                                                  |
|                                                      v In-Memory Invocation                             |
|   +--------------------------------------------------------------------------------------------------+  |
|   |                                    BlazorEnterprise.RulesEngine                                      |  |
|   |                         (Proprietary Business Logic - Cloud Binaries Only)                       |  |
|   |                                                                                                  |  |
|   |   +--------------------------+  +--------------------------+  +--------------------------+       |  |
|   |   |  Workflow State Machine  |  |   Project Health Engine  |  |  Calendar Conflict Rules |       |  |
|   |   |   & Stepper Transition   |  |   & Risk Scoring Model   |  |   & Working Day Heuristic|       |  |
|   |   +--------------------------+  +--------------------------+  +--------------------------+       |  |
|   |   +--------------------------------------------------------------------------------------+       |  |
|   |   |                      Deep Domain Invariant & Hierarchy Validators                    |       |  |
|   |   +--------------------------------------------------------------------------------------+       |  |
|   +--------------------------------------------------------------------------------------------------+  |
+---------------------------------------------------------------------------------------------------------+
```

---

## 3. Key Architectural Decisions

| Area | Decision | Implementation Details |
| :--- | :--- | :--- |
| **Database Ownership** | **100% Client-Side** | WebApp executes all EF Core queries, mutations, and startup migrations. |
| **Cloud Role** | **Stateless Rule & Calculation Engine** | Pure in-memory computation; returns calculation results/decision DTOs. |
| **Security & Auth** | **Dual-Header Identity** | `Authorization: Bearer <UserJWT>` + `X-Tenant-License-Key`. |
| **Resilience & Outage** | **Circuit Breaker / Read-Only Grace** | Polly Circuit Breaker. Reads continue uninterrupted; mutations fail gracefully with retry toast. |
| **IP Protection** | **Strict Binary Separation** | Proprietary rule engines live in Cloud-only projects (`BlazorEnterprise.RulesEngine`). |
| **Audit & Compliance** | **Dual Cryptographic Audit** | Full business diffs in client DB; Cloud logs tamper-evident SHA-256 hashes of transaction IDs. |

---

## 4. Proposed Changes (Phase 2 Blueprint)

```
========================================================================================
Target Solution Layout in Phase 2:
========================================================================================
Shared (Contracts & Entities):
├── src/BlazorEnterprise.Core                     <- Domain entities, enums, core contracts
└── src/BlazorEnterprise.Contracts.Rules          <- [NEW] Calculation Request/Response DTOs

Client Infrastructure (Deployable Package A):
├── src/BlazorEnterprise.Web                      <- Presentation UI (Blazor Fluent v5)
├── src/BlazorEnterprise.UI.Templates             <- Decoupled UI Wizard & Templates
├── src/BlazorEnterprise.Persistence              <- AppDbContext, UnitOfWork, Local Services
└── src/BlazorEnterprise.CloudEngineClient        <- [NEW] Resilient HTTP Client + Circuit Breaker

Tech Provider Cloud (Deployable Package B):
├── src/BlazorEnterprise.Api                      <- [NEW] ASP.NET Core Web API (Endpoints & Auth)
├── src/BlazorEnterprise.RulesEngine              <- [NEW] Proprietary Algorithms & State Engines
└── (No database connection or storage)       <- Stateless execution
========================================================================================
```

### Component 1: `BlazorEnterprise.Contracts.Rules` [NEW]
Shared calculation DTOs exchanged between the Client WebApp and Provider Cloud API over HTTPS.
* `WorkflowStepTransitionReqDto` & `WorkflowStepTransitionRespDto`
* `ProjectHealthCalculationReqDto` & `ProjectHealthCalculationRespDto`
* `CalendarWorkingDayReqDto` & `CalendarConflictRespDto`

### Component 2: `BlazorEnterprise.RulesEngine` [NEW] (Cloud-Only)
Houses the intellectual property, proprietary algorithms, and domain calculation engines:
* `WorkflowStateEngine.cs`: Evaluates step transitions, validations, and auto-assignments.
* `ProjectHealthScoringEngine.cs`: Multi-factor schedule, blocker, and budget health heuristics.
* `CalendarConflictEngine.cs`: Business hour, holiday, and working day calculation heuristics.

### Component 3: `BlazorEnterprise.Api` [NEW] (Cloud-Only)
Exposes the stateless calculation endpoints with Dual Header Authentication and Cryptographic Audit logging:
* `RulesWorkflowController.cs`
* `RulesHealthController.cs`
* `RulesCalendarController.cs`
* `CryptographicAuditLogger.cs`: Logs SHA-256 of request and response paired with transaction UUID (zero PII stored).

### Component 4: `BlazorEnterprise.CloudEngineClient` [NEW] (Client-Only)
Provides a resilient, circuit-broken HTTP client for the WebApp to interact with the Cloud API:
* `ICloudRuleEngineClient.cs`
* `CloudRuleEngineClient.cs`: Configured with Polly Circuit Breaker and retry policies.

### Component 5: `BlazorEnterprise.Persistence` [MODIFY]
Update local services to orchestrate:
$$\text{Local DB Read} \longrightarrow \text{Cloud Rule Engine Calculation} \longrightarrow \text{Local DB Write}$$
* `WorkflowService.cs`: Fetches entities locally $\to$ calls `ICloudRuleEngineClient.EvaluateWorkflowTransitionAsync()` $\to$ updates entities and local `AuditLogEntity` $\to$ commits to local PostgreSQL.

---

## 5. Guidelines for Ongoing Feature Development (Keeping the App Phase-2 Ready)

While building new features over the coming weeks, adhering to these 4 architectural principles ensures Phase 2 will execute seamlessly next month:

1. **Keep Service Interfaces Pure**:
   - Continue defining service contracts in `BlazorEnterprise.Core.Contracts` (e.g. `IUserTaskService`, `IWorkflowService`).
   - Razor components must **only inject interfaces**, never `AppDbContext` directly.
2. **Isolate Business Calculations from DB Queries**:
   - Structure domain calculations, health scores, and state machine transitions as pure functions or dedicated helper methods taking input state and returning results, rather than burying calculations inside massive SQL queries.
3. **Use DTOs for Service Boundaries**:
   - Avoid passing raw EF Core entity tracking proxies directly to the UI; continue using records/DTOs (`TaskDto`, `ProjectInfoDto`).
4. **Preserve Keyset Pagination & Soft Delete**:
   - Keep queries efficient using the existing `KeysetPaginationExtensions` and `ISoftDeletable` filters.

---

## 6. Verification Plan

### Automated Tests (Phase 2)
1. **Mock Rule Engine Test**:
   - Verify `WorkflowService` and `ProjectHealthService` with a mocked `ICloudRuleEngineClient` to test successful processing and local persistence.
2. **Circuit Breaker Resilience Test**:
   - Simulate Cloud API downtime (503 Service Unavailable / timeouts); verify that the Circuit Breaker trips and returns user-friendly read-only grace notifications without corrupting local DB state.
3. **Startup Migration Verification**:
   - Verify `DbContext.Database.MigrateAsync()` runs cleanly on client WebApp startup.

### Manual Verification
1. Open the UI, navigate projects and tasks (all loaded locally from PostgreSQL).
2. Trigger a workflow state change $\to$ verify request reaches Cloud API, calculation completes, and entity is updated in local PostgreSQL.
3. Simulate cloud network disconnection $\to$ verify UI remains browsable in read-only mode, and mutations display the retry notification.
