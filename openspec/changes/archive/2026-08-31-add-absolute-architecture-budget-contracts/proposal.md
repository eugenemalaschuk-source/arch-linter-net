## Why

Metric declarations currently let a policy author observe architecture size and
dependency values, but cannot make a reviewed absolute limit enforceable.
Policies need deterministic strict and audit budget gates without changing the
meaning of a metric or allowing incomplete scope to look compliant.

## What Changes

- Add strict and audit metric-budget contracts that reference a declared metric
  and declare one or both absolute `minimum` and `maximum` limits.
- Validate budget IDs, referenced metric IDs, non-negative integer limits, and
  coherent lower/upper bounds through the existing schema and typed policy
  validation paths.
- Evaluate budgets exclusively from the shared metric evaluator, producing a
  normal deterministic finding only when an evaluable metric violates a limit.
- Project incomplete, unmapped, ambiguous, stale, or otherwise insufficient
  metric scope through the common applicability/completion model so it cannot
  lower a budget silently.
- Carry budget findings through the canonical identity, baseline, human, JSON,
  SARIF, and testing result paths, with the measured value, breached limit,
  metric subject, and sorted contributors as evidence.
- Document authoring and validation examples while preserving all policies that
  declare no budget contracts.

## Capabilities

### New Capabilities

- `architecture-metric-budgets`: Declarative strict/audit absolute budget
  contracts over previously declared, shared-evaluator architecture metrics.

## Impact

- Core policy models, schema, validators, contract-family execution, normalized
  diagnostics, applicability projection, and public API review snapshot.
- Core and CLI/Testing output and baseline integration tests, plus policy-format
  documentation and examples.
- No new external dependencies, runtime/test/performance metric kinds, formulas,
  or baseline-relative budget semantics.
