## Why

The canonical v1 release-forensics JSON writer has become the shared edit point
for independent reporting concerns. Keeping all section projection details in
one type increases change coupling and makes accidental cross-section output
drift more likely, even though the report contract must remain byte-stable.

## What Changes

- Retain one small canonical JSON composition boundary for the v1 report
  envelope, deterministic section order, and terminal canonical text.
- Move analysis/configuration, evidence, hotspot, co-change, bottleneck, and
  OCP section projection details into focused internal reporting collaborators.
- Centralize the existing deterministic category, identity, task-key, and
  ordered-value helpers at one reporting seam rather than duplicating them.
- Add byte-level representative-report and structural regression coverage for
  the decomposition.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `release-forensics-reporting`: Require focused reporting-section projections
  behind a small canonical composition boundary while preserving the v1 bytes.

## Impact

Changes are limited to internal `ArchLinterNet.Core.History.Reporting` source
and focused Core History tests. The v1 schema, scoring and evidence semantics,
Git/policy dependency boundary, CLI behavior, and reviewed public API remain
unchanged.
