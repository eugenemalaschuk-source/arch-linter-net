## 1. Model changes

- [x] 1.1 Add optional `contract_id` field (`[YamlMember(Alias = "contract_id")]`) to `ArchitectureCoverageExclusion` in `ArchitectureContractModels.cs`.

## 2. Shared rule-input field extraction

- [x] 2.1 Extract the per-contract-family "referenced layer names" switch out of `ArchitectureContractRunner.PolicyConsistency.cs`'s `GetReferencedLayerNames` into a shared internal static helper usable from both `ArchitectureContractRunner.PolicyConsistency.cs` and `ArchitectureContractRunner.Coverage.cs`, with no behavior change to existing `FindUnreachableContracts` callers.
- [x] 2.2 Add a lookup from contract ID to the `IArchitectureContract` instance (and its declared family/group) across all layer-bearing contract families (dependency, allow_only, cycle, method_body, independence, protected, external, layer, layer_template-expanded), excluding coverage contracts themselves.

## 3. Loader validation for scope: rule_input

- [x] 3.1 Extend coverage-contract validation in `ArchitectureContractLoader.cs` to accept `scope: rule_input`: require non-empty `contract_ids`, reject `roots`/`between` on a `rule_input` contract.
- [x] 3.2 Validate every `contract_ids` entry resolves to a declared contract ID (using the lookup from 2.2); throw an actionable `InvalidOperationException` naming the unresolved ID otherwise.
- [x] 3.3 Validate every `exclude` entry with `contract_id` set has a non-empty `reason`, mirroring the existing namespace-scope exclusion reason check.

## 4. Coverage runner: rule_input scope

- [x] 4.1 Add a `scope == "rule_input"` branch to `CheckCoverageContract` in `ArchitectureContractRunner.Coverage.cs`.
- [x] 4.2 For each `contract_ids` entry, resolve its layer-bearing fields (via 2.1) against `document.Layers`; classify each field value not found there as `unresolved`.
- [x] 4.3 For each resolved layer name, classify it as `empty-input` when its namespace pattern matches zero entries in `ArchitectureCoverageInventory.Namespaces` (via `ArchitectureLayerResolver.MatchesNamespace`).
- [x] 4.4 Suppress `unresolved`/`empty-input` findings for any `contract_ids` entry matched by an `exclude` entry's `contract_id`.
- [x] 4.5 Emit findings as `ArchitectureViolation` records consistent with the namespace-scope precedent (contract name/id, representative description of the unresolved/empty field, classification text distinguishing `unresolved` from `empty-input`).

## 5. Runner factory guard update

- [x] 5.1 Narrow `ArchitectureRunnerFactory`'s unsupported-coverage-scope filter so only `project`, `assembly`, and `dependency_edge` are rejected; `namespace` and `rule_input` are both accepted.
- [x] 5.2 Update the associated error message text if it enumerates supported scopes.

## 6. Tests and fixtures

- [x] 6.1 Add fixture contracts/policies covering: non-empty (referenced contract matches real code), empty-source, empty-target, unresolved (dangling layer name), intentionally-empty (excluded with reason), audit-only (audit_coverage, warn severity).
- [x] 6.2 Add `RuleInputCoverageContractTests.cs` exercising each fixture against `ArchitectureValidationService.Validate`, asserting findings, severity, and pass/fail behavior.
- [x] 6.3 Update `CoverageContractReservedTests.cs`: remove/adjust any assumption that `scope: rule_input` is unsupported; keep `scope: project` asserted as still-reserved/unsupported.
- [x] 6.4 Add loader-validation tests for: empty `contract_ids`, `roots`/`between` declared alongside `scope: rule_input`, dangling `contract_ids` entry, exclusion `contract_id` without `reason`.

## 7. Documentation and schema

- [x] 7.1 Update `docs/contracts/coverage.md`: move `rule_input` out of "Current limits" reserved list into a documented, implemented scope with its own example and exclusion shape.
- [x] 7.2 Update `docs/reference/yaml-schema.md` for the now-implemented `contract_ids` field and the new exclusion `contract_id` field.
- [x] 7.3 Update `schema/dependencies.arch.schema.json` additively to accept `contract_ids` under `rule_input` scope coverage contracts and `contract_id` under coverage exclusions; confirm the change is additive only.

## 8. Validation

- [x] 8.1 Run `make fmt`.
- [x] 8.2 Run `task acceptance:fresh` and fix any failures.
- [ ] 8.3 Run `openspec validate --all` after archiving to confirm specs remain valid.
