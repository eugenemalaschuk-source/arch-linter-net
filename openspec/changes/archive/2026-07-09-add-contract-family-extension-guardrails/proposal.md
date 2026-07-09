## Why

The #208–#216 refactoring chain replaced ArchLinterNet's central contract-family bottlenecks (a monolithic catalog, loader-owned family validation, session-owned family checking/config inspection, a violation property bag, and a YAML mega-DTO) with focused extension points: `Execution.ArchitectureContractFamilyRegistry`, `Execution.Checkers`, `Execution.Abstractions` contributors, `Model` diagnostic payloads, and `Contracts.Families`/`Contracts.Validators`. Nothing in `architecture/dependencies.arch.yml` yet protects that recovered shape, and the "Contracts must not depend on Execution" boundary the new `Contracts.ArchitectureContractFamilyBinding` design relies on is currently documented only in a source comment, not enforced. #215 is the closing governance task for #183 and closes that gap using only capabilities the product already supports, following the same pattern #142 used for the prior refactor chain.

## What Changes

- Add a `core-contracts-must-not-depend-on-hosts` dependency rule: `core_contracts` (family/validator internals) must not depend on `cli`, `testing`, or `unity`, mirroring the existing `core-execution-must-not-depend-on-hosts` rule.
- Add a `core-contracts-must-not-depend-on-execution` dependency rule: `core_contracts` must not depend on `core_execution`, turning the `ArchitectureContractFamilyBinding.cs` comment's documented boundary ("Contracts must not depend on Execution") into an enforced rule. No production code changes needed — confirmed via source scan that no `Contracts` file currently references `ArchLinterNet.Core.Execution` in code (only in a comment).
- Extend the existing `self-policy-rule-input-coverage` `strict_coverage` contract's `contract_ids` with both new rule IDs.
- Document, in the `self-architecture-policy` spec, that the following extension-hotspot regressions are enforced by code review rather than a YAML contract (no ArchLinterNet contract family detects "a file grew a new inline branch"; confirmed against `docs/policy-format/supported-capabilities.md`'s unsupported list):
  - `ArchitectureContractFamilyRegistry`/`ArchitectureContractFamilyBindings` regrowing inline per-family conditionals instead of new descriptor/binding entries.
  - `ArchitectureAnalysisSession` regaining inline per-family checking or configuration-inspection logic instead of routing through `Execution.Checkers`/`Execution.Abstractions` contributors.
  - `ArchitectureDiagnosticMapper.FromViolation` regrowing an if/switch dispatch chain instead of new families supplying an `IArchitectureDiagnosticPayload`.
  - `ArchitectureContractModels.cs`/`ArchitectureContractGroups` regrowing inline `[YamlMember]` clusters instead of new `Contracts/Families/*.cs` files.
- Extend the existing "new contract-family implementations require self-policy coverage or a documented exception" requirement to explicitly name the now-established extension namespaces (`Execution.Checkers`, `Execution.Abstractions`, `Contracts.Families`, `Contracts.Validators`, `Model` payload records) as the required home for new family code.
- Add a "Guardrail candidate for #215" paragraph to `docs/internal/core-architecture-blueprint.md`, alongside the existing #142 paragraph, naming the four documentation-governed guardrails above with exact file paths.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `self-architecture-policy`: add requirements for the `core_contracts` host-isolation rule, the `core_contracts`/`core_execution` independence rule, the extended rule-input coverage list, and the documentation-governed extension-hotspot guardrails (central catalog/binding growth, god-session regression, diagnostic-mapper dispatch regrowth, YAML DTO regrowth).

## Impact

- `architecture/dependencies.arch.yml`: three new/modified contract entries (two `strict`, one `strict_coverage` `contract_ids` extension).
- `openspec/specs/self-architecture-policy/spec.md`: new requirements via archive.
- `docs/internal/core-architecture-blueprint.md`: one new guardrail-candidate paragraph.
- No `src/` production code changes expected — the recovered #208–#216 architecture already satisfies every new rule; this change only adds guardrails that catch a future regression.
- `make lint-architecture` / `make acceptance` must continue to pass against the repository's own build output.
