## 1. Ratchet rule and reviewed baseline

- [x] 1.1 Capture the exact current `production-types-have-one-source-declaration` audit findings
  (type, count, file list) via `--mode audit --format json` against real `main`.
- [x] 1.2 Add a new `strict_layout_conventions` rule reusing `max_declarations_per_type: 1` over the
  same `folder_segment: src` selector, with one `ignored_violations` entry per captured type using
  its exact `source_type` and exact `forbidden_reference` text.
- [x] 1.3 Leave the existing `audit_layout_conventions` rule unchanged.

## 2. Regression and validation

- [x] 2.1 Add a `SelfPolicyNegativeRegressionTests` case that mutates one frozen entry's expected
  count downward and asserts strict validation fails, proving growth beyond the reviewed count is
  blocked.
- [x] 2.2 Run `make lint-architecture`, `make policy-check`, and the new/affected test project;
  confirm the strict gate passes against the real repository with the reviewed exceptions in place.

## 3. Documentation

- [x] 3.1 Update `docs/internal/self-policy-capability-matrix.md`'s `layout-conventions` row to
  record the new strict ratchet rule alongside the existing audit-only debt inventory.

## 4. OpenSpec lifecycle

- [x] 4.1 Validate this change with `openspec validate freeze-new-partial-declaration-debt --strict`.
- [x] 4.2 Archive the change once implementation and validation are complete.
