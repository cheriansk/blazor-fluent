<!-- ===========================================================================
[SYSTEM / AGENT RUNTIME - STRICT MINIMAL-TOKEN READING & EXECUTION PROTOCOL]
thinking_strategy: zero_waste_discovery_focused_planning
thinking_level_hint: medium (0% wasted on file/line search; 100% focused on planning UI/logic changes)
agent_mode: targeted_surgical_edit
token_budget_policy: minimal_context_reads
navigation_index: docs/specs/index/BatchJobQueueListener.index.md
change_history: docs/specs/history/BatchJobQueueListener.history.md
source_file: src/BlazorEnterprise.Jobs/Listeners/BatchJobQueueListener.cs

MANDATORY AGENT RULES (READ BEFORE TAKING ANY ACTION):
  RULE 0 [PRE-FLIGHT FRESHNESS & LINE-NUMBER SYNC]: Before reading source slices or planning, verify the navigation index line numbers are in sync with live code by running: pwsh -NoProfile -File scripts/Generate-Specs.ps1 -TargetFilter 'BatchJobQueueListener' -EnsureFresh (exits in <50ms if unchanged, or refreshes docs/specs/index/BatchJobQueueListener.index.md while preserving Section 1 Pending Changes if external edits occurred).
  RULE 1 [READ THE NAVIGATION INDEX]: Read 'docs/specs/index/BatchJobQueueListener.index.md' to get the UI Map [Lstart-Lend], Method Map, Service Map, and Validation Rules. Completely IGNORE 'docs/specs/history/BatchJobQueueListener.history.md' and 'docs/specs/validation/' unless explicitly asked.
  RULE 2 [SURGICAL LINE-SLICE READS ONLY - NEVER READ FULL SOURCE FILES]: Do NOT call read_file on the entire .razor, .razor.cs, or service .cs files, and do NOT run broad codebase searches. Look up the exact [Lstart-Lend] range in 'docs/specs/index/BatchJobQueueListener.index.md' and call read_file ONLY with explicit StartLine and EndLine (+/- 10 lines of context) for the specific section or method being modified.
  RULE 3 [REUSE-FIRST, NO-NEW-ARCHITECTURE & CHANGE BOUNDARY]: Reuse existing components (Csk*, DataListSelect, EntityHistoryButton), dialogs/modals, DTOs, validators, and services first. Do NOT introduce new services, repositories, or abstractions unless required. Modify ONLY referenced files and methods — do NOT refactor unrelated code, rename symbols, or perform opportunistic cleanup.
  RULE 4 [CONDITIONAL PLANNING & COMPLEXITY CLASSIFICATION]:
    - Small / Medium Change (<= 3 files modified, UI/validation updates, existing method/service updates): Implement immediately without waiting for plan approval.
    - Large Change (> 3 files modified, DB schema changes, security model changes, new architecture, or new services/repositories): Output a concise 3-6 bullet 'Execution Plan' first and STOP for user approval ('yes' / Proceed).
  RULE 5 [SURGICAL EDIT, AUTO-ARCHIVE & OUTPUT CONTRACT]: After applying edit_file calls, immediately run: pwsh -NoProfile -File scripts/Generate-Specs.ps1 -TargetFilter 'BatchJobQueueListener' -ArchiveCompleted (moves completed items to docs/specs/history/BatchJobQueueListener.history.md with date/time and refreshes line numbers). NEVER run: ["dotnet build", "dotnet test"]. Return the standardized Output Contract: (1) Files Modified, (2) Change Summary, (3) Validation Performed, (4) Risks.
============================================================================ -->
# Spec: `BatchJobQueueListener`

## 1. Pending Changes (Write in Plain English)
<!-- Write plain English below. For complex items, optionally prefix with [UI], [DATA], [SECURITY], [VALIDATION], [BUGFIX], or [PERFORMANCE] and add 'Acceptance Criteria:' bullets. Then prompt: Implement @[docs/specs/BatchJobQueueListener.requirements.md] -->
<!-- BEGIN PENDING CHANGES -->
- [ ] *(Add pending change requests here. Then prompt Antigravity: `Implement the pending changes in @[docs/specs/BatchJobQueueListener.requirements.md]`)*
<!-- END PENDING CHANGES -->

## 2. Permanent & Implementation Rules
<!-- BEGIN CUSTOM BUSINESS RULES -->
- *(Document permanent domain invariants or custom business rules here. Preserved across regenerations.)*
<!-- END CUSTOM BUSINESS RULES -->
- **Reuse-First**: Reuse existing UI components, modals, DTOs, validators, and services before creating new code.
- **No New Architecture**: Prefer extending existing methods; do not introduce new services, repositories, or abstractions unless explicitly requested.
- **Change Boundary**: Modify only referenced files and methods. Do not refactor unrelated code, rename symbols, or perform opportunistic cleanup.
- **Conditional Planning**: Implement Small/Medium changes (<= 3 files, UI/validation/existing method updates) immediately; generate a plan and wait for approval only on Large changes (> 3 files, DB schema, security model, or new services/architecture).

## 3. Business Context
- **Purpose**: Background channel listener that dequeues and executes asynchronous tenant-scoped batch jobs.

