# Testing Context

Use this context before adding or changing tests.

## Ownership

- Tests live under `tests/`.
- Test projects should map to source areas where possible.
- If no direct test project exists for a source area, inspect existing test conventions before creating a new test project.
- Do not put product runtime code or sample behavior in test projects.

## Test Project Map

- `tests/Runiq.Agents.Tests` -> `src/Runiq.Agent` and agent-related behavior.
- `tests/Runiq.Cli.Tests` -> `src/Runiq.Cli`.
- `tests/Runiq.ContextSpaces.Tests` -> `src/Runiq.ContextSpaces`.
- `tests/Runiq.Core.Tests` -> `src/Runiq.Core`.
- `tests/Runiq.Rag.Tests` -> `src/Runiq.Rag`.
- `tests/Runiq.Workflows.Tests` -> `src/Runiq.Workflows`.

## Test Style

- New or modified test methods should include English explanatory comments describing what the test verifies.
- Prefer deterministic tests.
- Avoid real network, provider, database, filesystem, clock, or environment dependencies unless explicitly required and safely isolated.
- Reuse existing test fixtures, fakes, deterministic providers, and integration support where possible.
- Keep tests scoped to the active execution unit.

## Validation Strategy

- Run targeted tests first.
- Run broader tests when shared contracts, dependency injection, common behavior, or cross-project integration changed.
- Record validation commands and results in the active execution plan when executing a development unit.
- If validation cannot be run, record the reason and remaining risk.
- Do not invent validation commands that are not applicable.

## Discovered Test Commands

Use these commands from the repository root when applicable:

```text
dotnet test tests/Runiq.Agents.Tests/Runiq.Agents.Tests.csproj
dotnet test tests/Runiq.Cli.Tests/Runiq.Cli.Tests.csproj
dotnet test tests/Runiq.ContextSpaces.Tests/Runiq.ContextSpaces.Tests.csproj
dotnet test tests/Runiq.Core.Tests/Runiq.Core.Tests.csproj
dotnet test tests/Runiq.Rag.Tests/Runiq.Rag.Tests.csproj
dotnet test tests/Runiq.Workflows.Tests/Runiq.Workflows.Tests.csproj
dotnet test Runiq.Net.slnx
```

Use dashboard client commands from `src/Runiq.Dashboard.Client/package.json` when dashboard client files change:

```text
npm run build
npm run lint
```

Run those commands from `src/Runiq.Dashboard.Client`.
