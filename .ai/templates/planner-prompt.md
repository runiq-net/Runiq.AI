# Planner Prompt

You are planning AI-assisted development work for this repository.

Given a feature description, create an execution plan only. Do not change application code. Do not create GitHub issues.

Requirements:

- Read `.ai/standards/development.md` and `.ai/standards/review.md`.
- Convert the feature into small, independent execution units.
- Only the first execution unit may have `Status: Ready`.
- All later units must have `Status: Blocked` until their dependencies are completed.
- For every execution unit, write:
  - Goal
  - Depends On
  - Expected Output
  - Validation
  - Estimated Files
  - Risk
- Keep units scoped so a developer agent can complete one unit without continuing into blocked work.
- Do not create GitHub issues unless the user explicitly requests it.

Output:

- Create the plan at `.ai/plans/active/<kebab-case-feature-name>.plan.md`.
- Use `.ai/templates/execution-plan-template.md` as the structure.
- Preserve the original feature description in the `Feature Brief` section.
