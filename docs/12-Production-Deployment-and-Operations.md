# 12. Production Deployment & Operations Guide

> **Target Audience**: DevOps Engineers, Site Reliability Engineers (SRE), Cloud Architects, and Systems Administrators.  
> **Key Objective**: End-to-end operational guide for deploying, scaling, hardening, and maintaining BlazorEnterprise in enterprise production environments.

---

## 🏗️ 1. Production Topology & Architecture

```mermaid
flowchart TD
    Internet([Global Traffic / Users]) --> WAF[Azure Front Door / Cloudflare WAF]
    WAF --> LB[Application Gateway / ARR Load Balancer<br/>(Sticky Sessions Enabled / ARR Affinity)]
    
    subgraph WebCluster["Blazor Web App Cluster (Active-Active)"]
        Web1["BlazorEnterprise.Web (Instance 1)"]
        Web2["BlazorEnterprise.Web (Instance 2)"]
    end
    
    subgraph ApiCluster["Stateless API Cluster (Auto-scaling)"]
        Api1["BlazorEnterprise.Api (Instance 1)"]
        Api2["BlazorEnterprise.Api (Instance 2)"]
    end
    
    subgraph BackgroundWorker["Background Job Worker"]
        Worker["BlazorEnterprise.Worker / JobsListener"]
    end
    
    LB --> WebCluster
    LB --> ApiCluster
    
    WebCluster --> Redis[Azure Cache for Redis<br/>(L2 Distributed Cache & Data Protection Keys)]
    WebCluster --> PG[(Azure Database for PostgreSQL Flexible Server<br/>RLS Enabled)]
    Worker --> PG
    
    KeyVault[(Azure Key Vault)] -.-> WebCluster
    KeyVault -.-> ApiCluster
    KeyVault -.-> Worker
```

---

## 🔒 2. PostgreSQL Row-Level Security (RLS) Operations

BlazorEnterprise implements PostgreSQL RLS as a secondary defense layer beneath application-level global query filters.

### 2.1 RLS Role Setup & Script Execution

Before deploying schema migrations, execute the security role bootstrapping script as `postgres` superuser:

```sql
-- Create non-superuser runtime application role
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'blazorenterprise_app') THEN
        CREATE ROLE blazorenterprise_app WITH LOGIN PASSWORD 'DEPLOYMENT_GENERATED_STRONG_PASSWORD';
    END IF;
END $$;

-- Grant standard permissions on application tables
GRANT USAGE ON SCHEMA public, staging TO blazorenterprise_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public, staging TO blazorenterprise_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public, staging GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO blazorenterprise_app;
```

### 2.2 Dynamic Session Variable Injection

Every database connection acquired from the pool executes:
```sql
SET LOCAL app.current_tenant_id = '11111111-1111-1111-1111-111111111111';
```
PostgreSQL RLS policies then enforce:
```sql
CREATE POLICY tenant_isolation_policy ON tasks
    FOR ALL
    TO blazorenterprise_app
    USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);
```

---

## 🔑 3. Azure Key Vault Secret Management & Rotation

### 3.1 Required Key Vault Secrets

| Secret Name | Description | Rotation Interval |
| :--- | :--- | :--- |
| `ConnectionStrings--DefaultConnection` | PostgreSQL connection string with TLS enforced. | 90 days |
| `Security--IntegritySecret` | HMAC-SHA256 secret key for tamper-proofing `UserEntity` rows. | 180 days |
| `AzureAd--ClientSecret` | Microsoft Entra ID OAuth application secret. | 180 days |
| `Jobs--QueueChannelCapacity` | Buffer capacity tuning parameter for in-memory channel. | Dynamic |

### 3.2 Automated Zero-Downtime Secret Rotation

When rotating database credentials or HMAC secrets:
1. Update secondary credentials in Azure Key Vault.
2. Trigger seamless configuration reload via Azure App Service Managed Identity (Key Vault provider polls for updates every 15 minutes or upon restart).
3. Verify `/healthz` on staging slot before swapping slots.

---

## 🚀 4. Blue-Green Zero-Downtime Deployment Runbook

### 4.1 Deployment Sequence

1. **Database Migration (Forward-Compatible Only)**:
   - Apply additive schema changes (add nullable columns, create new tables).
   - Ensure old application code can run concurrently against the updated schema.
2. **Deploy to Staging Slot**:
   ```bash
   az webapp deployment source config-zip \
       --resource-group rg-blazorenterprise-prod \
       --name app-blazorenterprise-web \
       --slot staging \
       --src publish.zip
   ```
3. **Verify Staging Warmup**:
   - Check `https://staging.app.example.com/health/ready` -> returns HTTP 200.
4. **Swap Slots with ARR Session Draining**:
   ```bash
   az webapp deployment slot swap \
       --resource-group rg-blazorenterprise-prod \
       --name app-blazorenterprise-web \
       --slot staging \
       --target-slot production
   ```
5. **Circuit Drain**:
   - Allow existing SignalR circuits on the retiring slot to finish their disconnected circuit grace window (3 minutes).

---

## 📊 5. Production Diagnostics & Alert Thresholds

| Metric | Warning Threshold | Critical Threshold | Action |
| :--- | :--- | :--- | :--- |
| Active Blazor Circuits | > 1,500 / node | > 2,500 / node | Scale out Web App plan horizontally. |
| DB Connection Pool Wait | > 50ms | > 200ms | Increase PostgreSQL `max_connections` or PgBouncer pool. |
| Job Failure Rate | > 2% | > 5% | Inspect `JobExecutionEntity` where `Status == 'Failed'`. |
| Unhandled Exceptions | > 5 / min | > 25 / min | Inspect Serilog Application Insights logs for `ERR-XXXX`. |

---

*Related Documentation*:
- [03. Security Model & Defense-in-Depth](file:///c:/Users/cheri/source/repos/BlazorEnterprise/docs/03-Security-Model-and-Defense-in-Depth.md)
- [05. Observability & Reliability](file:///c:/Users/cheri/source/repos/BlazorEnterprise/docs/05-Observability-and-Reliability.md)
- [13. Database Migrations & Upgrades](file:///c:/Users/cheri/source/repos/BlazorEnterprise/docs/13-Database-Migrations-and-Upgrades.md)
