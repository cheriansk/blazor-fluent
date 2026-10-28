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
| **`scripts/verify-fluent-v5.ps1`** | Roslyn AST scanner verifying 100% compliance with Microsoft Fluent UI Blazor V5. | Run locally before committing or in CI/CD pull request gates. |
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

## 🔍 5. Fluent UI V5 Compliance Scanner (`scripts/verify-fluent-v5.ps1`)

Guarantees that no deprecated Fluent UI V3/V4 components or attributes exist in the codebase:

```powershell
pwsh scripts/verify-fluent-v5.ps1
```

### Verified Rules:
- Banned legacy controls: `<FluentTextField>`, `<FluentNumberField>`, `<FluentTextArea>`.
- Deprecated attributes: `Appearance="Appearance.Accent"`, `Appearance="Appearance.Neutral"`.
- Missing required attributes: `@bind-Value` vs `Value` usage in form components.
- Returns exit code `0` on 100% compliance; returns non-zero exit code with line numbers if violations are detected.

---

## 🌐 6. Air-Gapped Asset Downloader (`scripts/Download-Quill.ps1`)

For secure enterprise environments that prohibit loading assets from external CDNs:

```powershell
pwsh scripts/Download-Quill.ps1
```

---

## 🔄 7. Template Synchronization (`scripts/Sync-UpstreamTemplate.ps1`)

Executed directly from the **Base Template project** to compare or synchronize changes into a renamed downstream project:

```powershell
# 1. Interactive mode: Prompts for downstream project folder and confirms before running
pwsh scripts/Sync-UpstreamTemplate.ps1

# 2. Specify downstream folder directly:
pwsh scripts/Sync-UpstreamTemplate.ps1 -DownstreamFolder "C:\repos\AcmePortal"

# 3. Dry run preview without making changes:
pwsh scripts/Sync-UpstreamTemplate.ps1 -DownstreamFolder "C:\repos\AcmePortal" -Mode AutoApply -DryRun

# 4. Auto-apply safe base updates while protecting *.Custom.cs and [BaseModification] files:
pwsh scripts/Sync-UpstreamTemplate.ps1 -DownstreamFolder "C:\repos\AcmePortal" -Mode AutoApply
```

### Safety Features:
- **Always Runs from Base Template**: Guarantees the source is the authoritative base template.
- **Prominent Header & Confirmation Prompt**: Clearly shows source and target paths and requires `Y/n` confirmation before any action.
- **Automated Brand Translation**: Stages the template into `.template-staging` and renames it to match the downstream project name, eliminating false-positive diffs on `namespace` or directory paths.
- **TemplateGuard Protection**: Strictly protects all `*.Custom.cs` partial classes and files decorated with `[BaseModification]`, while auto-updating unmodified base files and locking the updated `.basefiles.json`.


Downloads pinned, hash-verified versions of Quill.js CSS and JavaScript bundles into `src/BlazorEnterprise.Web/wwwroot/lib/quill/`, enabling 100% offline rich-text editing.
