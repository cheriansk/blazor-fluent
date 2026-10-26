# Security Policy & Operational Security Runbook

## 1. Overview & Threat Model

BlazorEnterprise implements a strict **Defense-in-Depth Zero-Trust Architecture**. Every request, Blazor circuit, and background batch process operates under explicit identity, tenant scoping, and fail-closed permission checks.

---

## 2. Documented Anonymous Endpoints & Justifications

By default, the solution enforces authentication on all routes via `DefaultPolicy` and `FallbackPolicy`. The following endpoints are explicitly decorated with `[AllowAnonymous]` with formal security justifications:

| Endpoint / Route | Protocol / Method | Justification | Mitigating Security Controls |
|---|---|---|---|
| `/` | HTTP GET | Root landing page; evaluates auth state and redirects authenticated users to `/home` or unauthenticated users to `/login`. | Strictly read-only; renders no tenant or sensitive domain data. |
| `/login` | HTTP GET / POST | Interactive authentication entry point for human users. | Pre-auth tenant scoping; credentials validated in transient private scope; rate-limited; rejects system daemon / bot accounts. |
| `/auth/error` | HTTP GET | Auth error display page (e.g. invalid session, suspended account). | Displays sanitized `ERR-XXXXXXXX` code only; zero stack traces or internal paths exposed. |
| `/health/live` | HTTP GET | Kubernetes / Azure App Service liveness probe. | Returns simple 200 OK without executing database queries or leaking internals. |
| `/health/ready` | HTTP GET | Kubernetes / Azure App Service readiness probe. | Checks PostgreSQL connection pool availability; returns status code only. |
| `/api/webhooks/teams` | HTTP POST | Microsoft Teams outbound bot activity / webhook callback. | Cryptographic HMAC-SHA256 signature verification via `IWebhookSignatureValidator` in constant-time comparison before processing payload. |

---

## 3. Cryptographic Standards

- **Row-Level Integrity Seals**: All identity and role entities (`UserEntity`, `TenantUserEntity`, `ProgramUserRoleEntity`, `ProjectUserRoleEntity`) compute cryptographic HMAC-SHA256 signatures (`RowSignature`) using `Security:IntegritySecret`. Direct database modification immediately invalidates row signatures.
- **Client-Side File Encryption**: Batch files are encrypted client-side using hardware-accelerated **AES-256-GCM** with per-tenant derived keys via **HKDF-SHA256** (`ITenantEncryptionService`).
- **HTTP Security Headers**:
  - `Content-Security-Policy`: Nonced Level 3 strict policy with complete elimination of `'unsafe-inline'` scripts.
  - `Strict-Transport-Security`: `max-age=31536000; includeSubDomains; preload`.
  - `X-Frame-Options`: `SAMEORIGIN`.
  - `X-Content-Type-Options`: `nosniff`.
  - `Cross-Origin-Opener-Policy`: `same-origin-allow-popups`.
  - `Cross-Origin-Embedder-Policy`: `credentialless`.
  - `X-Permitted-Cross-Domain-Policies`: `none`.

---

## 4. Security Review Checklist for New Features

Before merging any new feature or endpoint, verify the following checklist:
- [ ] **No `[AllowAnonymous]` by default**: New Razor pages must inherit `AuthorizedPageComponentBase` and specify `MinimumVisitRole`.
- [ ] **Dual-Gate Authorization**: Service methods must call `_projectAuth.EnsureCanVisitAsync` on reads and `_projectAuth.EnsureCanEditAsync` on writes.
- [ ] **Tenant Isolation**: Entity queries must rely on EF Core named query filters (`QueryFilters.Tenant`) or explicitly validate `_tenantContext.TenantId`.
- [ ] **Input Sanitization**: User-submitted markup must pass through `IInputSanitizer.SanitizeHtml()`.
- [ ] **HMAC Sealing**: Any newly added role or security-sensitive entity must implement `IHmacSignableEntity`.
- [ ] **No Hardcoded Credentials**: Passwords, API keys, and connection strings must come from Azure Key Vault or environment variables.

---

## 5. Security Incident Response Runbook

### 5.1 Suspected Database Row Tampering (`RowSignature` Mismatch)
1. **Detection**: `InternalAuthenticationService` or `AuditableEntityInterceptor` logs `SecurityIntegrityViolation` with critical severity.
2. **Immediate Action**: Account access is automatically denied (fail-closed).
3. **Forensic Investigation**:
   - Query `audit.AuditRecords` for unauthorized updates on the affected entity ID.
   - Cross-reference PostgreSQL transaction logs (`pg_stat_activity` / write-ahead logs) for direct `UPDATE` queries executing outside EF Core connection pools.
4. **Resolution**:
   - Deactivate compromised account via `UPDATE identity."Users" SET "IsActive" = false`.
   - Contact system owner and rotate compromised keys if secret leakage is suspected.

### 5.2 Compromised Tenant or Rogue Service Account
1. **Immediate Quarantine**:
   - Toggle the tenant's **Automated Background Processing** kill switch in `SettingsTenantsTab.razor` or execute `TenantService.SetTenantDaemonActiveAsync(tenantId, false)`.
   - Revoke all active sessions for the tenant via `IUserSessionService.RevokePreviousSessionsAsync`.
2. **Secret Rotation**:
   - Regenerate the tenant license key: `TenantService.RegenerateTenantLicenseKeyAsync(tenantSlug)`.
   - Rotate tenant encryption key salts if file payload leakage is suspected.
