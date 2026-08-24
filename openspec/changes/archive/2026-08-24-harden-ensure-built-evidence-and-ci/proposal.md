## Why

`--ensure-built` must not lose the provenance collected during metadata preparation, nor may it
attest a selected artifact that the graph build did not produce. The installed Windows regression
that proves this behavior must execute in the mandatory pull-request matrix rather than only as a
locally invocable packed-artifact test.

## What Changes

- Preserve each successful metadata preparation in snapshot state immediately and use it when
  cancellation or evaluation-error projections need input provenance before runner materialization.
- Derive one effective build-output context from CLI overrides and policy defaults, including
  configuration, target framework, runtime identifier, and platform; apply it consistently to graph
  build, post-build resolution, manifests, and receipt publication.
- Add a reverse-configuration regression proving that a policy-selected Release output is rebuilt
  and receipted when no CLI configuration override is supplied.
- Add a dedicated Windows packed-artifact Make target and required PR workflow shard for the
  installed-tool write-conflict oracle, including its evidence artifact.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `analysis-build-state-preflight`: preserve prepared-input provenance and publish receipts only
  for artifacts built under the effective output context.
- `github-actions-ci`: execute the Windows installed-tool rebuild oracle as a required
  packed-artifact scenario shard with collected evidence.

## Impact

Affected code includes snapshot construction, build-state context/output selection, NUnit
regressions, packed-artifact Make routing, and the PR CI matrix. There are no public API, policy
syntax, or receipt-schema changes.
