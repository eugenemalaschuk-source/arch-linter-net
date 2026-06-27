## Why

ArchLinterNet's configuration sanity checks already catch syntactic problems (missing assemblies, empty layers, unknown external groups), but a policy can still be syntactically valid while expressing contradictory intent — e.g. one rule allows `domain -> application` while another forbids it, or an `independence` contract conflicts with an explicit allowed dependency between the same two layers. These contradictions currently surface only as confusing validation output later, or not at all. Policy authors and AI agents need a dedicated pass that detects internal contradictions in the policy itself, before contracts run against code.

## What Changes

- Add a new policy-consistency validation pass that runs after `configuration_check` and before `contract_checks`, operating on the fully expanded (post layer-template) `ArchitectureContractGroups` for both strict and audit families.
- Detect duplicate contract IDs across strict/audit families and expanded layer templates.
- Detect direct conflicts between allow-only and forbidden dependency contracts for the same source/target layer pair.
- Detect conflicts between independence contracts and explicit allowed/ordered dependency contracts between the same two layers.
- Detect conflicts between a protected surface's `allowed_importers` and a strict forbidden/protected rule that forbids the same importer.
- Detect overlapping layer definitions where the same concrete type/namespace is matched by more than one layer without an explicit documented allowance.
- Detect impossible/unreachable contracts whose source or target layer can never match any type because of mutually exclusive layer settings.
- Add `analysis.policy_consistency: error|warn|off` configuration (default `error`), following the existing `analysis.unmatched_ignored_violations` pattern.
- Emit deterministic diagnostics identifying the conflicting contract IDs/names, layers, and a human-readable reason.

## Capabilities

### New Capabilities
- `policy-consistency-checks`: Policy-level consistency validation pass that detects internal contradictions (duplicate IDs, allow/forbid conflicts, independence conflicts, protected-importer conflicts, layer overlaps, unreachable contracts) in an architecture policy document, independent of code scanning.

### Modified Capabilities
- `diagnostics-model`: Add a `PolicyConsistency` diagnostic kind/record carrying conflicting contract IDs, contract names, layers, and a reason, alongside the existing `Configuration`/`Dependency`/`Cycle` diagnostic kinds.
- `shared-validation-service`: `ValidationRequest`/`ArchitectureValidationService.Validate` gain awareness of `analysis.policy_consistency` so the consistency pass runs as part of the shared pipeline and can fail/warn the outcome per the configured severity.

## Impact

- `src/ArchLinterNet.Core/Validation/ArchitectureValidationService.cs`: new pipeline step between `configuration_check` and `contract_checks`.
- `src/ArchLinterNet.Core/Execution/`: new `PolicyConsistencyChecker` (or similarly named) component, alongside `ArchitectureContractRunner.CheckConfiguration`.
- `src/ArchLinterNet.Core/Model/`: new diagnostic record(s) and `ArchitectureDiagnosticKind` entry.
- `src/ArchLinterNet.Core/Contracts/` / config schema: new `analysis.policy_consistency` setting.
- `schema/`: JSON schema update for the new `analysis.policy_consistency` field.
- Tests: new test fixtures/policies covering green, duplicate IDs, allow-vs-forbid, independence conflicts, protected-importer conflicts, layer overlap, expanded templates, strict/audit interaction.
- No changes to code-scanning behavior (`ArchitectureContractExecutor`) — this is a policy-only pass.
