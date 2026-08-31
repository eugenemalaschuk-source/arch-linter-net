## 1. Policy contract surface

- [x] 1.1 Add strict/audit metric-budget policy models, schema definitions, raw/effective validation, and contract-family catalogue/selection wiring; verify valid, invalid, imported, and unchanged-policy cases with focused Core tests.
- [x] 1.2 Update reviewed Core public-API artifacts for the intentional model additions; verify `make public-api-check` reports no unreviewed drift after the explicit update lifecycle.

## 2. Budget evaluation and normalized evidence

- [x] 2.1 Implement metric-budget execution that evaluates each referenced metric through the shared evaluator, applies inclusive absolute bounds, and emits typed deterministic threshold evidence; verify upper, lower, dual-bound, equality, contributor-order, and shared-metric cases with focused Core tests.
- [x] 2.2 Adapt incomplete referenced-metric evidence to required budget applicability controls and the shared completion projection; verify missing, unmapped, ambiguous, stale, and complete-empty scope cases cannot publish a partial passing value.
- [x] 2.3 Route budget diagnostics through canonical identity, baseline matching, Human/JSON/SARIF, and Testing paths; verify strict/audit mode behavior and exact evidence in focused Core/CLI/Testing tests.

## 3. Authoring guidance and integration validation

- [x] 3.1 Document strict/audit budget authoring, inclusive bounds, neutral measure results, and unassessable scope in the policy-format guide; verify schema examples and documentation links are valid.
- [x] 3.2 Run formatter, OpenSpec validation, affected test projects, schema/output checks, `make lint-architecture`, and risk-appropriate broader validation; record exact outcomes before archiving.
