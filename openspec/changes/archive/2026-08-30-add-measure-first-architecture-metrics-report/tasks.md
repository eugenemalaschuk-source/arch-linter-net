## 1. Metric definition and policy validation

- [x] 1.1 Add the optional top-level metric-definition model and the closed kind/target/unit schema rules; verify focused policy-load and raw/effective schema tests cover valid definitions, duplicate IDs, unsupported combinations, and legacy policies without metrics.
- [x] 1.2 Extend packaged policy-schema resources and their registry coverage where required; verify schema resource/digest tests and `make policy-check` pass.

## 2. Core measurement authority

- [x] 2.1 Extract the narrow topology and public-surface fact projections required by #516 without duplicating selector, ownership, export-normalization, or applicability semantics; verify focused Core tests for type, namespace, project, assembly, and public-surface projection boundaries.
- [x] 2.2 Implement the reusable metric measurement request/outcome, canonical evaluator, and ArchitectureEngine/application-service seam; verify all six metric kinds, ordinal contributor de-duplication, self-edge/cycle handling, trusted zero, and no partial value on incomplete scope.
- [x] 2.3 Attach metric-native scope evidence through the shared applicability projection and its approved public API surface; verify focused Core tests for unmapped, ambiguous, stale, missing-owner, and unresolved public-surface cases plus reviewed API snapshot checks.

## 3. Read-only CLI reporting

- [x] 3.1 Add the discovered `measure` CLI module, runtime forwarding seam, argument validation, and exit semantics; verify command/module composition, invalid argument, unknown-metric, complete, and unassessable handler tests.
- [x] 3.2 Implement deterministic Human and versioned JSON measurement formatters with bounded/all contributor controls; verify CLI integration tests cover stable ordering, truncation metadata, trusted zero, typed unassessability, and absence of healthy findings/SARIF results.

## 4. Documentation and integration

- [x] 4.1 Document metric declarations and the `measure` workflow with realistic multi-module/topology examples; verify documentation/policy examples remain valid and existing non-measure validation output is unchanged.
- [x] 4.2 Run focused Core and CLI suites, formatter, relevant architecture/policy checks, and strict OpenSpec validation; fix issue-related failures and record exact results.
