## Why

The former README architecture-policy badge reflected a GitHub workflow result,
not the canonical Architecture Health, waiver debt, and effective-control
evidence that ArchLinterNet itself produces. With pull requests now owning the
complete required architecture gate, publishing a truthful current-main badge
must promote already-validated evidence without rerunning architecture analysis
or deploying documentation after every merge.

## What Changes

- Add `badge architecture-health`, a deterministic Shields endpoint projection
  over canonical `architecture-health/v1` and policy-inventory evidence.
- Keep `badge architecture-policy` behavior-compatible as the narrower legacy
  strict-validation projection.
- Produce an immutable, bounded architecture-health badge payload and manifest
  in the required Architecture Coverage PR job.
- Add a trusted, badge-only main publisher that verifies the merged commit and
  validated PR head have identical Git-tree identities before transporting the
  exact CLI payload to a stable public static endpoint; all missing, stale,
  failed, or mismatched evidence publishes an explicit unavailable state.
- Replace the README's former workflow-status architecture badge with the
  stable Architecture Health endpoint and describe its scope separately from
  Main quality, SonarCloud, and Codecov.
- Add focused CLI and workflow-fixture coverage for health/debt/rule badge
  states, deterministic output, tree-identity promotion, rejection paths, and
  non-Pages badge publication.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `architecture-policy-badge-cli`: add the canonical Architecture Health badge
  projection while retaining legacy architecture-policy behavior.
- `architecture-policy-badge`: replace the workflow-status architecture badge
  with a trusted static Architecture Health payload source.
- `github-actions-ci`: transport and promote immutable PR badge evidence to a
  stable endpoint only after verified merged-tree identity, without duplicate
  main architecture analysis or Pages deployment.

## Impact

- `src/ArchLinterNet.Cli/Commands/Badge` and CLI tests gain the new projection.
- PR CI gains a payload/manifest artifact and a narrowly privileged
  post-merge badge publisher workflow with fixture tests.
- README and CLI documentation distinguish real Architecture Health from
  generic main-quality telemetry.
- The static badge branch/endpoint is an automation-owned publication target;
  no policy, baseline, Health, or inventory semantics move into workflow code.
