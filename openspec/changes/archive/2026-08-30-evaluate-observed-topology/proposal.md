## Why

The native topology schema defines a bounded, reviewable model but currently has
no evaluator. As a result, an observed first-party dependency can fall outside
an exhaustive topology without producing the canonical applicability evidence
or structural finding needed to fail closed.

## What Changes

- Evaluate the policy-owned observed subject universe against declared topology
  mappings, reviewed exclusions, and allowed directional edges.
- Produce deterministic native topology evidence for mapped, reviewed
  out-of-scope, unmapped, ambiguous, and stale declarations.
- Feed one topology applicability control through the existing expected-record,
  completion, Human/JSON/SARIF/Testing, baseline, and strict/audit seams.
- Emit normalized structural and relational findings with a deterministic
  representative dependency witness, while preserving applicability gaps as
  distinct unassessable evidence.
- Document the evaluator and add focused Core, CLI, and Testing regression
  coverage without changing pre-topology policy behavior.

## Capabilities

### New Capabilities

- `declared-topology-evaluation`: Deterministic evaluation of observed static
  dependencies against the declared topology model and applicability contract.

### Modified Capabilities

None.

## Impact

The change affects Core analysis/session execution, topology evidence and
diagnostics, the canonical applicability transport and cache reconstruction,
reviewed Core public API, normalized CLI/Testing output, topology documentation,
and focused NUnit fixtures. It consumes the existing declared-topology model
and applicability projection contracts; it does not add a topology-specific
output envelope, baseline identity, diagram parser, or policy mutation path.
