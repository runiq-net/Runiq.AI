# Development Standards

These standards apply to AI-assisted development work in this repository.

## Scope Discipline

- Stay within the feature brief, execution plan, and active execution unit.
- Implement only the active execution unit selected for the current developer pass.
- Do not implement `Blocked`, `Done`, or later execution units.
- Do not move to the next execution unit unless the user explicitly starts another developer pass.
- Do not change unrelated files or behavior.
- Do not perform opportunistic refactors unless they are required by the active unit.
- Keep each change small, independent, and reviewable.
- Agent runtime, dependency injection registration, runtime bridge, and similar integration scopes may be changed only when the active execution unit explicitly asks for them.

## Code Design

- Preserve the existing architecture, project boundaries, naming conventions, and file organization.
- Prefer existing abstractions, helpers, patterns, and error handling contracts over new custom approaches.
- Research existing domain, RAG, agent models, metadata filter types, and pipelines before adding new code, and reuse them when they fit the active unit.
- Add a new abstraction, pipeline, or top-level concept only when the active execution unit explicitly asks for it.
- Do not introduce duplicate logic. Reuse existing logic or extract a narrowly scoped shared helper when justified.
- Do not add provider, network, database, or external service dependencies unless the acceptance criteria require them.
- Do not break dependency injection override points. Services, clients, clocks, configuration, and persistence dependencies must remain replaceable in tests and host-specific composition.
- If a method accepts a `CancellationToken`, pass it through the full async call chain where supported.
- Follow the repository's established error handling contract. Preserve expected exception types, result objects, status codes, and validation behavior.

## Documentation Comments

- Add clear English XML comments to new or modified public classes, methods, properties, interfaces, and extension methods.
- Keep XML comments concise and useful. Describe behavior, parameters, return values, and important contract details.
- Do not add comments that merely repeat the member name.

## Tests

- Evaluate unit and integration test needs against the acceptance criteria and risk of the active unit.
- Add unit or integration tests when the acceptance criteria or active execution unit validation requires them.
- Add or update tests when behavior, contracts, validation, serialization, DI registration, or integration boundaries change.
- In each new or modified test method, include an English explanatory comment describing what the test verifies.
- Prefer deterministic tests. Avoid real network, provider, database, filesystem, clock, or environment dependencies unless explicitly required and safely isolated.

## Validation

- When work is complete, run the relevant build, test, or format commands for the touched area.
- Prefer targeted commands first when the change is narrow, then broader validation when contracts or shared behavior changed.
- Record validation commands and results in the execution plan.
- If validation cannot be run, record the reason and the remaining risk.

## Execution Plan Updates

- When the active execution unit is complete, update the same execution plan before reporting completion.
- Change the completed execution unit to `Status: Done`.
- Change execution units whose dependencies are complete from `Status: Blocked` to `Status: Ready`.
- Add a Change Log entry with the changed files, validation commands and results, and any skipped validation with the reason.

## Restricted Operations

- Do not run `git push`.
- Do not publish packages.
- Do not perform destructive deletes.
- Do not run hard reset commands.
