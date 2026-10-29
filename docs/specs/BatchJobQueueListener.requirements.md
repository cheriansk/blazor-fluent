<!-- ===========================================================================
[SYSTEM / AGENT RUNTIME - STRICT MINIMAL-TOKEN READING & EXECUTION PROTOCOL]
thinking_strategy: zero_waste_discovery_focused_planning
thinking_level_hint: medium (0% wasted on file/line search; 100% focused on planning UI/logic changes)
agent_mode: targeted_surgical_edit
token_budget_policy: minimal_context_reads
source_file: src/BlazorEnterprise.Jobs/Listeners/BatchJobQueueListener.cs
generated_at_utc: 2026-10-29T15:32:57Z
source_hash_sha256: 1a90f761034e0a2c

MANDATORY AGENT RULES (READ BEFORE TAKING ANY ACTION):
  RULE 0 [PRE-FLIGHT FRESHNESS & LINE-NUMBER SYNC]: Before reading source slices or planning, verify the spec's line numbers are in sync with live code by running: pwsh -NoProfile -File scripts/Generate-Specs.ps1 -TargetFilter 'BatchJobQueueListener' -EnsureFresh (exits in <50ms if unchanged, or refreshes line numbers while preserving Section 1 Pending Changes if external edits occurred). If it regenerates, re-read the updated Agent Code Index.
  RULE 1 [WHAT TO READ IN THIS MD FILE]: Read ONLY '## 1. What I Want Changed' (and 'Permanent Rules') plus the 'Agent Code Index' (Sections A, B, C at the bottom). Use '## 2. Current Page Overview' only for quick orientation.
  RULE 2 [WHAT TO SKIP IN THIS MD FILE]: Completely IGNORE the 'Change History (Completed Requirements by Date)' block unless the user explicitly asks about historical changes.
  RULE 3 [SURGICAL LINE-SLICE READS ONLY - NEVER READ FULL SOURCE FILES]: Do NOT call read_file on the entire .razor, .razor.cs, or service .cs files, and do NOT run broad codebase searches. Look up the exact [Lstart-Lend] range in the 'Agent Code Index' below and call read_file ONLY with explicit StartLine and EndLine (+/- 10 lines of context) for the specific section or method being modified.
  RULE 4 [PHASE 1 - PLAN FIRST & STOP FOR APPROVAL]: Map each plain-English requirement in Section 1 to the exact target file, line range, @bind property, and C# method from the Agent Code Index. Output a concise 3-6 bullet 'Execution Plan' in chat and STOP. Wait for the user to reply 'yes' / click Proceed before editing any files.
  RULE 5 [PHASE 2 - SURGICAL EDIT, AUTO-ARCHIVE & FORBIDDEN COMMANDS]: Once approved, apply targeted edit_file calls, then immediately run: pwsh -NoProfile -File scripts/Generate-Specs.ps1 -TargetFilter 'BatchJobQueueListener' -ArchiveCompleted (moves completed items to Change History with date/time and refreshes line numbers). NEVER run: ["dotnet build", "dotnet test"].
============================================================================ -->
# Spec: `BatchJobQueueListener`

## 1. What I Want Changed (Write in Plain English)
<!-- Write your requirements in plain English below (no line numbers or code syntax needed), then prompt: Implement @[docs/specs/BatchJobQueueListener.requirements.md] -->
<!-- BEGIN PENDING CHANGES -->
- [ ] *(Add pending change requests here. Then prompt Antigravity: `Implement the pending changes in @[docs/specs/BatchJobQueueListener.requirements.md]`)*
<!-- END PENDING CHANGES -->

### Permanent Rules (Optional)
<!-- BEGIN CUSTOM BUSINESS RULES -->
- *(Document permanent domain invariants or custom business rules here. Preserved across regenerations.)*
<!-- END CUSTOM BUSINESS RULES -->

## 2. Current Page Overview (Plain-English Summary)
- **Purpose**: Background channel listener that dequeues and executes asynchronous tenant-scoped batch jobs.
- **Methods**: `ExecuteAsync`, `ProcessJobEventAsync`

