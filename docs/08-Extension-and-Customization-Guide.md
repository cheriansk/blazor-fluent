# 08. Base Product Extension & Customization Governance Guide

> **Purpose**: Governance rules, coding conventions, compiler enforcement, and architectural patterns to keep the **Base Product** locked and updatable, while empowering downstream projects (created from this template) to extend features, entities, and services cleanly.  
> **Who should read this**: Any developer customizing or extending a project created from this template, as well as AI assistants modifying the codebase.

---

## 1. Architectural Principles: 100% .NET Standard & Zero Merge Conflicts

When this template project is cloned/renamed for a client solution, the **Base Product** remains an upstream source of truth. Future upstream updates (bug fixes, performance improvements, security patches, framework migrations) will be merged into downstream repositories.

To guarantee that upstream merges happen with **zero merge conflicts** while adhering **100% to official Microsoft .NET Framework Design Guidelines**, all customizations use native C# **`partial` classes**, **multi-part file nesting (`.Custom.cs`)**, and **standard PascalCase naming**:

```
+--------------------------------------------------------------------------------------------------------+
|                                  UPSTREAM BASE PRODUCT (Locked)                                        |
|                                                                                                        |
|   src/BlazorFluent.Core/Domain/Tasks/UserTaskEntity.cs                                                 |
|   ├── public partial class UserTaskEntity : TaskEntity                                                 |
|   └── (Never touched by downstream - 100% clean git merge for future updates)                          |
+--------------------------------------------------------------------------------------------------------+
                                                    |
                                                    | Extended via C# partial class
                                                    v
+--------------------------------------------------------------------------------------------------------+
|                                DOWNSTREAM CUSTOMIZATION (100% .NET Idiomatic)                          |
|                                                                                                        |
|   src/BlazorFluent.Core/Domain/Tasks/UserTaskEntity.Custom.cs  <-- [Multi-part file, nested in IDE]    |
|   ├── public partial class UserTaskEntity                                                              |
|   │   ├── public string? CostCenter { get; set; }           <-- [Standard PascalCase - CA1707 clean]   |
|   │   └── public decimal CalculateLabor() { ... }           <-- [Standard PascalCase - CA1707 clean]   |
+--------------------------------------------------------------------------------------------------------+
                                                    |
                                                    | In-Place Modification Fallback (Audited)
                                                    v
+--------------------------------------------------------------------------------------------------------+
|   [Audit Marker: When base method MUST be modified in-place]                                           |
|                                                                                                        |
|   [BaseModification(                                                                                   |
|       Author = "DevName",                                                                              |
|       Date = "2026-11-01",                                                                             |
|       Ticket = "PROJ-104",                                                                             |
|       Reason = "Custom multi-tier tax computation required by client accounting",                      |
|       OriginalBehavior = "Default flat rate tax"                                                       |
|   )]                                                                                                   |
|   public async Task<Result<TaskEntity>> UpdateTaskAsync(...) { ... }                                   |
+--------------------------------------------------------------------------------------------------------+
```

---

## 2. Manifest & Analyzer Distribution Lifecycle

### Who Generates the Base Manifest (`.basefiles.json`)?
* **The Tool**: `scripts/generate-base-manifest.ps1`.
* **When**: Executed strictly by the **Template Maintainer** when publishing or tagging a base product release (e.g., v1.0, v1.1).
* **What it does**: Scans `src/` to index all base files, known member names, and syntax hashes into `tools/TemplateGuard/.basefiles.json`.
* **Downstream Teams**: Committed directly into Git. Downstream client teams **never** regenerate this file; it serves as their immutable baseline contract.

### Project Renaming Immunity Architecture
* **Layer-Relative Normalization**: Keys in `.basefiles.json` are stored relative to the project layer (e.g. `Core/Domain/Tasks/UserTaskEntity.cs`, `Persistence/Services/UserTaskService.cs`), without any project or company name prefix.
* **Prefix-Agnostic Matching**: When `TemplateGuardAnalyzer` runs inside any project (`BlazorFluent.Core`, `Acme.Core`, or `Contoso.Enterprise.Core`), it normalizes the file path to its layer and performs an $O(1)$ lookup in the manifest.
* **100% Rename Safe**: Running the rename script to re-brand the template to a client solution will **never** break or invalidate the base manifest locking!

### How the Analyzer DLL is Included in Every Project Automatically
* **Zero `.csproj` edits required**: The root `Directory.Build.props` automatically injects the analyzer into every project:
  ```xml
  <ItemGroup Condition="Exists('$(MSBuildThisFileDirectory)tools\TemplateGuard\TemplateGuard.Analyzers.dll')">
    <Analyzer Include="$(MSBuildThisFileDirectory)tools\TemplateGuard\TemplateGuard.Analyzers.dll" />
    <AdditionalFiles Include="$(MSBuildThisFileDirectory)tools\TemplateGuard\.basefiles.json" />
  </ItemGroup>
  ```
* **Pre-compiled in Git**: `TemplateGuard.Analyzers.dll` is committed in `tools/TemplateGuard/`, meaning anyone who clones or renames the repository gets immediate compiler enforcement from day one with zero manual setup.
* **Compile-Time Only (Zero Runtime Bloat)**: `<Analyzer>` items are compiler-only plugins passed to `csc.exe`. They are **never** copied to `bin/` or `publish/` output directories and add **0 bytes** to client deployments or Docker images.

---

## 3. Diagnostic Error Reference & Allowed Approaches

