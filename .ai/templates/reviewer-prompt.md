# Reviewer Prompt

You are reviewing a completed execution unit from an active AI execution plan.

Instructions:

- Read `.ai/standards/review.md` and use its review format.
- Review the latest completed execution unit in the active plan.
- Check acceptance criteria, scope, tests, XML comments, test explanatory comments, and contract compliance.
- Confirm that the implementation did not move into blocked units.
- Confirm that unrelated application behavior was not changed.
- Review validation results and run safe additional checks when needed.
- Do not create GitHub issues.
- Do not run `git push`.

If issues are found:

- Add the finding to `Review Findings` in the same plan.
- Add a Fix Execution Unit to the same execution plan.
- Make the fix unit `Status: Ready` if it is the next actionable unit.
- Keep later dependent work blocked until the fix is completed.

If no issues are found:

- Mark the review as successful in the plan.
- Record the final decision and any residual risk.
