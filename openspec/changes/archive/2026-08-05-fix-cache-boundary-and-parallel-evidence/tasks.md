## 1. Preparation and cache boundary

- [x] 1.1 Extract metadata-only project/artifact/reference preparation with immutable identity evidence.
- [x] 1.2 Make snapshot runner setup lazy and authorize per-mode cache lookup before materialization.
- [x] 1.3 Reverify planned bytes at materialization and preserve per-mode publication authorization.
- [x] 1.4 Add boundary, mixed hit/miss, disposal, and stale-artifact regression tests.

## 2. Safe eligibility and observability

- [x] 2.1 Fail closed for ancestor build-file nested-import uncertainty and cover it with tests.
- [x] 2.2 Add avoided-work cache counters through Core, profile JSON, schema, dictionary, and tests.
- [x] 2.3 Create a contract-bearing four-partition fixture and assert real bounded fact work.

## 3. Evidence and release checks

- [x] 3.1 Retain paired strict/audit raw profiles and calculate wall-clock/allocation/resource distributions.
- [x] 3.2 Add exact source/package/configuration identity and #374 baseline/post/delta comparison.
- [x] 3.3 Regenerate raw JSON and Markdown only after Core gates pass.
- [x] 3.4 Run targeted tests, full correctness gates, OpenSpec validation, push the branch, and perform a fresh PR review.
