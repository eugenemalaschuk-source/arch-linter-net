## 1. Complete metric evidence projections

- [x] 1.1 Cache direct reference types with a completeness flag and verify the reference-graph tests retain best-effort callers while exposing incomplete scans to Core internals.
- [x] 1.2 Add a facts-plus-completeness IL projection for metrics, preserving the validation-compatible facts API, and verify resolver/body failures mark the source incomplete.
- [x] 1.3 Carry incomplete source evidence through topology and external-dependency fact projections and verify complete scans preserve existing deterministic facts.
- [x] 1.4 Short-circuit explicitly unresolved root assemblies before empty-scope metric evaluation and verify a missing target cannot emit a trusted zero.
- [x] 1.5 Preserve `ReflectionTypeLoadException` completeness in the type index and verify a partially loadable target cannot emit a trusted metric value.

## 2. Fail closed in metric evaluation

- [x] 2.1 Make topology relation metrics unassessable for incomplete selected outgoing or potentially contributing incoming sources and verify `missing_required_input`, null value, and no contributors.
- [x] 2.2 Make external-dependency-group metrics unassessable for incomplete selected sources and verify no partial group count is emitted.

## 3. Regression and repository validation

- [x] 3.1 Add unloadable-reference and IL-completeness metric regressions, plus focused CLI formatter/handler and policy-validator coverage, and verify their test filters pass.
- [x] 3.2 Run formatting, policy, architecture, public API, OpenSpec, and risk-based test gates and verify strict OpenSpec validation.
