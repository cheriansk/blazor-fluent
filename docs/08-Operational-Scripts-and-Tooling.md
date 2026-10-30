# 08. Operational Scripts & Tooling

> **Target Audience**: DevOps Engineers, Release Managers, Software Developers, and System Administrators.  
> **Key Objective**: Exhaustive operational catalog detailing every automation and maintenance script in the BlazorEnterprise repository, including parameters, execution examples, and CI/CD integration.

---

## 🛠️ Script Catalog Overview

| Script | Purpose | Execution Context |
| :--- | :--- | :--- |
| **`Rename-Project.ps1`** | Completely rebrands and renames the template across all projects, namespaces, files, and configurations. | When cloning the template to start a new project. |
| **`scripts/build-templateguard.ps1`** | Compiles the TemplateGuard Roslyn Analyzer and the AST Manifest Tool. | Before running manifest generation or analyzer packaging. |
| **`scripts/generate-base-manifest.ps1`**| Scans all C# and Razor files to update the locked `.basefiles.json` manifest. | When authoring new baseline features in TemplateAuthoring mode. |
| **`scripts/Generate-DataModelDocs.ps1`**| Generates interactive HTML documentation and Markdown schema references. | Whenever database entities or configurations change. |
| **`scripts/Generate-Specs.ps1`** | Generates Antigravity/Gemini-optimized `.requirements.md` specifications, end-to-end Mermaid sequence diagrams, and an interactive HTML explorer from `docs/specs/specs-manifest.json`. | Whenever UI pages, batch jobs, or services change, or before spec-driven AI edits. |
| **`scripts/Download-Quill.ps1`** | Downloads air-gapped Quill.js rich text editor assets into `wwwroot`. | When setting up air-gapped environments without external CDNs. |
| **`scripts/Sync-UpstreamTemplate.ps1`** | Synchronizes updates from a local base template folder into renamed downstream projects via Beyond Compare or AutoApply. | When taking updates from the template. |
| **`Scripts.txt`** | Quick CLI cheat-sheet for data model document regeneration. | Reference cheat-sheet. |

---

## 📋 1. Project Rebranding (`Rename-Project.ps1`)

Automates changing `BlazorEnterprise` to your custom organization or product name (e.g. `AcmeCorp.Portal`).

### Syntax & Parameters:
```powershell
./Rename-Project.ps1 -NewName "<YourProjectName>" [-DryRun]
```

### What It Automates:
1. **Directories**: Renames `src/BlazorEnterprise.*`, `tests/BlazorEnterprise.*`.
2. **Project Files**: Renames `.csproj`, `.slnx`, and solution folders.
3. **Namespaces & Code**: Replaces `namespace BlazorEnterprise.*` across all `.cs` and `.razor` files.
4. **Configuration Keys**: Replaces connection strings, log prefixes, and assembly names in `appsettings.json` and `launchSettings.json`.
5. **Clean Verification**: Preserves `.git`, `bin`, and `obj` exclusion boundaries.

---

## 🛡️ 2. TemplateGuard Compilation (`scripts/build-templateguard.ps1`)

Compiles the Roslyn analyzer and CLI tool into `tools/TemplateGuard/`:

```powershell
pwsh scripts/build-templateguard.ps1
```

- Compiles `tools/src/TemplateGuard.Analyzers` into `netstandard2.0` analyzer DLLs.
- Compiles `tools/src/TemplateGuard.Tool` into the native manifest indexing executable.

---

## 📦 3. Base Manifest Generation (`scripts/generate-base-manifest.ps1`)

Generates `tools/TemplateGuard/.basefiles.json`, the cryptographic fingerprint of all template files:

```powershell
pwsh scripts/generate-base-manifest.ps1
```

### How It Works:
1. Traverses `src/` scanning all `.cs` and `.razor` files (excluding `.Custom.cs` downstream extensions).
2. Uses Roslyn AST parsing to extract declared types, members, and method syntax hashes.
3. Computes SHA-256 hashes of normalized Razor markup and C# syntax trees.
4. Preserves the existing file timestamp if no syntax changes occurred, preventing spurious build invalidations.

---

## 📊 4. Data Model Documentation Generator (`scripts/Generate-DataModelDocs.ps1`)

Generates comprehensive data model documentation directly from the codebase:

```powershell
# Generate both interactive HTML portal and Markdown reference
pwsh scripts/Generate-DataModelDocs.ps1 -Format All

# Generate only the interactive HTML portal
pwsh scripts/Generate-DataModelDocs.ps1 -Format Html

# Generate only the Markdown reference (docs/DATA_MODEL.md)
pwsh scripts/Generate-DataModelDocs.ps1 -Format Markdown
```

### Output:
- Interactive HTML portal featuring entity diagrams, column types, foreign keys, and indexes.
- Markdown documentation suitable for repository documentation and PR reviews.

---

## 🌐 5. Air-Gapped Asset Downloader (`scripts/Download-Quill.ps1`)

For secure enterprise environments that prohibit loading assets from external CDNs:

```powershell
pwsh scripts/Download-Quill.ps1
```

---

## 🔒 6. Zero-Trust Hardcoding Scanner (`scripts/verify-no-hardcode.ps1`)

Scans solution source files and configuration for hardcoded secrets, plain-text passwords, or hardcoded GUID literals:

```powershell
pwsh scripts/verify-no-hardcode.ps1
```

---

## 🔄 7. Template Synchronization (`scripts/Sync-UpstreamTemplate.ps1`)

Executed directly from the **Base Template project** to compare or synchronize changes into a renamed downstream project:

