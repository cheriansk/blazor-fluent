# Antigravity Workspace Rules - BlazorEnterprise

## Operational Guardrails & Command Execution
- **Direct Implementation on Spec Requests**: Whenever the user requests "Implement <spec-file>" (e.g., `Implement @[docs/specs/...requirements.md]`), proceed IMMEDIATELY to applying the code changes directly. Do NOT pause to generate a multi-step execution plan or ask for confirmation, regardless of file count or complexity, unless the user explicitly requested `/plan`. The spec itself is the approved plan.
- **Dotnet Build & Test Restriction**: NEVER run `dotnet build`, `dotnet test`, `dotnet run`, or `msbuild` without explicit confirmation or direct request from the user. When code verification is ready, present the changes and ask the user for permission before running any build or test command.
- **Git Restrictions**: NEVER execute git commands (`git status`, `git add`, `git commit`, `git checkout`, `git push`, etc.). Leave all changes unstaged in the working tree for the developer.
