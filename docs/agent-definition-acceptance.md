# Agent definition API acceptance

Validated on 2026-09-07 against the local working tree. This is integrated acceptance
of the existing implementation, not an additional executor feature. No production
behavior or tests were added in this acceptance pass. Existing behavioral tests are
reused; public-signature reflection tests check API compatibility, not private methods.

## Requirements and evidence

Test files are under `tests/Runiq.AI.Agents.Tests/Agents/`.

| PBI | Existing acceptance evidence |
| --- | --- |
| 01 — API contract | `AgentExecutorTests`: `Constructors_PreserveNormalization`, `UseModel_PreservesLegacyConfiguration`, `RequestedExamples_PreserveNamedConstructorAndDefaults`, `Registration_ValidatesCompletedDefinitions`, `Selection_IsExclusiveAndPreservesTools`, `InvalidModel_LeavesSelectionAvailable` |
| 02 — State integrity | `AgentExecutorTests`: `InvalidModel_CanBeCorrectedWithModelSelection`, `InvalidSecondModelSelection_PreservesFirstExecutor`, `ConcurrentSelections_AllowExactlyOneWinner`; `AgentValidationBoundaryTests.ConfigurationFailures_DoNotExposeSensitiveSettings` covers recovery to Codex |
| 03 — Compatibility and execution | `LegacyModelCompatibilityTests`, `AgentExecutorTests.ModelDefinitions_ProduceEquivalentEffectiveRequests`, `AgentValidationBoundaryTests.RuntimeOverloads_RejectBeforeAnyExternalWork`; solution suite includes model, stream, tool and RAG regressions |
| 04 — Documentation | XML inventory below; four compiled README examples; existing executor selection, legacy compatibility and validation tables |
| 05 — Repository checks | Commands and observed results below; six TRX reports independently aggregated with the repository reporting script |

The selection matrix covers all nine repeated/cross-kind combinations and asserts
the original agent instance, executor reference, identity, instructions and tools.
Concurrency is coordinated with ready/release task completion sources; fixed sleeps
are not used. Only executor selection is asserted atomic. Failed first selections can
recover to Model, Codex or Claude. Invalid second selections preserve the original
configuration and follow the documented conflict-first error priority.

The acceptance tests use local definitions, controlled dependencies and scripted
clients. They do not require real keys, provider connections or Codex/Claude CLIs.
Built-in Codex/Claude execution remains unimplemented; the existing custom-executor
registry is not disabled. No tool bridge, Studio feature or workflow change is added.

## Public XML documentation inventory

| Source | Reviewed surface | Result |
| --- | --- | --- |
| `src/Runiq.AI.Agents/Domain/Agent.cs` | Draft constructor, `Executor`, `UseModel`, `UseCodex`, `UseClaude` | English summaries; constructor/method parameters, returns where applicable, expected argument/selection exceptions, null and definition-only behavior documented |
| Same source | Legacy constructor and model/provider getters affected by executor selection | Signature/defaults preserved; English return/null/exception behavior, provider reference ownership and local-only construction documented |
| `src/Runiq.AI.Agents/Configuration/AgentExecutorConfiguration.cs` | `AgentExecutorKind` and its three values; `AgentExecutorConfiguration` and two properties; `AgentModelConfiguration` and six properties | English summaries and optional/model-only data semantics present; configuration constructors remain internal |

No new public definition member lacks documentation. This is a scoped inventory,
not a claim that all historical APIs in the repository have English comments.
The acceptance tests introduced in preceding feature work have explanatory English
comments immediately above their methods. No new test method was needed here.

## Commands and observed results

Commands run from the repository root unless indicated otherwise. Test counts are
observations from this run, not required historical totals.

| Check | Command / evidence | Result | Warning or limit |
| --- | --- | --- | --- |
| Restore | `dotnet restore Runiq.AI.slnx` | Success; up to date | None |
| API contract and integrity | `dotnet test tests/Runiq.AI.Agents.Tests -c Release --no-build --no-restore --filter "FullyQualifiedName~AgentExecutorTests\|FullyQualifiedName~LegacyModelCompatibilityTests\|FullyQualifiedName~AgentValidationBoundaryTests"` | 77 passed, 0 failed/skipped | Subset of the full suite; do not add twice |
| Full regression suite | `dotnet test Runiq.AI.slnx -c Release --no-build --no-restore --logger trx --results-directory artifacts/agent-definition-acceptance/TestResults` | 1,296 passed, 0 failed/skipped | Includes 460 Agents, 702 RAG, 59 Core, 36 Workflows, 29 PostgreSQL and 10 CLI tests |
| Build and existing samples | `dotnet build Runiq.AI.slnx -c Release --no-restore` | Success; 0 warnings/errors | `git diff --name-only -- samples` was empty |
| README examples | Extracted the four `csharp` blocks after `## Executor selection` verbatim into separate net10.0 console projects referencing Agents; `dotnet run --project artifacts/agent-definition-acceptance/example-N/Example.csproj -c Release` for N=0..3 | All four compiled and ran | Covers draft/three selections/legacy constructor, complete tool definition, safe inspection, registration |
| Public documentation | Source inventory above and README examples | Complete for this feature | Manual semantic inspection; not an external review verdict |
| Reporting self-tests | `./scripts/test-update-test-badge.ps1` | All 12 scenarios passed | Uses isolated fixtures |
| Actual TRX aggregation | `./scripts/update-test-badge.ps1 -ResultsDirectory artifacts/agent-definition-acceptance/TestResults -OutputPath artifacts/agent-definition-acceptance/tests.json -TestOutcome success -ExpectedResultCount 6 -SummaryPath artifacts/agent-definition-acceptance/test-summary.md` | Six reports complete; 1,296 passed | Repository badge was not modified |
| Local packaging | `dotnet pack Runiq.AI.slnx -c Release --no-build --no-restore -o artifacts/packages` | Success | Five non-packable sample-project warnings, also observed before this acceptance pass |
| Whitespace | `git -c core.safecrlf=false diff --check` | Success | Documentation checked after writing this report |

The five packaging warnings concern Expense, ProductSupportAssistant,
WorkflowTravelPlanner, DashboardSecurityUser and DashboardSecurityRole; their
packaging settings were not changed. No new build warning was observed.

Artifacts under `artifacts/agent-definition-acceptance/` are local verification output.
Commands above can regenerate them; they are not published or committed. Real
Codex/Claude execution, live-provider acceptance, browser verification and remote CI
were not performed and are not claimed as passed. They are outside this definition
feature. All required local acceptance checks completed; no blocking criterion remains.

Usage and boundary details remain in the [Agents README](../src/Runiq.AI.Agents/README.md#executor-selection)
and [runtime lifecycle contract](agent-execution-lifecycle.md).
