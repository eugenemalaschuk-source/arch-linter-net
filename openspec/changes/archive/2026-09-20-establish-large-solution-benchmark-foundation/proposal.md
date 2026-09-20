## Why

Performance work for v0.9 currently has a fixed large adopter fixture and several
issue-specific timing harnesses, but no canonical way to vary solution shape,
attribute deterministic work, or compare memory and result evidence. This makes
large-solution decisions difficult to reproduce and encourages each downstream
measurement lane to grow its own benchmark corpus.

## What Changes

- Add a deterministic synthetic workload generator for independent project,
  type, reference/edge, selector/layer, contract, finding, source-root, and
  graph-topology dimensions.
- Provide reusable linear, wide, diamond, dense, cyclic/SCC, project/type,
  selector, contract, finding, source-root, real-MSBuild, staged-assembly, full
  governance, multi-command, and PR-shaped workload definitions.
- Add a versioned benchmark evidence document that composes raw
  `analysis-profile/v1` profiles with workload identity, deterministic counts,
  canonical-result identity, environment/resource measurements, complexity
  observations, and issue-specific expected-effect/disposition data.
- Add focused deterministic tests proving fixture repeatability, independently
  variable dimensions, topology construction, canonical-result identity, and
  evidence serialization/validation.
- Document the benchmark corpus, evidence schema, measurement methodology, and
  downstream reuse contract for #503, #655, #675, #493, and future v0.9 lanes.
- Do not implement any graph, selector, cache, prepared-state, incremental, or
  concurrency optimization and do not add universal timing SLAs.

## Capabilities

### New Capabilities

- `large-solution-benchmarking`: Reusable deterministic large-solution workload
  generation and analysis-profile-based evidence for performance and scale
  investigations.

### Modified Capabilities

<!-- No existing requirement changes; the new evidence composes the existing
     analysis-profile/v1 contract without changing its public schema. -->

## Impact

- Adds benchmark/test infrastructure under `tests/ArchLinterNet.Core.Tests` and
  checked-in internal documentation/schema fixtures.
- Adds no production dependency, package, CLI, public API, or policy-semantic
  change.
- Extends the existing synthetic adoption corpus rather than replacing its
  current v0.8 full-governance fixture.
- Downstream benchmark issues consume workload manifests and evidence records;
  they remain responsible for their own measurement decisions and optimization
  outcomes.
