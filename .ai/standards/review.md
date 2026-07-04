\# Review Standard



Use this standard when reviewing a completed execution unit.



\## Operating Mode



\- Read the active execution plan before reviewing.

\- Review only completed execution units requested by the user or the latest completed execution unit when no specific unit is named.

\- Do not change code during review.

\- Do not perform fixes during review.

\- Verify the implementation against the active execution unit, acceptance criteria, repository standards, and existing architecture.

\- Prefer concrete findings with file references, affected behavior, and required action.



\## Review Output Format



Every review must include the following sections.



\## Status: Successful / Problematic



\## Short Assessment



Summarize whether the completed unit satisfies the goal and acceptance criteria without changing unrelated behavior.



\## Findings



\### P1 Merge Blocker



Serious contract, scope, build, test, data-loss, security, runtime, or main acceptance criteria breakage that blocks merge.



\### P2 Must Fix



Limited but required correction. The change may mostly work, but a defect, missing required test, documentation gap, contract mismatch, or architectural violation must be fixed before completion.



\### P3 Improvement



Non-blocking improvement, maintainability note, clarity improvement, additional test coverage suggestion, intentional trade-off, or future follow-up note.



\## Acceptance Criteria Checklist



List each acceptance criterion and mark it as:



\- Satisfied

\- Not satisfied

\- Not applicable



\## Scope Check



Confirm whether the change stayed inside the active execution unit and avoided unrelated application behavior changes.



\## Test Result



List validation commands run, their results, and any tests that were skipped or could not be run.



\## Final Decision



State whether the execution unit is accepted or requires follow-up fixes.



If no P1 or P2 finding exists, explicitly state that no Fix Execution Unit is required.



\## Finding Severity



\### P1 — Merge Blocker



Use P1 for issues that break:



\- compilation,

\- tests,

\- public contract compatibility,

\- data safety,

\- security,

\- runtime behavior,

\- main acceptance criteria,

\- repository integrity.



P1 findings block completion.



\### P2 — Must Fix



Use P2 for issues that do not fully block the merge but violate:



\- execution unit scope,

\- standards,

\- acceptance criteria,

\- architectural constraints,

\- required tests,

\- required documentation,

\- expected validation or error handling contracts.



P2 findings require correction before the unit is accepted.



\### P3 — Improvement



Use P3 for:



\- intentional trade-offs,

\- clarity improvements,

\- additional test coverage,

\- maintainability notes,

\- future follow-up suggestions,

\- small cleanup opportunities.



P3 findings do not require a Fix Execution Unit by default.



Create a Fix Execution Unit for a P3 finding only when it creates meaningful risk for the next execution unit or repeatedly appears across reviews.



\## Review Rules



\- P1 findings are merge blockers caused by serious contract, scope, build, test, data-loss, security, runtime, or acceptance criteria breakage.

\- P2 findings are required fixes with limited scope.

\- P3 findings are improvement notes only.

\- If any P1 or P2 issue is found, add a Fix Execution Unit to the same execution plan.

\- Do not create a new independent GitHub issue from review findings unless the user explicitly requests it.

\- Do not request broad refactors when a targeted fix is enough.

\- Do not require changes that are outside the active execution unit unless the change introduced a serious regression.

\- Distinguish between intentional design trade-offs and accidental defects.

\- If a behavior is intentionally deferred to a later execution unit, do not mark it P1 or P2 unless it breaks the current unit's acceptance criteria.



\## Scope Review



Verify that:



\- only the active completed execution unit was implemented,

\- blocked or future execution units were not implemented early,

\- unrelated files were not changed,

\- unrelated application behavior was not changed,

\- no opportunistic refactor was introduced,

\- no provider, network, database, runtime bridge, registry, or infrastructure dependency was added unless explicitly required.



\## Architecture Review



Verify that:



\- existing architecture and project boundaries were preserved,

\- existing abstractions and patterns were reused where possible,

\- no duplicate pipeline, duplicate provider model, or parallel abstraction was introduced,

\- new abstractions were added only when explicitly required by the execution unit,

\- error handling follows the repository's established contract,

\- dependency injection override points remain replaceable,

\- cancellation tokens are forwarded through async boundaries where supported.



\## Review Focus For Adapter Units



Use these checks when reviewing a tool, adapter, facade, bridge, or integration boundary.



\- Verify that the implementation delegates to the intended existing service.

\- Verify that no new routing, provider registry, runtime bridge, tool discovery system, or duplicate pipeline was introduced.

\- Verify that adapter-only fields are not silently converted into new infrastructure.

\- Verify that validation ownership remains clear.

\- Verify that validation already owned by the delegated service was not unnecessarily duplicated.

\- Verify that adapter boundary conditions are handled deterministically.

\- Verify that existing result items, error codes, metadata models, and cancellation behavior are preserved unless a new contract was explicitly required.

\- Verify that tests prove delegation, mapping, failure propagation, validation ownership, and cancellation behavior.



\## Test Review



Verify that:



\- relevant targeted tests were run,

\- broader tests were run when shared contracts or common behavior changed,

\- test results are recorded in the execution plan,

\- skipped tests are explained with remaining risk,

\- new or modified tests include English explanatory comments,

\- tests are deterministic,

\- tests avoid real network, provider, database, filesystem, clock, or environment dependencies unless explicitly required and safely isolated.



\## Documentation Review



Verify that:



\- new or modified public classes, interfaces, methods, properties, extension methods, and public contracts include clear English XML comments,

\- comments describe behavior, parameters, return values, and important contract details,

\- comments do not merely repeat member names,

\- intentional no-op or context-only fields are documented when they could otherwise be mistaken for routing, provider selection, or runtime behavior.



\## Execution Plan Review



Verify that the execution plan was updated correctly:



\- completed execution unit changed to `Status: Done`,

\- newly unblocked execution units changed from `Status: Blocked` to `Status: Ready`,

\- future units were not incorrectly marked as `Done`,

\- Change Log entry includes changed files,

\- Change Log entry includes validation commands and results,

\- skipped validation and remaining risk are recorded when applicable.



\## Fix Execution Unit Rules



\- Add a Fix Execution Unit only when there is at least one P1 or P2 finding.

\- Add a Fix Execution Unit for a P3 finding only when it creates meaningful risk for the next execution unit.

\- The Fix Execution Unit must be added to the same execution plan.

\- The Fix Execution Unit must be small, targeted, and directly tied to the review findings.

\- Do not create an independent GitHub issue unless the user explicitly requests it.

