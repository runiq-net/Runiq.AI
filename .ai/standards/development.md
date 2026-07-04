# Development Standard

These standards apply to AI-assisted development work in this repository.

## Operating Mode

- Read the active execution plan before making changes.
- Select only the first execution unit with `Status: Ready`.
- Implement only that execution unit in the current developer pass.
- Do not work on `Blocked`, `Pending`, `Done`, or later execution units.
- Do not move to the next execution unit unless the user explicitly starts another developer pass.
- Keep each change small, independent, and reviewable.

## Repository Context

- Before implementing an execution unit, read relevant files under `.ai/context/`.
- Always read `.ai/context/repository-map.md` before deciding project or folder ownership.
- Read `.ai/context/studio.md` when the execution unit touches dashboard, embedded dashboard, Studio, UI, pages, routes, navigation, or dashboard client behavior.
- Read `.ai/context/testing.md` before adding or changing tests.
- If project ownership is unclear, inspect the repository before making changes.
- Do not guess project ownership when a context file or existing convention can answer it.
- The active execution plan may specify `Area` and `Owned Projects`; follow them unless repository inspection proves they are wrong.
- If the plan's ownership seems wrong, stop and report the mismatch instead of silently changing unrelated areas.

## Scope Discipline

- Stay within the feature brief, execution plan, and active execution unit.
- Do not change unrelated files or unrelated application behavior.
- Do not perform opportunistic refactors unless they are required by the active execution unit.
- Do not add runtime integration, dependency injection registration, runtime bridges, registries, provider routing, tool discovery, or infrastructure changes unless the active execution unit explicitly asks for them.
- Do not introduce provider, network, database, filesystem, clock, environment, or external service dependencies unless the acceptance criteria explicitly require them.

## Existing Abstraction Reuse

- Before adding a new model, enum, service, pipeline, abstraction, or top-level concept, inspect the existing project concepts.
- Prefer existing abstractions, helpers, patterns, result objects, error codes, metadata models, request models, filters, pipelines, and validation contracts.
- Reuse existing domain, RAG, agent, retrieval, vector store, metadata filter, and pipeline types when they already carry the required meaning.
- Do not create parallel types when an existing type already represents the same concept.
- If a new tool-facing or adapter-facing type is required, keep it thin and delegate to existing domain or application concepts.
- Do not duplicate existing logic. Reuse existing logic or extract a narrowly scoped shared helper only when justified by the active execution unit.

## Code Design

- Preserve the existing architecture, project boundaries, naming conventions, and file organization.
- Add a new abstraction, pipeline, provider, router, registry, or top-level concept only when the active execution unit explicitly requires it.
- Keep provider selection and runtime routing out of the implementation unless the active execution unit explicitly requires them.
- Do not break dependency injection override points. Services, clients, clocks, configuration, and persistence dependencies must remain replaceable in tests and host-specific composition.
- If a method accepts a `CancellationToken`, pass it through the full async call chain where supported.
- Follow the repository's established error handling contract. Preserve expected exception types, result objects, status codes, and validation behavior.

## Adapter Implementation Rules

Use these rules when implementing a tool, adapter, facade, bridge, or integration boundary.

- Delegate to the existing application or domain service whenever possible.
- Do not create a parallel pipeline for behavior that already exists.
- Do not duplicate validation already owned by the delegated service.
- Validate only the adapter boundary conditions that the delegated service cannot know.
- Preserve existing result items, error codes, metadata models, request models, and cancellation behavior unless the active execution unit explicitly requires a new contract.
- Tool-facing models may carry agent-facing context fields.
- Do not turn context fields into routing, provider selection, or new runtime infrastructure unless the active execution unit explicitly requires it.
- Tool implementations should adapt to existing domain or application services.
- Tool implementations must not duplicate existing pipelines.

## Documentation Comments

- Add clear English XML comments to new or modified public classes, interfaces, methods, properties, extension methods, and public contracts.
- Keep XML comments concise and useful.
- Describe behavior, parameters, return values, and important contract details.
- Do not add comments that merely repeat the member name.
- When a field is intentionally carried as context but not used for routing or provider selection, document that behavior explicitly.

## Tests

- Evaluate unit and integration test needs against the acceptance criteria and risk of the active execution unit.
- Add or update tests when behavior, public contracts, validation, serialization, dependency injection registration, integration boundaries, or result mapping changes.
- Add unit or integration tests when the active execution unit validation requires them.
- In each new or modified test method, include an English explanatory comment describing what the test verifies.
- Prefer deterministic tests.
- Avoid real network, provider, database, filesystem, clock, or environment dependencies unless explicitly required and safely isolated.
- For adapter units, tests should usually prove delegation, mapping, failure propagation, validation ownership, and cancellation behavior.

## Validation

- When work is complete, run the relevant build, test, or format commands for the touched area.
- Prefer targeted validation first when the change is narrow.
- Run broader validation when shared contracts, public models, dependency injection, or common behavior changed.
- Record validation commands and results in the execution plan.
- If validation cannot be run, record the reason and the remaining risk.

## Execution Plan Updates

- When the active execution unit is complete, update the same execution plan before reporting completion.
- Change the completed execution unit to `Status: Done`.
- Change execution units whose dependencies are now satisfied from `Status: Blocked` to `Status: Ready`.
- Do not mark future units as `Done`.
- Add a Change Log entry with:
  - completed execution unit,
  - changed files,
  - key design decisions,
  - validation commands and results,
  - skipped validation and reason, if any.

## Final Report

- Report the completed execution unit.
- List changed files.
- Summarize key design decisions.
- Include validation commands and results.
- Mention the execution plan status update.
- Mention any skipped validation and remaining risk.
- Do not include step-by-step narration of file exploration, searches, or minor actions.
- Do not narrate every file read or every command considered.
- Report only meaningful milestones and final results.

## Restricted Operations

- Do not run `git push`.
- Do not publish packages.
- Do not perform destructive deletes.
- Do not run hard reset commands.
- Do not rewrite unrelated history.
- Do not change secrets, credentials, or environment-specific configuration unless the active execution unit explicitly requires it.
