# Architecture Decision Records (ADRs) — Baseline Lock

This document records the architectural decisions, design rationales, and governing standards established for the **BlazorEnterprise** solution.

---

## ADR-001: JWT Validation and S2S Dual-Issuer Authorization
- **Status**: Approved & Enforced
- **Context**: The API server requires validation for both end-user interactive tokens and daemon server-to-server (S2S) background tokens minted via `TenantTokenService`.
- **Decision**:
  - Enforce fail-closed issuer, audience, signature, expiration, and algorithm (`HS256`) validation in `BlazorEnterprise.Api/Program.cs`.
  - Support dual-issuer validation:
    - User tokens: validated against `Auth:Issuer` and `Auth:Audience` (lifetime: 60 minutes).
    - S2S daemon tokens: validated against `Auth:S2S:Issuer` and `Auth:S2S:Audience` (lifetime: 5 minutes) and signed using tenant-specific license keys.
  - Fail-fast validation at host startup ensures empty or missing auth parameters halt the application immediately.
  - Zero sensitive details or token contents leaked in logs or 401 Unauthorized responses.

---

## ADR-002: Universal Correlation ID and Zero Sensitive Error Leakage
- **Status**: Approved & Enforced
- **Context**: In multi-tier cloud applications, support teams require immediate traceability without exposing database schemas, SQL exceptions, or stack traces to users.
- **Decision**:
  - The ambient `ICorrelationContext.CorrelationId` is the single source of truth across Web, API, Jobs, and Database Interceptors.
  - User-facing error displays (cards, message bars, dialogs, toasts) MUST render safe, generic guidance paired with the active Correlation ID as the incident **Reference ID**:
    > *"Unable to save changes. Please try again or share Reference ID: `{CorrelationId}` with your administrator."*
  - Server-side logging retains 100% fidelity (`Logger.LogError(ex, "...", correlationId)`).
  - Primitives (`Csk*`) in `Shared.UI` must never swallow errors; exceptions bubble to parent components and error boundaries (`AppErrorBoundaryComp`).

---

## ADR-003: Native Material Design 3 (`Csk*`) UI Exclusivity
- **Status**: Approved & Enforced
- **Context**: Third-party component suites (e.g. Fluent UI V5, MudBlazor) introduce bloat, JavaScript vulnerabilities, and CSP incompatibilities.
- **Decision**:
  - All UI components are authored natively in `BlazorEnterprise.Shared.UI` using Material Design 3 design tokens (`Csk*`).
  - Strict Content Security Policy (CSP): zero inline scripts, zero inline styles, zero `eval()`.
  - Clean separation: domain-agnostic UI controls live strictly in `Shared.UI`; domain pages and business modals live in `Web`.

---

## ADR-004: Centralized Exception Handling (`IExceptionHandler`)
- **Status**: Approved & Enforced
- **Context**: API endpoints and Web fetch handlers require standardized RFC 7807 `ProblemDetails` error formats.
- **Decision**:
  - Both `BlazorEnterprise.Api` and `BlazorEnterprise.Web` implement ASP.NET Core `IExceptionHandler` (`GlobalExceptionHandler` and `WebExceptionHandler`).
  - Returns `application/problem+json` with `correlationId` and `traceId` extensions for API/JSON calls, while preserving `/Error` routing for standard browser navigations.

---

## ADR-005: Constant-Time $O(1)$ Batch Querying and Split Queries
- **Status**: Approved & Enforced
- **Context**: $N+1$ iterative database queries inside loops severely degrade database performance under multi-tenant load.
- **Decision**:
  - Strictly prohibit executing database queries inside `foreach` or `for` loops.
  - Pre-aggregate child records using batch `.Where(x => ids.Contains(x.Id)).ToDictionaryAsync()` lookups before iterating.
  - Apply `.AsSplitQuery()` when eagerly loading multiple 1:N collections to eliminate SQL Cartesian cross-joins.

---

## ADR-006: Core to Shared Dependency Boundary
- **Status**: Approved & Enforced
- **Context**: Review of Clean Architecture layer dependencies.
- **Decision**:
  - `BlazorEnterprise.Core` depends only on `BlazorEnterprise.Shared`.
  - Domain entities, value objects, and service interfaces reside in `Core`. Shared DTOs, data list enums, and cross-cutting contracts reside in `Shared`.
  - Zero persistence, infrastructure, or UI dependencies are permitted in `Core` or `Shared`.

---

## ADR-007: Elimination of `DateOnly` in Favor of UTC `DateTime`
- **Status**: Approved & Enforced
- **Context**: `DateOnly` introduces serialization mismatches across database drivers, JSON serializers, and timezone conversions (`TG011`).
- **Decision**:
  - `DateOnly` is strictly prohibited across the entire solution.
  - All business and domain dates are persisted and transferred as UTC `DateTime` (`timestamptz`). Date comparisons use `.Date` or half-open intervals (`>= start && < end`).
