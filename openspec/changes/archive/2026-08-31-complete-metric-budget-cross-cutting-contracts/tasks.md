## 1. Static policy contracts

- [x] 1.1 Add metric-budget facts to policy-context projection and verify policy-context export covers every registered family.
- [x] 1.2 Add directional weakening rules for relaxed minimum/maximum budget bounds and verify 10→20 maximum and 10→5 minimum regressions.

## 2. Cache and packaged schema contracts

- [x] 2.1 Publish a current 0.8 analysis-cache schema admitting MetricBudgetPayload while retaining frozen 0.6.1 legacy bytes, and verify real cached budget output against the advertised schema.
- [x] 2.2 Update compatibility metadata, registry, package content, and package-validation smoke assertions for current v3 finding/current cache plus explicit legacy checks.

## 3. Approval and validation contracts

- [x] 3.1 Refresh the reviewed Core API approval fixture and raw-validator pipeline order expectation, then verify their focused tests.
- [x] 3.2 Run focused Core/package tests and repository validation, archive the OpenSpec follow-up, and verify the PR branch is ready for CI.