<details>
<summary><b>Change History (Completed Requirements by Date)</b></summary>

<!-- BEGIN CHANGE HISTORY -->
*(No archived changes yet. Completed requirements are automatically moved here with a date/time stamp when `-ArchiveCompleted` is run.)*
<!-- END CHANGE HISTORY -->
</details>

<details>
<summary><b>Agent Code Index (Auto-Generated Line Numbers, Bindings & Service Map - For AI Only)</b></summary>

### A. Technical UI Layout & Binding Tree (`BatchJobQueueListener.cs`)
- **Injected**: `_queue:IJobEventQueue`, `_scopeFactory:IServiceScopeFactory`, `_lifetime:IHostApplicationLifetime`, `_jobsOptions:IOptions<JobsOptions>`, `_logger:ILogger<BatchJobQueueListener>`, `dbContext:AppDbContext`, `auditService:IAuditService`, `eventTracker:IEventTrackerService`, `tenantContext:ITenantContext`, `currentUser:ICurrentUser`

### B. Method Map & End-to-End Call Chains (`BlazorEnterprise.Jobs/Listeners/BatchJobQueueListener.cs`)
| Method | Lines | Role / Trigger | End-to-End Call Chain ([PreGates] -> Service.Method[Gates](DB/Audit)) |
| :--- | :--- | :--- | :--- |
| `ExecuteAsync` | `L51-L70` | [INIT] | `local:ProcessJobEventAsync()` -> `_queue.ReadAllAsync()` -> `_scopeFactory.CreateScope()` -> `auditService.LogSecurityEventAsync` -> `auditService.LogUserActivityAsync` -> `eventTracker.TrackConsumptionStartAsync[TenantBoundaryGate](DB[W]:EventPublishTrackers,EventConsumptionTrackers)` -> `eventTracker.TrackConsumptionCompleteAsync(DB[W]:EventConsumptionTrackers,EventPublishTrackers)` -> `tenantContext.Initialize()` -> `currentUser.RestoreUserContext()` -> `currentUser.SetSystemDaemon()` |
| `ProcessJobEventAsync` | `L72-L359` | [HELPER] | `[IsRootAdmin]` -> `_scopeFactory.CreateScope()` -> `auditService.LogSecurityEventAsync` -> `auditService.LogUserActivityAsync` -> `eventTracker.TrackConsumptionStartAsync[TenantBoundaryGate](DB[W]:EventPublishTrackers,EventConsumptionTrackers)` -> `eventTracker.TrackConsumptionCompleteAsync(DB[W]:EventConsumptionTrackers,EventPublishTrackers)` -> `tenantContext.Initialize()` -> `currentUser.RestoreUserContext()` -> `currentUser.SetSystemDaemon()` -> `DB[W]:TenantUsers,JobExecutions` -> `Audit:LogSecurityEventAsync,LogUserActivityAsync` |

### C. Downstream Service Endpoints & Validation Rules
| Service Method | Source Location | Security Gates | DB & Audit | Result.Failure Conditions |
| :--- | :--- | :--- | :--- | :--- |
| `AuditService.LogSecurityEventAsync` | `BlazorEnterprise.Persistence/Services/AuditService.cs#L153-L237` | - | In-Memory | - |
| `AuditService.LogUserActivityAsync` | `BlazorEnterprise.Persistence/Services/AuditService.cs#L241-L303` | - | In-Memory | - |
| `EventTrackerService.TrackConsumptionStartAsync` | `BlazorEnterprise.Persistence/Services/EventTrackerService.cs#L353-L463` | `TenantBoundaryGate` | `DB[W]: EventPublishTrackers,EventConsumptionTrackers` | - |
| `EventTrackerService.TrackConsumptionCompleteAsync` | `BlazorEnterprise.Persistence/Services/EventTrackerService.cs#L467-L611` | - | `DB[W]: EventConsumptionTrackers,EventPublishTrackers` | - |

</details>

