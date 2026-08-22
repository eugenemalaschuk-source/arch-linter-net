## Context

`baseline verify` already collects complete authoritative candidates and compares
them to a reviewed baseline using `ArchitectureViolationIdentity`; its
comparison status is the authority for persistent finding debt. `policy
weakening` separately compares two explicit effective-policy context artifacts
and produces normalized semantic or `impact_not_proven` guardrail findings.
Neither command currently returns one typed result or one CI exit decision.

## Goals / Non-Goals

**Goals:**

- Compose the existing baseline and weakening authorities without duplicating
  their comparison, identity, or policy-loading logic.
- Make an explicit gate fail closed when the baseline comparison cannot be
  trusted and fail on configured error-severity weakening.
- Preserve a deterministic, typed projection across Human, JSON, SARIF, and
  the Testing adapter.

**Non-Goals:**

- Add a `ratchet` evaluation mode or alter strict/audit behavior.
- Write, generate, update, prune, migrate, or approve baseline files.
- Reimplement policy-weakening detection or infer persistent debt from it.
- Add partial/changed-file authoritative analysis.

## Decisions

### Add one Core orchestration seam, not another comparer

Introduce a typed `ArchitectureDebtGateRequest` and `ArchitectureDebtGateResult`
in Core. The gate calls the existing baseline verification service for the
persistent-debt section and the existing policy-weakening comparer only when
both explicit contexts are supplied. This retains exact occurrence and
cross-assembly identity behavior and avoids an alternate baseline lifecycle.

The gate result contains an evaluation receipt, persistent-debt comparison
entries, and an optional weakening section. Its decision is false if baseline
verification is out of sync (new, resolved, stale/configuration, or ambiguous
entries), collection/preflight fails, or the independently computed weakening
result has error-severity findings. Warning/off weakening stays visible and is
not converted into baseline debt.

Alternative considered: compose CLI output from two process-like handlers.
Rejected because Core and Testing consumers would have no typed single result,
and JSON/SARIF parity would become formatting-dependent.

### Expose a `gate` command with explicit inputs

Add a top-level instance-based CLI module:

`arch-linter-net gate --policy <path> --baseline <path> [--base-context <path> --current-context <path>] [--mode strict|audit|all] [--format human|json|sarif]`

`--baseline` is mandatory. Policy contexts are optional as a pair: supplying
only one is an invalid invocation. The command forwards existing build-state
selectors to baseline candidate collection, so current persistent findings are
always derived from complete ordinary analysis rather than a changed-file
subset.

Alternative considered: extend `baseline verify`. Rejected because it would
blur baseline maintenance/lifecycle with change-time policy guardrails and
would make weakening appear to be baseline debt.

### Use a gate-specific formatter and SARIF run

Core normalizes the combined result by stable section and finding identity. The
formatter emits separately named `evaluation`, `persistent_debt`, and
`policy_weakening` JSON sections. SARIF contains result properties that identify
the section and preserve respectively baseline lifecycle/identity or weakening
classification/severity/provenance; it never maps weakening records onto
baseline statuses.

### Testing mirrors the Core request/result

The fluent Testing builder gains a gate operation based on its configured policy
and baseline. Explicit context-artifact paths opt into weakening comparison. It
returns the public typed gate result for direct assertions rather than requiring
tests to parse a CLI report.

## Risks / Trade-offs

- [A baseline that contains resolved entries fails the gate even with zero new
  findings] → This is intentional fail-closed lifecycle behavior; output makes
  resolved debt and the explicit prune workflow visible.
- [One command has many optional build selectors] → Reuse the established
  baseline command option shape and guard helpers rather than add new selector
  semantics.
- [SARIF needs two semantically different result families] → Use distinct rule
  namespaces and typed properties; retain the existing baseline and weakening
  fields unchanged in their respective section.

## Migration Plan

The feature is additive. Existing validate/baseline/policy commands retain
their parsing, output, and exit codes. CI adopters add the new explicit command
after retaining their reviewed baseline and, where required, independently
exported base/current policy contexts. Removing the command restores the prior
workflow with no persisted-state migration or rollback action.

## Open Questions

None. The issue supplies the required authority boundaries and the existing
baseline/weakening APIs provide the composition inputs.
