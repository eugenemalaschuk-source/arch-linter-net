## 1. Versioned metric-baseline lifecycle

- [x] 1.1 Add the version-3 metric-baseline model, canonical identity validation, loader attachment, and deterministic baseline generation/preservation flow; verify focused baseline model, loader, generator, update, and round-trip tests.
- [x] 1.2 Extend baseline schema resources and compatibility metadata for version 3, and verify schema/packaged-schema validation tests cover legacy compatibility and malformed metric entries.

## 2. Relative metric-budget contracts and evaluation

- [x] 2.1 Add `baseline_mode` and `max_delta` policy contract fields, closed schema/raw/typed validation, policy-context facts, and policy-weakening comparison; verify focused policy, schema, and context tests.
- [x] 2.2 Compare matching complete metric baselines through the shared evaluator, fail closed through metric-budget applicability evidence when absent or stale, and record capture candidates separately from finding debt; verify strict/audit, missing/stale, shared-metric, cap, and arithmetic-boundary tests.
- [x] 2.3 Project additive baseline-relative fields through typed diagnostics, normalized Human/JSON/SARIF/Testing output, and reviewed public API artifacts; verify output parity and public API review checks.

## 3. Guidance and integration

- [x] 3.1 Document authoring, explicit capture, no-worse/delta/cap behavior, stale/missing behavior, and separation from finding debt; verify documentation formatting and links.
- [x] 3.2 Run formatter, OpenSpec validation, focused and affected project tests, policy/schema checks, `make lint-architecture`, and risk-appropriate broader validation; inspect all results before archive.
