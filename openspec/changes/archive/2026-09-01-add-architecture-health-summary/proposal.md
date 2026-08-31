## Why

The repository already produces authoritative but separate governance evidence
for current validation, applicability, coverage, baseline debt, waivers,
weakening, metrics, topology, and trusted external diagnostics. Maintainers
need one deterministic, non-scoring summary that preserves those authorities
and fails closed when required evidence cannot be assessed.

## What Changes

- Add the versioned `architecture-health/v1` model, composed exclusively from
  existing canonical governance results.
- Define deterministic, non-compensating gate and health precedence, with
  explicit assessability and independent finding-debt, waiver-debt, new-debt,
  weakening, metric, topology, and external-evidence facts.
- Project the same Core result through human, JSON, and NUnit Testing outputs
  without adding a health-specific evaluator, exit-code scheme, score, or
  hosting-platform dashboard.

## Capabilities

### New Capabilities

- `architecture-health-summary`: Canonical `architecture-health/v1`
  aggregation, assessability, gate/health precedence, and consistent Core,
  CLI, and Testing projections.

### Modified Capabilities

- None.

## Impact

The change adds focused Core model/projection code and tests, then exposes the
canonical result through the existing CLI and Testing adapter paths. It
consumes existing applicability, policy-inventory, debt-gate, metric,
topology, external-evidence, and policy-weakening authorities unchanged.
