# Repository Map

This map helps AI agents decide project and folder ownership before changing files. Use it together with the active execution plan and repository inspection.

## Main Folders

### `src/`

Purpose:
- Contains reusable product source areas and runtime/library projects.

Change here when:
- The active execution unit changes reusable product behavior, public contracts, runtime code, or client UI owned by a source area.

Do not add:
- Sample-only logic.
- Test-only helpers.
- AI workflow documentation.

Notes:
- Reusable code belongs under the source area that owns the behavior.

### `samples/`

Purpose:
- Contains usage examples, integration samples, and host/demo applications.

Change here when:
- The active execution unit explicitly changes a sample or demonstrates a feature in a sample host.

Do not add:
- Reusable library logic.
- Shared product behavior that should live under `src/`.

Notes:
- Sample projects must not become the owner of reusable library logic.
- Sample-only code belongs in the relevant sample project.

### `tests/`

Purpose:
- Contains automated tests for source areas and selected sample integration behavior.

Change here when:
- The active execution unit changes behavior, contracts, validation, serialization, dependency injection, integration boundaries, or requires test coverage.

Do not add:
- Product runtime code.
- Sample host behavior.

Notes:
- Tests should map to source areas where possible. See `.ai/context/testing.md`.

### `.ai/`

Purpose:
- Contains AI workflow standards, templates, context files, and active execution plans.

Change here when:
- The active task changes AI planning, developer, reviewer, or repository context documentation.

Do not add:
- Product runtime code.
- Tests for product behavior.
- Sample application code.

Notes:
- AI workflow files belong under `.ai/`.

## Source Areas

### `src/Runiq.Agent`

Purpose:
- Inferred from project name and folder structure: owns agent runtime, tool execution, provider integration, streaming primitives, and agent domain/configuration behavior.

Change here when:
- The active execution unit changes reusable agent domain models, agent runtime behavior, agent query handling, tool execution primitives, or agent-facing contracts.

Do not add:
- RAG pipeline implementation that belongs in `src/Runiq.Rag`.
- Dashboard UI code.
- Sample host logic.

Notes:
- The project file package id is `Runiq.Agents`, while the folder is `Runiq.Agent`.

### `src/Runiq.Cli`

Purpose:
- Inferred from project name and folder structure: owns command-line entry points and CLI behavior for Runiq.

Change here when:
- The active execution unit changes command parsing, CLI commands, CLI output, or CLI host behavior.

Do not add:
- Reusable domain logic that belongs in another source library.
- Dashboard UI code.

Notes:
- Inspect existing commands and CLI tests before changing command behavior.

### `src/Runiq.ContextSpaces`

Purpose:
- Owns context and source reading primitives for Runiq agents.

Change here when:
- The active execution unit changes context spaces, source documents, source readers, skills/documents context, or related reusable primitives.

Do not add:
- Agent runtime orchestration.
- RAG ingestion or retrieval pipelines.
- Dashboard UI code.

Notes:
- Prefer existing context/source abstractions before adding new ones.

### `src/Runiq.Core`

Purpose:
- Owns the embedded ASP.NET Core runtime and dashboard hosting layer for Runiq.Net agents.

Change here when:
- The active execution unit changes server/runtime hosting, embedded dashboard serving, ASP.NET Core integration, or core host APIs.

Do not add:
- Reusable dashboard React components or routes.
- RAG-specific pipelines.
- Sample-only host setup.

Notes:
- Dashboard client assets may be hosted or embedded from here, but reusable UI belongs in `src/Runiq.Dashboard.Client`.

### `src/Runiq.Dashboard.Client`

Purpose:
- Dashboard/studio client area. It is a Vite/React/TypeScript client folder with pages, layouts, routes, components, API client modules, theme code, and dashboard configuration.

Change here when:
- The active execution unit changes dashboard UI, Studio UI, embedded dashboard pages, routes, navigation, client-side rendering, reusable UI components, or client-side API wrappers.

Do not add:
- C# library code.
- Server/runtime behavior.
- Dependency injection registration.
- Backend APIs or provider logic.

Notes:
- This folder exists under `src/`, but it may not appear as a normal C# project in Visual Studio solution trees.
- Treat it as the Studio / embedded dashboard client area unless repository inspection proves otherwise.
- See `.ai/context/studio.md` before changing dashboard or Studio UI behavior.

### `src/Runiq.Mcp`

Purpose:
- Owns Model Context Protocol integration for exposing ASP.NET Core application services as MCP tools.

Change here when:
- The active execution unit changes MCP tool exposure, MCP endpoint behavior, or MCP integration contracts.

Do not add:
- Generic agent runtime behavior.
- Dashboard UI code.
- RAG provider implementation.

Notes:
- Inspect existing MCP service patterns before adding tool exposure behavior.

### `src/Runiq.Rag`

