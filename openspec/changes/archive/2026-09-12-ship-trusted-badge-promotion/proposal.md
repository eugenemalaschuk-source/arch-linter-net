## Why

The current Architecture Health badge publisher is a repository-specific workflow with trust-sensitive logic embedded in a privileged job. Private and public consumers cannot adopt the same exact merged-tree provenance, artifact, disclosure, and renewal guarantees without copying that implementation. Issue #830 makes the promotion contract reusable and versioned before the v0.8.x completeness release while preserving the existing public raw endpoint.

## What Changes

- Extract transport-independent evidence resolution, validation, and monotonic promotion into a tested, versioned shared implementation.
- Add thin, explicitly configured `github-raw`, `relay`, and `none` adapters; reject arbitrary producer selection, URLs, private-repository raw publication, and unapproved configurations.
- Publish a pinned reusable workflow/action contract that reads approved setup, validates exact PR/merge/tree/check/run/attempt/job/artifact provenance, and never executes consumer-controlled code or artifacts.
- Support push/squash publication, bounded retry/recovery, and metadata-only renewal using fresh authorization/evidence checks and the product-owned validity horizon; do not rerun main analysis during renewal.
- Preserve fail-closed unavailable semantics, fixed redacted diagnostics, artifact/archive safety limits, OIDC audience and workflow identity binding, Relay CAS/lease boundaries, and the legacy public raw adapter.
- Add adversarial fixtures, workflow/security checks, adapter and protocol tests, and consumer-facing configuration/documentation needed by downstream #825/#834/#836 work.

## Capabilities

### New Capabilities

- `architecture-health-badge-promotion`: Shared trusted promotion, reusable workflow/action configuration, exact provenance validation, adapter selection, publication/renewal protocol, and security-tested consumer contract.

### Modified Capabilities

- `architecture-health-badge-relay-runtime`: Extend the publisher-facing contract for bounded challenge/renewal/recovery integration without weakening the existing Relay authority or CAS rules.
- `architecture-health-publication-evidence`: Require promotion and renewal paths to preserve the product-owned semantic validity horizon and reject stale or incomplete evidence.

## Impact

Affected areas include `.github/workflows`, new repository-owned promotion/action helpers and fixtures, the existing badge publisher and CI artifact contract, Relay integration boundaries, security/workflow lint tests, and adoption documentation. No Core/CLI architecture boundary or existing public Health semantics are changed; release publication remains owned by #806.
