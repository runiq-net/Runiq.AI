# Review Standard

Use this format when reviewing a completed execution unit.

## Status: Successful / Problematic

## Short Assessment

Summarize whether the completed unit satisfies the goal and acceptance criteria without changing unrelated behavior.

## Findings

### P1 Merge Blocker

Serious contract, scope, build, test, data-loss, security, or runtime breakage that blocks merge.

### P2 Must Fix

Limited but required correction. The change may mostly work, but a defect, missing required test, documentation gap, or contract mismatch must be fixed before completion.

### P3 Improvement

Non-blocking improvement, maintainability note, or cleanup suggestion.

## Acceptance Criteria Checklist

List each acceptance criterion and mark it as satisfied, not satisfied, or not applicable.

## Scope Check

Confirm whether the change stayed inside the active execution unit and avoided unrelated application behavior changes.

## Test Result

List validation commands run, their results, and any tests that were skipped or could not be run.

## Final Decision

State whether the execution unit is accepted or requires follow-up fixes.

## Review Rules

- P1 findings are merge blockers caused by serious contract, scope, or test/build breakage.
- P2 findings are required fixes with limited scope.
- P3 findings are improvement notes only.
- If any P1 or P2 issue is found, add a Fix Execution Unit to the same execution plan.
- Do not create a new independent GitHub issue from review findings unless the user explicitly requests it.
