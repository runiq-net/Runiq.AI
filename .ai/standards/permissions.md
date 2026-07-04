# Agent Permission Policy

Agents must work conservatively inside this repository and keep all actions tied to the active execution plan.

## Automatically Allowed

The following operations may be run without additional approval when they are scoped to this repository and the active execution plan:

- Read
- Grep
- Glob
- LS
- Edit or Write only inside the repository and only for files in scope for the execution plan
- `dotnet build`
- `dotnet test`
- `dotnet format`
- `git status`
- `git diff`
- `git log`
- `Get-ChildItem`
- `Select-String`

## Requires Approval Or Is Prohibited

The following operations must require explicit user approval or be blocked by the agent environment:

- `git push`
- `git reset --hard`
- `git clean -fdx`
- Package publish
- `dotnet nuget push`
- Reading secrets or environment files
- Destructive delete operations
- `docker system prune`
- Calls to production endpoints

## Safety Rules

- Never read secrets, local environment files, credentials, or production configuration unless the user explicitly authorizes it for a specific task.
- Never delete files destructively unless the execution plan explicitly requires it and the user approves the exact target.
- Never call production endpoints from an automated agent workflow.
- Prefer dry-run, diff, status, and targeted validation commands before broader actions.
- Record meaningful commands and validation results in the execution plan.
