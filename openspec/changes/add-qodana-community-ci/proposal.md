## Why

Issue #864 introduces an independent ReSharper-based CI signal during post-v0.8.0
engineering-health stabilization. EAP adoption must not weaken existing CI authority.

## What Changes

- Add tokenless Community .NET analysis of the canonical solution in its own advisory workflow.
- Preserve SARIF, logs, runtime/image/cache evidence and deterministic result fingerprints.
- Provide an opt-in cold/warm rerun and positive/negative inspection probe.
- Keep baseline review and the post-burn-in promotion decision explicit and pending.

## Capabilities

### New Capabilities

- `qodana-community-ci`: read-only supplementary analysis and adoption evidence.

### Modified Capabilities

None. Existing CI check names, required checks, Sonar semantics and release authority stay unchanged.

## Impact

Only repository CI tooling, tests and internal documentation. No runtime/package/API changes.
