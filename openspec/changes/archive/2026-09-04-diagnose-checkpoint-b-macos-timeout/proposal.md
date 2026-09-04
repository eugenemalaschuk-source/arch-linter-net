## Why

The real `publish=true`, `version_override=0.8.0` release run stopped before
publication when the macOS x64 v0.8 full-cycle Checkpoint B shard exhausted its
five-minute NUnit watchdog. The cancellation occurred while the current
`change snapshot` was executing, but the aggregated scenario has no per-command
timing record, so the failed run cannot distinguish a single slow command from
the accumulated cost of repeated build preparation.

This must be diagnosed and corrected before v0.8.0 can be published, without
loosening the platform matrix, scenario inventory, evidence aggregation, or
fail-closed watchdog.

## What Changes

- Record bounded elapsed-time command diagnostics for the packed v0.8 full-cycle
  scenario and preserve the completed-phase trace when its NUnit watchdog
  cancels execution.
- Use that trace to remove only demonstrated redundant restore work in the
  full-cycle fixture, while retaining per-command build verification and the
  same required command/evidence scenarios against the immutable packed
  candidate.
- Add focused regressions for timing-trace ordering, cancellation diagnostics,
  and any optimized build-preparation path; document the observed macOS x64
  root cause and bounded runtime decision in the pull request.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `checkpoint-b-release-evidence`: Checkpoint B's required packed full-cycle
  execution exposes bounded phase timing on failure while retaining its existing
  fail-closed candidate, platform, shard, and scenario authority.

## Impact

- `tests/ArchLinterNet.Core.Tests/CheckpointBReleaseGateTests.*.cs` and the
  Checkpoint B process/diagnostic harness.
- Focused Checkpoint B NUnit regressions and the archived OpenSpec capability
  specification after synchronization.
- No production package API, release workflow matrix, required scenario
  inventory, evidence schema, provenance gate, or publication behavior changes.
