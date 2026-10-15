# 03. Security Model & Defense-in-Depth

> **Target Audience**: Security Engineers, Solutions Architects, Compliance Officers, and Lead Developers.  
> **Key Objective**: Comprehensive security specification detailing the 5-layer defense-in-depth architecture, PostgreSQL Native Row-Level Security (RLS), application-level envelope encryption, multi-IdP authentication, and real-time circuit session revocation.

---

## 🛡️ 1. The 5-Layer Defense-in-Depth Model

BlazorFluent implements a strict **Fail-Closed Zero-Trust** security architecture. Security is never assumed to be solved at a single boundary; each layer independently verifies identity, tenant context, and authorization:

```mermaid
flowchart TD
    Req([Inbound Client Request]) --> L1
    
    subgraph L1 ["Layer 1: Edge & HTTP Security"]
        L1a["Strict Content-Security-Policy (CSP)"]
        L1b["HSTS, Anti-Clickjacking, Antiforgery"]
        L1c["HTML Input Sanitization (Ganss.Xss)"]
    end
    
    L1 --> L2
    subgraph L2 ["Layer 2: Identity & Centralized Auth Gateway"]
        L2a["Multi-IdP Federation (Entra ID, OIDC, Local Dev)"]
        L2b["HMAC-SHA256 Cryptographic Tokens"]
        L2c["Azure Key Vault Secret Storage"]
    end
    
    L2 --> L3
    subgraph L3 ["Layer 3: UI-Level Compile-Time Page Contracts"]
        L3a["AuthorizedPageComponentBase"]
        L3b["MinimumVisitRole Guard (Access Denied Card)"]
        L3c["MinimumEditRole Guard (Read-Only Mode)"]
    end
    
    L3 --> L4
    subgraph L4 ["Layer 4: Service-Level Write & Read Authorization"]
        L4a["EnsureCanVisitAsync & EnsureCanEditAsync"]
        L4b["Forensic Security Audit Alerting"]
        L4c["Query Scoping (.WhereAuthorizedProject)"]
    end
    
    L4 --> L5
    subgraph L5 ["Layer 5: PostgreSQL Native Row-Level Security (RLS)"]
        L5a["TenantDbConnectionInterceptor"]
        L5b["SET LOCAL app.current_tenant_id = '<tenant_id>'"]
        L5c["PostgreSQL Database Engine Physical Policy Enforcement"]
    end
    
    L5 --> DB[(PostgreSQL Database)]
```

---

## 🐘 2. Layer 5: PostgreSQL Native Row-Level Security (RLS)

While most web applications rely solely on C# LINQ query filters (which can be accidentally bypassed via `.IgnoreQueryFilters()` or raw SQL), BlazorFluent enforces tenant isolation at the **PostgreSQL database engine level**:

```mermaid
sequenceDiagram
    autonumber
    actor User as Client / User Request
    participant EF as EF Core (AppDbContext)
    participant Interceptor as TenantDbConnectionInterceptor
    participant PG as PostgreSQL Engine (RLS)
    
    User->>EF: LINQ Query / Mutation
    EF->>Interceptor: ConnectionOpened / ConnectionOpenedAsync
    alt IsHost == true (SuperAdmin)
        Interceptor->>PG: set_config('app.is_host', 'true', false)
        Interceptor->>PG: set_config('app.current_tenant_id', '', false)
    else Tenant User
        Interceptor->>PG: set_config('app.is_host', 'false', false)
        Interceptor->>PG: set_config('app.current_tenant_id', '<TenantId>', false)
    else Anonymous / Unauthenticated
        Interceptor->>PG: set_config('app.is_host', 'false', false)
        Interceptor->>PG: set_config('app.current_tenant_id', '', false) (Fail-Closed)
    end
    EF->>PG: Execute SQL Statement
    PG->>PG: Evaluate RLS Policy:<br/>app.is_host = 'true' OR "TenantId" = app.current_tenant_id
    PG-->>EF: Return Tenant-Filtered Rows or Reject Foreign Mutations
    EF-->>User: Result
```

### PostgreSQL Superuser Bypass Rule
In PostgreSQL, database superusers (`postgres`) and roles with the `BYPASSRLS` attribute inherently bypass RLS policies even when `FORCE ROW LEVEL SECURITY` is applied. 

To guarantee physical RLS enforcement:
- Application runtime queries execute under a dedicated non-superuser role (`blazorfluent_app`).
- The `postgres` superuser is strictly reserved for database creation, schema migrations, and administrative maintenance.

---

## 🔐 3. Application-Level Envelope Encryption (AES-256-GCM + HKDF)

For sensitive attachments, imported spreadsheets, and tenant documents, BlazorFluent utilizes **Zero-Knowledge Envelope Encryption**:

