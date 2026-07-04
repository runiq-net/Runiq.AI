# Developer Prompt

Use this template to run one execution unit from an active AI execution plan.

Minimum prompt:

```text
Execute the first Ready execution unit in `<plan-file-path>`.
```

Execution instructions:

- Read `.ai/standards/development.md`.
- Read the specified execution plan file.
- Find the first execution unit with `Status: Ready`.
- Implement only that execution unit.
- Apply all rules from `.ai/standards/development.md`.
- Run the relevant validation commands for the active unit.
- When finished, update the same execution plan according to `.ai/standards/development.md`.
- Report a concise result summary with changed files, validation results, and any skipped validation.