```powershell
# 1. Interactive mode: Prompts for downstream project folder and displays comparison paths
pwsh scripts/Sync-UpstreamTemplate.ps1

# 2. Specify downstream folder directly:
pwsh scripts/Sync-UpstreamTemplate.ps1 -DownstreamFolder "C:\repos\AcmePortal"

# 3. Optional: Automatically launch Beyond Compare GUI:
pwsh scripts/Sync-UpstreamTemplate.ps1 -DownstreamFolder "C:\repos\AcmePortal" -LaunchBeyondCompare

# 4. Dry run preview without making changes:
pwsh scripts/Sync-UpstreamTemplate.ps1 -DownstreamFolder "C:\repos\AcmePortal" -Mode AutoApply -DryRun

# 5. Auto-apply safe base updates while protecting *.Custom.cs and [BaseModification] files:
pwsh scripts/Sync-UpstreamTemplate.ps1 -DownstreamFolder "C:\repos\AcmePortal" -Mode AutoApply
```

### Safety Features:
- **Always Runs from Base Template**: Guarantees the source is the authoritative base template.
- **Prominent Header & Confirmation Prompt**: Clearly shows source and target paths and requires `Y/n` confirmation before any action.
- **Automated Brand Translation**: Stages the template into `.template-staging` (inside the template directory) and renames it to match the downstream project name, eliminating false-positive diffs on `namespace` or directory paths.
- **Clean Comparison Paths**: Emits exact Source and Target paths and the CLI command (`bcompare "<source>" "<target>"`) without forcing a GUI popup, and automatically removes any stale `.template-staging` directories from downstream repositories.
- **TemplateGuard Protection**: Strictly protects all `*.Custom.cs` partial classes and files decorated with `[BaseModification]`, while auto-updating unmodified base files and locking the updated `.basefiles.json`.

---

## 📐 8. Antigravity Specification & Flow Generator (`scripts/Generate-Specs.ps1`)

Generates **Google Antigravity / Gemini-optimized** `.requirements.md` specification files, end-to-end Mermaid sequence diagrams (from UI/caller → Service → Security Gates → `AppDbContext` DbSets & `IAuditService`), and an interactive HTML explorer (`docs/specs/index.html`) for target `.razor` and `.cs` files registered in `docs/specs/specs-manifest.json`.

### Syntax & Parameters:
```powershell
# Generate specs for all active targets in docs/specs/specs-manifest.json
pwsh scripts/Generate-Specs.ps1

# Generate spec for a single target file (matched by name or path)
pwsh scripts/Generate-Specs.ps1 -TargetFilter "SettingsTenantsTab"
```

### Key Features:
1. **Manifest-Driven Target Selection (`docs/specs/specs-manifest.json`)**: Avoids scanning and generating clutter across the entire repository; only generates specs for the `.razor` components, batch jobs, or `.cs` services explicitly listed in the manifest.
2. **`[SYSTEM / AGENT RUNTIME]` Top Header**: Places `thinking_level`, `agent_mode: targeted_surgical_edit`, `token_budget_policy: minimal_context_reads`, `do_not_run_commands`, generation timestamps (`generated_at_utc` & `generated_at_local`), and `source_hash_sha256` at line 1 of every `.requirements.md` file.
3. **Agent Quick-Jump & Line-Range Map**: Provides exact `file:///...#Lstart-Lend` coordinates for every component method and downstream `*Service.cs` method so Antigravity can make surgical line-range reads instead of full-file scans.
4. **Non-Destructive Regeneration**: Preserves user-authored `<!-- BEGIN PENDING CHANGES -->` and `<!-- BEGIN CUSTOM BUSINESS RULES -->` blocks across regenerations, and automatically rescues any inline `[CHANGE: ...]` or `[TODO: ...]` tags written outside those blocks.



## TemplateGuard: base-file governance (TG001–TG004, TG020, TG021)

In `DownstreamEnforce` mode (set by `Rename-Project.ps1`) these are build errors:

| Rule | Covers | Unlock / fix |
|---|---|---|
| TG001/TG002/TG003 | Base `.cs` members/methods | Use `*.Custom.cs` partials or `[BaseModification]` |
| TG004 | Base `.razor` markup | `@attribute [BaseModification(Author=..., Reason=..., Ticket=...)]` |
| TG021 | Base `.css` / `.js` | Tag comment with a non-empty `Reason`: CSS `/* [BaseModification Author="x" Reason="y" Ticket="z"] */`, JS `// [BaseModification Author="x" Reason="y" Ticket="z"]`; or add a new `*.Custom.css` / `*.Custom.js` |
| TG020 | Missing/corrupt analyzer DLL or `.basefiles.json` | Restore `tools/TemplateGuard/*` (git checkout or `Sync-UpstreamTemplate.ps1`) |

New files that are not in the base manifest are always allowed. Minified (`*.min.*`), `wwwroot/lib/**`, `node_modules`, `bin`/`obj` and `Migrations` are not locked.

**Local escape hatch (last resort only):** `dotnet build -p:TemplateGuardAllowMissing=true` or env `TEMPLATEGUARD_ALLOW_MISSING=true` downgrades TG020 to a warning. CI must never set it.

**Maintaining the baseline (template authors):** run `scripts/build-templateguard.ps1` (regenerates `.basefiles.json`, rebuilds the analyzer so the manifest is embedded), then commit `tools/TemplateGuard/*`. `Rename-Project.ps1` re-baselines against the renamed source before purging `tools/src`.
