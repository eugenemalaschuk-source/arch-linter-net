## Why

The CLI already evaluates `strict,audit` from one immutable snapshot, but the primary CI and adoption guidance still illustrates two independent full validation processes. Repositories that intentionally require both views therefore duplicate `--ensure-built` preparation and analysis work despite an existing, semantically equivalent combined path.

## What Changes

- Make combined `--mode strict,audit --ensure-built` the documented canonical invocation when one workflow requires both modes over the same build state, while retaining separate strict-gating and non-blocking-audit workflows.
- Add deterministic regression evidence that the combined ensure-built path uses one snapshot/preparation and preserves both standalone mode outcomes and combined exit behavior.
- Keep multi-report routing explicitly tied to the same completed combined analysis, with profile counters distinguishing rendering/output work from analysis work.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `analysis-snapshot`: Make the combined CLI's one-snapshot, ensure-built preparation boundary and profile-counter evidence explicit.
- `adoption-migration-guidance`: Document combined strict-and-audit validation as the canonical CI/adoption path when both views are required from one build state.

## Impact

- CLI and Core regression tests for combined validation, preparation, semantic equivalence, and multi-sink profile evidence.
- CI, adoption, upgrade, reference-entrypoint, and output-format documentation.
- No public API, policy schema, cache format, or cross-process prepared-state mechanism changes.
