# LLM Code Generation Requirements Optimization Guide
## For Antigravity / Gemini / AI-Assisted Development

---

# Goal

Create requirement documents that:

- Reduce token consumption
- Improve implementation accuracy
- Minimize hallucinated code
- Encourage reuse of existing code
- Reduce regeneration cycles
- Improve code review efficiency

---

# Recommended Document Structure

```text
PageName.requirements.md
PageName.index.md
```

### Purpose

Separate:

**Human-authored requirements**

from

**AI-generated navigation metadata**

This keeps requirements concise and easier to maintain.

---

# Requirements File Template

```md
# SettingsProjectsTab

## Pending Changes

- [UI] Add archive project button.

Acceptance Criteria:
- Visible only to Admin users.
- Confirmation required.
- Archived projects are removed from active list.
- Existing workflows remain unchanged.

## Permanent Rules

- Project Code is immutable after creation.
- Tenant boundaries cannot be bypassed.

## Implementation Rules

- Reuse existing code first.
- Prefer modifying existing methods.
- Do not introduce new architecture unless explicitly required.
```

---

# Use Change Classification Tags

Tag every requested change.

## Recommended Tags

```text
[UI]
[DATA]
[SECURITY]
[PERFORMANCE]
[API]
[VALIDATION]
[BUGFIX]
[REFACTOR]
```

## Example

```md
- [UI] Add archive button.
- [DATA] Add IsArchived field.
- [SECURITY] Only administrators can archive projects.
- [PERFORMANCE] Avoid full page reload after archive.
```

---

# Add Acceptance Criteria

Every change request should include acceptance criteria.

## Avoid

```md
- Add archive button.
```

## Prefer

```md
- [UI] Add archive button.

Acceptance Criteria:
- Visible only to Admins.
- Requires confirmation.
- Removes project from active list.
- Preserves existing filters.
```

## Benefits

- Improves implementation accuracy
- Reduces follow-up fixes
- Reduces regeneration cycles
- Clarifies expected behavior

---

# Add Reuse-First Rules

Include the following section in every requirements document.

```md
## Implementation Rules

- Reuse existing components whenever possible.
- Reuse existing dialogs whenever possible.
- Reuse existing DTOs whenever possible.
- Reuse existing validators whenever possible.
- Reuse existing services whenever possible.
- Search sibling pages for similar implementations before creating new code.
```

## Purpose

Prevents AI from creating:

```text
ProjectArchiveDialog
ProjectArchiveService
ProjectArchiveDto
ProjectArchiveValidator
```

when equivalent implementations already exist.

---

# Add No-New-Architecture Rule

Include:

```md
- Do not introduce new services, repositories, abstractions, or architectural patterns unless explicitly required.
- Prefer modification of existing implementations.
```

## Purpose

Reduces:

- Overengineering
- Unnecessary abstractions
- Repository/service proliferation
- Hallucinated architecture changes

---

# Add AI Navigation Index

Maintain a generated index file.

Example:

```md
# SettingsProjectsTab Navigation Index

## UI Map

| Component | Location |
|------------|----------|
| Project Grid | L120-L180 |
| Archive Button | L182-L190 |

## Method Map

| Method | Location |
|----------|----------|
| SaveProjectAsync | L388-L427 |
| ArchiveProjectAsync | L430-L465 |

## Service Map

| Service | Usage |
|----------|----------|
| ProjectService | CRUD |
| AuthService | Permissions |

## Validation Rules

- Project code immutable.
- Tenant isolation enforced.
```

---

# Keep AI Navigation Concise

Include:

- UI Map
- Method Map
- Service Map
- Validation Rules

Avoid verbose descriptions.

---

# Remove Low-Value Sections

Avoid including:

```text
Generated At
SHA Hash
Version History
Last Modified
Previous Changes
Long Narrative Summaries
```

Unless required for another process.

These usually consume tokens without improving implementation quality.

---

# Reduce Narrative Documentation

Avoid:

```md
## Page Overview

This page allows users to manage projects
through a centralized administration system...
```

Prefer:

```md
## Purpose

Manage project assignments.
```

Or remove entirely if Method Map and UI Map already exist.

---

# Add AI Execution Constraints

Include:

```md
## AI Instructions

Read only:

1. Requirements section
2. Referenced methods from Method Map
3. Referenced services from Service Map

Do not scan unrelated files unless necessary for implementation.
```

## Benefits

Prevents AI from:

- Reading large portions of the repository
- Consuming unnecessary context
- Making unrelated changes

---

# Add Output Contract

Require every implementation response to follow:

```md
## Files Modified

1. SettingsProjectsTab.razor
2. SettingsProjectsTab.razor.cs

## Change Summary

- Added archive button.
- Added confirmation dialog.

## Validation Performed

- Existing save workflow unchanged.
- Authorization behavior preserved.

## Risks

- None identified.
```

## Benefits

- Easier reviews
- Easier pull request creation
- Easier verification

---

# Recommended Token-Efficient Workflow

## Step 1

Provide:

```text
requirements.md
```

## Step 2

Attach:

```text
index.md
```

only when implementation begins.

## Step 3

AI reads:

- Requirements
- Relevant Method Map entries
- Relevant Service Map entries

## Step 4

AI modifies existing code.

## Step 5

AI returns standardized output contract.

---

# High ROI Improvements (Priority Order)

## 1. Acceptance Criteria

Highest impact on implementation accuracy.

---

## 2. Reuse Existing Code First

Highest impact on preventing duplicate implementations.

