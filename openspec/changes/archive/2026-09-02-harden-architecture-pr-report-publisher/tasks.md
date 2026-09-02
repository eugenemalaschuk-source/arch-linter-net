## 1. Producer correctness and gate separation

- [x] 1.1 Make base and current snapshot baseline arguments conditional in their own worktrees.
- [x] 1.2 Keep the report producer transport-ready after a strict architecture finding and add a
  separate failing Architecture PR Report Gate.

## 2. Publisher integrity boundary

- [x] 2.1 Validate exactly one successful named producer job and remove aggregate CI-conclusion
  coupling.

## 3. Executable regression evidence

- [x] 3.1 Replace source-only publisher checks with fixture-driven execution of resolve, artifact
  validation, and comment-update JavaScript branches.
- [x] 3.2 Run focused tests, workflow lint, OpenSpec validation, risk-appropriate repository
  validation, then archive the follow-up change.