```mermaid
sequenceDiagram
    autonumber
    actor User as Client Upload
    participant Web as BlazorFluent.Web
    participant Crypto as EncryptedStreamWriter (AES-256-GCM)
    participant HKDF as HKDF-SHA256 Derivation
    participant Storage as Tenant Blob Storage (Disk / Azure Blob)

    User->>Web: Upload Document (Stream)
    Web->>HKDF: Derive Tenant Key (MasterKey, TenantId, Salt)
    HKDF-->>Web: Unique 256-bit Tenant Encryption Key
    Web->>Crypto: Stream Plaintext through AES-256-GCM
    Note over Crypto: Generates unique 96-bit Nonce (IV)<br/>Computes 128-bit Authentication Tag
    Crypto->>Storage: Write Envelope: [12B Nonce][16B Tag][Encrypted Stream]
    Note over Storage: Zero-Knowledge Storage:<br/>Storage volume has zero ability<br/>to inspect document contents
    Storage-->>Web: Storage Path Confirmed
    Web-->>User: Ingestion Completed
```

### Key Security Properties:
1. **Per-Tenant Cryptographic Partitioning**: Tenant A's derived key mathematically cannot decrypt Tenant B's files, even if storage files are exfiltrated.
2. **Authenticated Encryption (AEAD)**: AES-256-GCM detects any ciphertext tampering or bit-flipping prior to decryption.
3. **Streaming Hardware Acceleration**: Employs Intel AES-NI / ARM Crypto instructions for minimal CPU overhead.

---

## 🔑 4. Centralized Authentication Gateway & Multi-IdP Support

`BlazorFluent.Api` acts as a centralized authentication broker supporting enterprise federation:

```mermaid
sequenceDiagram
    autonumber
    actor User as User Browser
    participant Web as Blazor Server WebApp
    participant Gateway as Centralized Auth Gateway (BlazorFluent.Api)
    participant IdP as Enterprise Identity Provider (Entra ID / Okta)

    User->>Web: Access Application
    Web->>Gateway: Initiate Authentication
    Gateway->>IdP: Redirect to OpenID Connect Provider
    User->>IdP: Authenticate & Pass MFA
    IdP-->>Gateway: Return Authorization Code & Claims
    Gateway->>Gateway: Validate Claims, Provision User & Assign Tenant
    Gateway->>Gateway: Mint HMAC-SHA256 Cryptographic Token
    Gateway-->>Web: Return Secure Identity Envelope
    Web-->>User: Initialize Blazor Circuit with Authenticated Claims
```

---

## 🚦 5. Project-Level 3-Tier Role Authorization & Write Guards

BlazorFluent enforces authorization through a 3-tier model combining compile-time page contracts with service and database write guards:

```mermaid
flowchart TD
    User([User Request / Navigation]) --> BasePage[AuthorizedPageComponentBase]
    
    subgraph UI ["Tier 1: Page Level Contract"]
        BasePage --> Eval[Base evaluates CanVisit & CanEdit]
        Eval -->|CanVisit = false| DeniedCard[Fluent UI V5 Access Denied Card]
        Eval -->|CanVisit = true, CanEdit = false| ReadOnlyUI[Render Page in Read-Only Mode\nInputs Disabled, Save Buttons Hidden]
        Eval -->|CanVisit = true, CanEdit = true| FullUI[Render Page in Full Read/Write Mode]
    end

    subgraph Actions ["Tier 2: Dual-Gate Action Protection (Writes)"]
        FullUI -->|Click Save/Delete| SvcCall[Service: UpdateProjectDataAsync]
        SvcCall --> Gate1[Gate 1: EnsureCanEditAsync]
        Gate1 -->|Unauthorized| SecAudit1[audit.AuditRecords: Security Alert & Throw]
        Gate1 -->|Authorized| EfWrite[EF Core: SaveChangesAsync]
        EfWrite --> Gate2[Gate 2: ProjectSecurityInterceptor]
        Gate2 -->|Fail-Closed Check| PostgreSql[(PostgreSQL)]
    end

    subgraph Queries ["Tier 3: Query Level Filtration (Reads)"]
        ReadOnlyUI & FullUI --> FetchData[Fetch Project Entities]
        FetchData --> CheckVisit[Service: EnsureCanVisitAsync]
        CheckVisit --> FilterQuery[Query: .WhereAuthorizedProject]
        FilterQuery --> PostgreSql
    end
```

### Discrete Project Roles (`ProjectRole`):
- `ReadOnly = 1`: View-only access to project boards and deliverables.
- `QA = 2`: Read/write access for test and validation scenarios.
- `Dev = 3`: Full read/write access for feature development.
- `Admin = 4`: Full project control and team role management.

---

## ⚡ 6. Real-Time Circuit Monitoring & Remote Session Kill-Switch

Blazor Server circuits maintain persistent WebSocket connections. In enterprise threat scenarios (compromised employee credentials, urgent offboarding), traditional cookie expiration is too slow:
1. `CircuitHeartbeatHandler` periodically audits active circuits against the database user revocation status.
2. If an administrator terminates a session at `/admin/sessions`, the remote user's circuit is instantly frozen via `<SessionRevokedModal />`, severing their connection in real time.
