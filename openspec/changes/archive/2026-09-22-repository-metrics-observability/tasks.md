## 1. Core metric model and fact inventory

- [x] 1.1 Add versioned typed repository-metrics models for size, coupling, structure, per-project rows, availability, and stable reason codes; verify model construction and JSON field names with Core tests.
- [x] 1.2 Extend the existing source-file fact scan to retain normalized readable C# file identities and physical line totals once per file, preserving generated-file filtering and ownership; verify overlap, generated-file, unreadable-file, and line-ending tests.
- [x] 1.3 Implement one deterministic project-graph metrics calculator with deduplicated edges, ratios, per-project Ca/Ce/instability, Tarjan SCCs, and condensation depth; verify empty, isolated, duplicate-edge, self-loop, cycle, diamond, and deep-chain tests.

## 2. Snapshot and artifact integration

- [x] 2.1 Project repository metrics from the retained analysis session into validation outcomes without changing governance semantics; verify strict/audit outcomes and analysis counters remain unchanged apart from the shared fact projection.
- [x] 2.2 Carry optional repository-metrics data through Architecture Health/validation JSON and human reporting; verify complete, partial, unavailable, and legacy-consumer-compatible output.
- [x] 2.3 Extend architecture change snapshots/reports with optional base/head repository-metrics evidence and a compatible typed delta; verify old artifacts still deserialize and missing/incompatible metrics remain unavailable.

## 3. PR report and badge projections

- [x] 3.1 Extend the Core PR report reader/projector models to retain the optional repository metrics delta and reject incompatible metric evidence without affecting architecture acceptance; verify canonical report tests.
- [x] 3.2 Render one bounded neutral Repository metrics delta section in the existing CLI Markdown report, omitting unchanged noise and never fabricating zeros; verify deterministic Markdown and hostile/unavailable evidence tests.
- [x] 3.3 Add a Core/CLI-owned absolute Source lines Shields payload projection and route verified-main publication through the existing badge transport; verify payload determinism, absolute-only semantics, and workflow contract tests.

## 4. Documentation and evidence

- [x] 4.1 Document metric counting semantics, availability states, governance non-effects, report/delta meaning, and badge provenance; verify documentation references and examples are consistent with the implemented output.
- [x] 4.2 Add profiling or benchmark evidence for one shared projection and no second MSBuild/semantic-analysis pass; verify the focused performance evidence and relevant existing benchmark/test suite.

## 5. Integration validation

- [x] 5.1 Run focused Core and CLI tests, formatter/linter/spec checks, and inspect the complete diff for unrelated changes; verify all required checks pass before spec synchronization.
