## Why

Architecture validation can be made to pass by weakening the policy instead of
fixing the code.  The existing effective policy-context export and complete
architecture change snapshots provide authoritative inputs, but neither
classifies a base-to-current policy weakening for a change workflow.

## What Changes

- Add a policy-weakening guardrail that deterministically compares separately
  produced base and current effective policy-context artifacts.
- Report normalized, provenance-rich findings for provable enforcement,
  static governed-scope, permission/prohibition, and exception weakening;
  retain bounded suspicion when selector impact cannot be proved.
- Add explicit, schema-backed `analysis.policy_weakening` severity and a
  `policy weakening` CLI workflow with deterministic human, JSON, and SARIF
  projections.  Ordinary validation remains unchanged unless that workflow is
  invoked.
- Fail closed for incomplete, unsupported, or incompatible context artifacts;
  never claim exact affected subjects without trusted membership evidence.
- Document artifact production from separate repository states, configured
  migration handling, supported exact comparisons, and known limits.

## Capabilities

### New Capabilities

- `policy-weakening-guardrails`: Typed, deterministic base-to-current policy
  comparison and normalized weakening evidence for change-time CI guardrails.

### Modified Capabilities

- `policy-context-export`: Expose the schema-backed policy-weakening severity
  in the versioned effective policy context used as comparison input.
- `cli-command-dispatch`: Add the instance-based `policy weakening` subcommand
  to the existing policy command family.

## Impact

`ArchLinterNet.Core` gains a public guardrail comparison model and formatter,
the policy analysis schema and policy-context projection gain one explicit
severity setting, and `ArchLinterNet.Cli` gains a policy subcommand.  Core and
CLI tests, public documentation, API snapshots, self-policy, and output-format
guidance will be updated.  This change does not modify ordinary strict/audit
validation, baseline lifecycle, candidate-policy simulation, or #121's
combined new-debt gate.
