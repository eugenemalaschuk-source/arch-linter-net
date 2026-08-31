## Why

The bounded SARIF reader, external-diagnostic selector, normalized finding projection, and
applicability projection each have focused tests, but their trust and provenance contract needs
one clear capability-level acceptance suite. Without it, a local change can preserve a component
in isolation while silently breaking the evidence chain that downstream consumers rely on.

## What Changes

- Add deterministic, public-safe synthetic SARIF reference scenarios that compose the complete
  external-diagnostics federation path.
- Prove that current evidence, trusted zero results, trust failures, deterministic identity, and
  native/imported output coexistence retain their required semantics end to end.
- Document the vendor-neutral current-context evidence flow without adding analyzer execution,
  remote fetching, or producer-service dependencies.

## Capabilities

### New Capabilities

- `external-diagnostics-federation`: Cross-boundary reference scenarios for trusted SARIF
  evidence, selected imported diagnostics, canonical projections, and applicability evidence.

### Modified Capabilities

- None.

## Impact

Affected areas are the Core NUnit reference-scenario tests, the external-evidence policy guide,
and OpenSpec artifacts. No public API, policy schema, analyzer integration, or external service
dependency is introduced.