Purpose:
- Owns retrieval-augmented generation primitives for Runiq.Net agents.

Change here when:
- The active execution unit changes RAG ingestion, embeddings, retrieval, vector store abstractions, metadata filters, RAG models, RAG DI, or provider-independent RAG pipelines.

Do not add:
- Agent runtime orchestration that belongs in `src/Runiq.Agent`.
- Dashboard UI code.
- Real provider, network, or database dependencies unless explicitly required.

Notes:
- Prefer existing RAG request/result models, metadata filters, vector store abstractions, and pipelines before adding new concepts.

### `src/Runiq.Workflows`

Purpose:
- Owns workflow orchestration primitives for Runiq.Net runtime and dashboard scenarios.

Change here when:
- The active execution unit changes workflow models, workflow execution/orchestration, workflow graph behavior, or reusable workflow contracts.

Do not add:
- Dashboard React workflow components.
- Agent runtime behavior unrelated to workflows.
- Sample-only workflow demos.

Notes:
- Dashboard visualization of workflows belongs in `src/Runiq.Dashboard.Client`; reusable workflow logic belongs here.

## Test Areas

### `tests/Runiq.Agents.Tests`

Purpose:
- Tests `src/Runiq.Agent` and agent-related behavior.

Change here when:
- Agent contracts, runtime behavior, tool execution, provider integration, or streaming primitives change.

Do not add:
- RAG-only tests unless they verify agent integration.

Notes:
- Project reference points to `src/Runiq.Agent/Runiq.Agents.csproj`.

### `tests/Runiq.Cli.Tests`

Purpose:
- Tests `src/Runiq.Cli`.

Change here when:
- CLI command behavior, CLI output, or CLI host behavior changes.

Do not add:
- Tests for reusable library logic that belongs to another source area's test project.

Notes:
- Project reference points to `src/Runiq.Cli/Runiq.Cli.csproj`.

### `tests/Runiq.ContextSpaces.Tests`

Purpose:
- Tests `src/Runiq.ContextSpaces`.

Change here when:
- Context spaces, source readers, context documents, or context-space DI behavior changes.

Do not add:
- Agent runtime tests unless they specifically verify context-space integration.

Notes:
- Project reference points to `src/Runiq.ContextSpaces/Runiq.ContextSpaces.csproj`.

### `tests/Runiq.Core.Tests`

Purpose:
- Tests `src/Runiq.Core` and core hosting behavior.

Change here when:
- Embedded ASP.NET Core runtime, dashboard hosting, or core host APIs change.

Do not add:
- Dashboard React component tests unless a dashboard client test setup exists.

Notes:
- This project also references `src/Runiq.Workflows` for core integration scenarios.

### `tests/Runiq.Rag.Tests`

Purpose:
- Tests `src/Runiq.Rag` and selected RAG sample integration behavior.

Change here when:
- RAG models, embeddings, retrieval, ingestion, vector stores, metadata filters, DI, or RAG pipelines change.

Do not add:
- Agent runtime tests unless they verify a RAG integration boundary owned by RAG.

Notes:
- This project references `src/Runiq.Rag` and RAG sample projects.

### `tests/Runiq.Workflows.Tests`

Purpose:
- Tests `src/Runiq.Workflows`.

Change here when:
- Workflow models, orchestration, graph behavior, or workflow contracts change.

Do not add:
- Dashboard UI tests for workflow visualization.

Notes:
- Project reference points to `src/Runiq.Workflows/Runiq.Workflows.csproj`.

## Sample Projects

Purpose:
- Sample projects are usage examples, integration samples, or host/demo applications.

Change here when:
- The active execution unit explicitly changes a sample scenario, usage example, or host/demo configuration.

Do not add:
- Reusable library logic.
- Product contracts that other projects should consume.

Notes:
- Known sample folders include `Runiq.ContextTravelGuide`, `Runiq.DashboardSecurityRole`, `Runiq.DashboardSecurityUser`, `Runiq.ExpenseDesk`, `Runiq.Rag.IngestionSample`, `Runiq.Rag.UpsertPipelineSample`, `Runiq.Samples.DashboardSecurityUser`, and `Runiq.WorkflowTravelPlanner`.
- `Runiq.Samples.DashboardSecurityUser` was discovered in the repository in addition to the known sample list.

## Ownership Decisions

- Read the active execution plan first.
- Read this repository map before deciding project or folder ownership.
- Inspect existing folders, project references, and nearby tests before editing.
- Put reusable code in the source area that owns the behavior.
- Put sample-only code in the relevant sample project.
- Put tests in the test project that maps to the changed source area where possible.
- Put AI workflow standards, templates, plans, and context files under `.ai/`.
- If ownership is unclear, mark it as an inferred assumption and inspect the repository before changing behavior.
- Do not guess ownership when this context file or existing conventions can answer it.
