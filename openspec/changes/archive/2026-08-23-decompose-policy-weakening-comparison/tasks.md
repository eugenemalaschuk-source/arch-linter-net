## 1. Comparison support boundary

- [x] 1.1 Extract context validation, compatible-pair validation,
  membership-evidence resolution, and canonical digesting into internal
  comparison context support.
- [x] 1.2 Delegate formatter deserialization and public digest compatibility
  APIs to the new support without changing formatter projections.

## 2. Focused comparison evaluators

- [x] 2.1 Reduce the public comparer to request validation, fixed evaluator
  orchestration, and deterministic aggregation.
- [x] 2.2 Extract focused internal evaluators for enforcement, analysis scope,
  static/source scope, contract facts/optionality, exceptions, and selectors.
- [x] 2.3 Reuse canonical comparison helpers and preserve the exported-context
  trust boundary without a registry or plugin abstraction.

## 3. Regression coverage and verification

- [x] 3.1 Add focused rule-family and cross-family aggregation regressions for
  exact normalized findings and formatter projections.
- [x] 3.2 Run focused Core tests and all directly implicated formatter,
  architecture, size, public-API, formatting, and OpenSpec checks.
