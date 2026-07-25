# Change: Specify the 0.5.1 adoption-stabilization compatibility contract

## Why

Issue #355 is the shared architecture source of truth for the 0.5.1 stabilization epic. Identity, baselines, API snapshots, diagnostics, build state, output, cache, profiling, cancellation, schemas, migration, and support evidence are implemented by separate tasks. Without one release-level contract, each task can make locally reasonable but mutually incompatible choices.

The Analysis and build state slice is already approved through #387 and `analysis-build-state-fingerprints`. The remaining work is to define the integration contract around that slice, preserve the design-slice rule, and reserve one max-depth final consistency pass before Checkpoint B.

## What Changes

- Add the `adoption-stabilization-compatibility` OpenSpec capability.
- Define one public 0.5.1 boundary and compatibility envelope `adoption-stabilization/v1`.
- Fix the 0.5.1 schema/version registry for policy, fragment, baseline, API snapshot, finding, build state, cache, profiling, and the registry itself.
- Separate stable identity from display/diagnostic evidence.
- Define exact baseline/API lifecycle and typed finding projections.
- Define multi-sink syntax, atomic file output, cache defaults/trust, profiling checkpoints, bounded concurrency, cancellation, policy-only tooling, and support evidence.
- Add the internal compatibility blueprint and child-slice map.
- Require one final max consistency pass across OpenSpec, blueprints, schemas, manifest, CLI/API, migration docs, acceptance corpus, and issue wording.

## Impact

- Affected specs: new `adoption-stabilization-compatibility`.
- Affected docs: new internal compatibility blueprint and internal-doc index.
- Production code: none in this design-only change.
- Public behavior: no unimplemented capability is advertised as currently shipped; child tasks implement the approved slices.
- Downstream issues: #94, #121, #356-#375, #382, #387 and #366 Checkpoint B.
