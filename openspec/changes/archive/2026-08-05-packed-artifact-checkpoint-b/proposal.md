## Why

Checkpoint A and the final consistency audit establish that the repository's
implementation and documented contract are coherent, but neither consumes the
packages that a 0.5.1 adopter will install. The single public release boundary
therefore needs reproducible, synthetic packed-artifact evidence before the
candidate can be authorized for publication.

## What Changes

- Add a Checkpoint B NUnit acceptance harness that packs the candidate,
  installs it from an isolated local feed, and reuses the adoption corpus.
- Exercise direct CLI, external `ArchLinterNet.Testing`, generic CI-neutral,
  offline schema, non-TTY, cache, parallelism, and cancellation scenarios
  against the packed candidate artifacts.
- Generate a deterministic, committed release-evidence summary with candidate
  package identities, scenario results, observed platform/shell support, gate
  results, exclusions, and the explicit Checkpoint B outcome.
- Preserve the release boundary: a failed matrix is release-blocking and this
  change does not publish packages.

## Capabilities

### New Capabilities

- `checkpoint-b-release-evidence`: Synthetic, packed-artifact acceptance and
  deterministic 0.5.1 release-evidence requirements.

### Modified Capabilities

- `adoption-stabilization-compatibility`: Make the existing Checkpoint B
  authorization requirement reference reproducible packed-artifact evidence.

## Impact

Affected areas are the shared adoption acceptance tests and fixtures, package
metadata validation, release documentation/evidence, and OpenSpec contracts.
No public product API or package is published by this change.
