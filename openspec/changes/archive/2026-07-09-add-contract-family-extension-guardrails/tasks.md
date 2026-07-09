## 1. Policy rules

- [x] 1.1 Add `core-contracts-must-not-depend-on-hosts` to `strict` (source: `core_contracts`, forbidden: `[cli, testing, unity]`)
- [x] 1.2 Add `core-contracts-must-not-depend-on-execution` to `strict` (source: `core_contracts`, forbidden: `[core_execution]`)
- [x] 1.3 Add `core-contracts-must-not-depend-on-hosts` and `core-contracts-must-not-depend-on-execution` to `self-policy-rule-input-coverage`'s `contract_ids` (in `strict_coverage`)

## 2. Documentation

- [x] 2.1 Add a "Guardrail candidate for #215" paragraph to `docs/internal/core-architecture-blueprint.md`, alongside the existing #142 paragraph, naming: central catalog/binding branch growth (`ArchitectureContractFamilyRegistry`/`ArchitectureContractFamilyBindings`), god-session regression (`ArchitectureAnalysisSession`), diagnostic-mapper dispatch regrowth (`ArchitectureDiagnosticMapper.FromViolation`), and YAML DTO regrowth (`ArchitectureContractModels`/`ArchitectureContractGroups`)
- [x] 2.2 Confirm no other docs cross-reference needs updating for the two new contract IDs (`docs/policy-format`/`docs/contracts` describe generic contract shape, not this repo's specific rule set) — confirmed, no update needed

## 3. Validation

- [x] 3.1 Run `make fmt`
- [x] 3.2 Run `make acceptance` (lint + full test suite) and confirm the repository's own policy still passes in strict mode with the two new rules and extended coverage list — exit 0, 952 Core + 112 Cli + 3 Unity tests passed, including `SelfArchitecturePolicyTests.RepositoryPolicy_ValidatesOwnInternalBoundaries`
- [x] 3.3 Confirm `openspec validate --all` passes after spec sync — 79/79 passed

## 4. Spec sync and archive

- [x] 4.1 Run `openspec archive add-contract-family-extension-guardrails` after implementation and tests pass
- [x] 4.2 Verify `openspec/specs/self-architecture-policy/spec.md` reflects the new and modified requirements — confirmed: 2 requirements added, 2 modified
