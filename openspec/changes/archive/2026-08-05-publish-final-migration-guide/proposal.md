## Why

The implemented 0.5.1 stabilization surfaces are documented across individual
reference pages, but adopters do not yet have one release-accurate migration
path or small, status-correct entrypoints for their environment. This is the
last documentation slice before the repository-wide consistency pass and the
packed-artifact release gate.

## What Changes

- Publish a single 0.5.1 adoption and migration guide that separates a
  greenfield path from an upgrade from 0.5.0, with explicit, reviewable
  baseline and API-snapshot workflows.
- Add thin, copy-pasteable direct CLI, POSIX shell, PowerShell, Make, Task,
  Tilt, GitHub Actions, and CI-neutral entrypoint examples that safely route
  arguments, preserve streams, and propagate the product exit code.
- Reconcile public documentation, the capability manifest, CLI reference,
  schema guidance, support claims, AI guidance, and release notes with the
  shipped 0.5.1 contracts for output, cache, profile, concurrency,
  cancellation, and offline schema discovery.
- Add executable external-consumer documentation checks using the synthetic
  adoption corpus and freshly packed local artifacts.

## Capabilities

### New Capabilities

- `adoption-migration-guidance`: Release-qualified, accessible user guidance
  and verified reference entrypoints for adopting or upgrading to 0.5.1.

### Modified Capabilities

- None.

## Impact

Affected areas are the MkDocs user and AI guidance, CLI/reference pages,
release notes and capability manifest, synthetic adoption fixtures, and
documentation validation tests. No Core, CLI, or Testing product semantics are
changed; the guide documents their existing public contracts.