### <a id="tg001"></a>TG001: Direct Member Addition to Locked Base File
* **Severity**: `Error` (halts `dotnet build`)
* **Trigger**: A new property, field, or method is declared inside an upstream base file registered in `.basefiles.json`.
* **Error Message**:
  > `TG001: Member '{MemberName}' was added directly inside locked base file '{FileName}'. Direct modifications to base files cause upstream merge conflicts.`
* **Allowed Approach & Resolution Guidance**:
  > **ALLOWED APPROACH**: Do not add members directly to base files. Instead, create a partial class file named `{ClassName}.Custom.cs` alongside this file and declare `{MemberName}` there using standard PascalCase.
* **IDE Quick-Fix**: Press `Ctrl+.` $\to$ *"Move '{MemberName}' to partial class '{ClassName}.Custom.cs'"*.

---

### <a id="tg002"></a>TG002: Unannotated Modification to Base Method
* **Severity**: `Error` (halts `dotnet build`)
* **Trigger**: An existing method in a base file has a syntax hash differing from `.basefiles.json` and lacks `[BaseModification]`.
* **Error Message**:
  > `TG002: Base method '{MethodName}' in locked file '{FileName}' was modified without the required [BaseModification] attribute.`
* **Allowed Approach & Resolution Guidance**:
  > **ALLOWED APPROACH**: Prefer extending behavior using an `override` in a derived service subclass (`Custom{ServiceName}`) or using a partial method hook. If an in-place edit is unavoidable, decorate this method with: `[BaseModification(Author = "...", Date = "YYYY-MM-DD", Ticket = "...", Reason = "...", OriginalBehavior = "...")]` and document your changes.
* **IDE Quick-Fix**: Press `Ctrl+.` $\to$ *"Add [BaseModification] attribute template"*.

---

### <a id="tg003"></a>TG003: Non-Standard Underscore Member Naming (CA1707)
* **Severity**: `Error` (halts `dotnet build`)
* **Trigger**: A member in a `.Custom.cs` file or base class contains an underscore (e.g., `CostCenter_Ext`), violating Microsoft .NET Framework Design Guidelines (`CA1707`).
* **Error Message**:
  > `TG003: Member '{MemberName}' in '{FileName}' contains an underscore, violating .NET Framework Design Guidelines (CA1707).`
* **Allowed Approach & Resolution Guidance**:
  > **ALLOWED APPROACH**: Use standard C# PascalCase without underscores (e.g., `{CleanName}` instead of `{MemberName}`). Custom members are already isolated by being placed in a `.Custom.cs` partial class.
* **IDE Quick-Fix**: Press `Ctrl+.` $\to$ *"Rename to '{CleanName}' (remove underscore)"*.

---

### <a id="tg004"></a>TG004: Modified Base Razor Component Missing Documentation Header
* **Severity**: `Error` (enforced via MSBuild target)
* **Trigger**: A base `.razor` component is modified without the mandatory documentation header.
* **Error Message**:
  > `TG004: Locked base Blazor component '{FileName}' was modified without the mandatory documentation header.`
* **Allowed Approach & Resolution Guidance**:
  > **ALLOWED APPROACH**: Add the following Razor comment block at line 1 of the file: `@* [BaseModification(Author="...", Date="YYYY-MM-DD", Ticket="...", Reason="...")] *@`.
* **IDE Quick-Fix**: Press `Ctrl+.` $\to$ *"Insert @* [BaseModification] *@ header block"*.

---

## 4. EF Core Mapping Pattern for Custom Properties

Base EF Core configuration classes are also defined as `partial` with an `OnConfigureCustom` hook:

```csharp
// Upstream Base (UserTaskEntityConfiguration.cs):
public partial class UserTaskEntityConfiguration : IEntityTypeConfiguration<UserTaskEntity>
{
    public void Configure(EntityTypeBuilder<UserTaskEntity> builder)
    {
        builder.ToTable("UserTasks", "tasks");
        builder.HasKey(x => x.Id);
        // ... base mappings ...

        OnConfigureCustom(builder); // Hook for downstream customizations
    }

    partial void OnConfigureCustom(EntityTypeBuilder<UserTaskEntity> builder);
}

// Downstream Custom (UserTaskEntityConfiguration.Custom.cs):
public partial class UserTaskEntityConfiguration
{
    partial void OnConfigureCustom(EntityTypeBuilder<UserTaskEntity> builder)
    {
        builder.Property(x => x.CostCenter).HasMaxLength(32);
    }
}
```

---

## 5. Summary Checklist for Downstream Developers

- [ ] **Need new entity properties?** Create `<EntityName>.Custom.cs` with `public partial class <EntityName>` and standard PascalCase (avoids `TG001` and `TG003`).
- [ ] **Need new service methods?** Create `<ServiceName>.Custom.cs` with `public partial class <ServiceName>`.
- [ ] **Need custom EF Core mapping?** Implement `partial void OnConfigureCustom(builder)` in `<EntityConfiguration>.Custom.cs`.
- [ ] **Need to change service logic?** Create a subclass `Custom<ServiceName> : <ServiceName>`, override virtual methods, and register in DI.
- [ ] **Unavoidable base method edit?** Apply `[BaseModification(Author="...", Date="...", Ticket="...", Reason="...", OriginalBehavior="...")]` (avoids `TG002`).
- [ ] **Editing base Razor markup?** Place `@* [BaseModification(...)] *@` at line 1 (avoids `TG004`).
