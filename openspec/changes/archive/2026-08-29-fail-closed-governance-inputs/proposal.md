## Why

A valid governance request can currently be represented only as pass or failure. That makes it possible for a future v0.8 control with missing, stale, wrong-context, unexpectedly empty, unmapped, or ambiguous required evidence to look like a clean empty result. Assessment trust must be explicit before the topology, exposure, budget, and imported-diagnostics families begin implementing their evaluators.

## What Changes

- Add a canonical, opt-in assessment-completion contract that distinguishes a trusted pass, a trusted architecture failure, and a valid-but-unassessable assessment.
- Define how v0.8 family schemas declare required versus explicitly optional evidence, and how absent, unexpectedly empty, stale, unmapped, ambiguous, and wrong-context evidence becomes deterministic control-level unassessability rather than a zero-result pass.
- Extend the shared validation outcome boundary with typed, provenance-bearing completion evidence that future family evaluators can populate without treating configuration errors as architecture findings.
- Preserve the CLI's public `0`/`1`/`2` categories: authoritative pass maps to `0`, trusted failure to `1`, and valid-but-unassessable assessment to `2`, with a stable machine-readable completion reason distinct from invalid invocation, invalid policy, and runtime failures.
- Keep existing policies behavior-compatible until a v0.8 family explicitly opts into applicability semantics. Normalized Human/JSON/SARIF/Testing finding projection remains the shared follow-up owned by #507.

## Capabilities

### New Capabilities

- `governance-assessment-completion`: typed trusted/unassessable completion semantics, reason/provenance requirements, and the required/optional evidence boundary for v0.8 controls.

### Modified Capabilities

- `governance-applicability-evidence`: require future applicability-producing families to use the canonical completion boundary rather than infer an empty result as evaluable.
- `shared-validation-service`: expose assessment completion separately from ordinary conformance findings in the shared validation outcome.
- `cli-validation`: map authoritative assessment completion states to the existing `0`/`1`/`2` public exit-code categories without conflating unassessability with invalid invocation or configuration.
- `test-adapter`: retain typed assessment-completion evidence for NUnit-facing assertions and failure messages.

## Impact

The change establishes Core validation-result and CLI completion seams, plus focused Core/CLI/Testing coverage. It does not introduce a generic YAML field, a new CLI command, family-specific topology/exposure/budget/SARIF evaluation, normalized finding identity/output work, or Architecture Health aggregation.