---

## 3. No New Architecture Rule

Highest impact on preventing overengineering.

---

## 4. Output Contract

Highest impact on reviewability.

---

## 5. Change Classification Tags

Highest impact on requirements clarity.

---

# Expected Results

## Accuracy

Approximate improvement:

```text
+10% to +20%
```

due to clearer requirements and constraints.

---

## Token Usage

Approximate reduction:

```text
30% to 60%
```

depending on repository size and how much unnecessary context was previously loaded.

---

## Regeneration Cycles

Expected reduction:

```text
30% to 50%
```

because acceptance criteria and implementation rules reduce misunderstandings.

---

# Final Recommended Structure

```md
# Page Name

## Pending Changes

- [UI] Change request

Acceptance Criteria:
- Requirement 1
- Requirement 2

## Permanent Rules

- Domain rule 1
- Domain rule 2

## Implementation Rules

- Reuse existing code first.
- Prefer extending existing methods.
- Do not create new architecture unless required.

## AI Instructions

- Read only referenced methods.
- Use existing services.
- Avoid unrelated file scanning.

---

## Navigation Index (Generated)

### UI Map

### Method Map

### Service Map

### Validation Rules
```

This structure provides the best balance between:
- Accuracy
- Maintainability
- Token efficiency
- Reviewability
- Large-repository scalability

---

# Remaining Delta Improvements
## Final Optimizations Before Declaring the Framework Complete

The current Requirements / Index / History architecture is already production-grade. The items below represent the remaining high-value improvements.

---

# 1. Move Component Registration Warnings Out of Requirements

## Current

```md
## ALERT
UNREGISTERED CHILD COMPONENTS DETECTED
```

exists inside:

```text
<Page>.requirements.md
```

## Recommended

Move into:

```text
docs/specs/validation/ComponentRegistrationWarnings.md
```

or

```text
docs/specs/validation/<Page>.validation.md
```

## Why

- Tooling metadata
- Not part of implementation requirements
- Rarely needed during code generation
- Consumes tokens on every run

## Expected Benefit

- Small token reduction
- Cleaner requirements files

---

# 2. Reduce Current Page Overview

## Current

```md
## Current Page Overview

- Every card
- Every field
- Every button
- Every table
- Every modal
```

This information is already represented in:

```text
<Page>.index.md
```

## Recommended

Replace with:

```md
## Business Context

Purpose:
Manage tenants, users, programs, tasks, and risks.
```

Move the detailed UI inventory entirely into:

```text
docs/specs/index/<Page>.index.md
```

## Why

Current structure creates duplication:

```text
Requirements
+
Overview
+
Index
```

## Expected Benefit

- Largest remaining token reduction
- Cleaner requirements documents
- Single source of truth for UI metadata

---

# 3. Add Change Boundary Rule

## Add

```md
### Change Boundary

- Modify only referenced files.
- Modify only referenced methods.
- Do not refactor unrelated code.
- Do not rename symbols unless required.
- Do not perform opportunistic cleanup.
```

## Why

Prevents situations where:

```text
Requested:
Add one button.
```

becomes:

```text
14 files modified.
3 services refactored.
Naming conventions updated.
Several unrelated cleanups performed.
```

## Expected Benefit

- Higher implementation reliability
- Smaller PRs
- Fewer unintended code changes

---

# 4. Make Planning Conditional

## Current

```text
Plan
↓
Wait for approval
↓
Implement
```

for every request.

## Recommended

```md
Generate a plan first only when:

- More than 3 files will be modified.
- Database schema changes are required.
- Security model changes are required.
- New architecture is required.
- New services or repositories are required.

Otherwise implement immediately.
```

## Why

Most requests are small:

```text
UI Changes
Validation Changes
Form Updates
Minor Business Rules
```

These do not benefit from a separate planning phase.

## Expected Benefit

- Significant token savings
- Faster implementation workflow
- Reduced back-and-forth conversations

---

# 5. Add Complexity Classification (Optional)

## Add

```md
### Small Change

Examples:
- UI updates
- Validation updates
- Existing method modifications

Action:
Implement immediately.

---

### Medium Change

Examples:
- Multiple related methods
- Cross-file updates
- Service modifications

Action:
Implement immediately unless risk is high.

---

### Large Change

Examples:
- Database schema changes
- Security changes
- Architecture changes
- Cross-module changes

Action:
Plan first and request approval.
```

## Why

Creates predictable AI behavior.

## Expected Benefit

- Consistent execution strategy
- Reduced unnecessary planning

---

# Not Recommended Yet

Do NOT do these yet.

## Splitting Index Files Further

Avoid:

```text
<Page>.ui.md
<Page>.methods.md
<Page>.services.md
<Page>.validation.md
```

unless index files become extremely large.

Current:

```text
<Page>.index.md
```

is simpler and easier to maintain.

---

# Final Delta Summary

Only four changes are strongly recommended:

## High Priority

```text
1. Move Component Registration Warnings
   → validation folder

2. Replace Current Page Overview
   → Business Context summary

3. Add Change Boundary Rule

4. Make Plan/Approval Conditional
```

## Optional

```text
5. Add Complexity Classification
```

---

# Final Assessment

Current State:

```text
9.5 / 10
```

After completing the remaining delta:

```text
10 / 10
```

for an enterprise-scale Antigravity/Gemini workflow focused on:

- Large .NET solutions
- Blazor applications
- Minimal token consumption
- Surgical code modifications
- Reuse-first development
- High implementation accuracy