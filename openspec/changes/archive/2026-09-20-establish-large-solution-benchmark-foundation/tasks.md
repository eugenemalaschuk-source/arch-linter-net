## 1. Workload model and deterministic corpus

- [x] 1.1 Add immutable benchmark manifest, dimension, consumer-mode, and topology models with validation for supported independent dimensions; verify invalid dimensions and impossible topology sizes are rejected by focused NUnit tests.
- [x] 1.2 Implement deterministic graph-shape builders for linear/deep, wide fan-out/fan-in, diamond, dense, and cyclic/SCC workloads; verify normalized topology and derived project/edge/SCC counts are identical across repeated builds.
- [x] 1.3 Implement project/type/source/selector/layer/contract/finding dimension expansion with canonical synthetic naming and inventory counts; verify changing one dimension preserves unrelated manifest values and identities.

## 2. Fixture materialization and downstream reuse

- [x] 2.1 Add temporary-root materialization for buildable real-MSBuild workloads, deterministic `.slnx`/`.csproj`/policy/source files, and the current full-governance command shape; verify a generated representative fixture builds and is accepted by the existing CLI fixture boundary.
- [x] 2.2 Add a prebuilt/staged-assembly materialization mode and PR-shaped change metadata while retaining the same workload identity/policy semantics; verify evidence distinguishes external compilation from real-MSBuild preparation.
- [x] 2.3 Publish a canonical workload manifest/schema and reuse guidance for #503, #655, #675, #493, and future v0.9 measurement lanes; verify committed artifacts contain only synthetic/anonymized identities.

## 3. Evidence envelope and measurement contract

- [x] 3.1 Add typed `benchmark-evidence/v1` records for workload identity, dimensions, deterministic counts, raw `analysis-profile/v1` payloads, canonical-result identity, mode/cache/prepared metadata, environment/resource availability, and provenance; verify JSON round-trip and explicit unavailable measurements.
- [x] 3.2 Add canonical normalization/checksum helpers for workload inputs and findings/result projections; verify equivalent temporary roots produce equal identities and a changed result/order is detected.
- [x] 3.3 Add complexity observation and pre-implementation expected-effect records, including multi-size work counters, current/target models, end-to-end bound, trade-offs, success threshold, kill criterion, and non-reproduction disposition; verify required fields serialize deterministically.
- [x] 3.4 Add a versioned JSON Schema and deterministic contract tests for benchmark evidence without changing `analysis-profile/v1`; verify a representative document validates and malformed/unavailable evidence fails closed.

## 4. Test and documentation integration

- [x] 4.1 Add focused NUnit coverage for corpus generation, topology shapes, independent dimensions, real-MSBuild/staged modes, canonical equivalence, and evidence privacy; verify the benchmark matrix itself remains excluded from default test selection.
- [x] 4.2 Document the corpus catalog, dimension methodology, analysis-profile mapping, cold/warm/cache/prepared modes, complexity/effect methodology, expected-effect contract, and non-universality rules; verify documentation references the actual manifest/schema fields.
- [x] 4.3 Run formatter, focused Core tests, OpenSpec validation, and implicated lint checks; inspect the final diff for unrelated files, raw private provenance, generated build artifacts, and file-size violations.

## 5. Finalization

- [x] 5.1 Synchronize the archived `large-solution-benchmarking` specification with the implemented behavior and verify `openspec archive establish-large-solution-benchmark-foundation` plus `openspec validate --all` succeed.
