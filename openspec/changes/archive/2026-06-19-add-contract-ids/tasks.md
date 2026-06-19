## 1. Model — Add Id to all contract types

- [x] 1.1 Add `IArchitectureContract` interface with `Name` and `Id` properties to `ArchitectureContractModels.cs`
- [x] 1.2 Add `string? Id` property with `[YamlMember(Alias = "id")]` to `ArchitectureDependencyContract`
- [x] 1.3 Add `string? Id` property to `ArchitectureLayerContract`
- [x] 1.4 Add `string? Id` property to `ArchitectureAllowOnlyContract`
- [x] 1.5 Add `string? Id` property to `ArchitectureCycleContract`
- [x] 1.6 Add `string? Id` property to `ArchitectureMethodBodyContract`
- [x] 1.7 Add `string? Id` property to `ArchitectureAsmdefContract`
- [x] 1.8 Add `string? Id` property to `ArchitectureIndependenceContract`

## 2. Loading — ID normalization and duplicate validation

- [x] 2.1 Add `NormalizeToContractId(string name)` helper to `ArchitectureContractLoader` (lowercase + spaces-to-hyphens + non-alnum-to-hyphen stripping)
- [x] 2.2 Add post-deserialization step in `LoadFromPath` to compute fallback IDs for all contracts without explicit `id`
- [x] 2.3 Add duplicate ID validation scoped to each contract type within each mode group
- [x] 2.4 Throw `InvalidOperationException` with diagnostic message on duplicate IDs

## 3. Runner — Contract filtering

- [x] 3.1 Add `HashSet<string>? SelectedContractIds` parameter to `ArchitectureContractRunner` constructor
- [x] 3.2 Add `IsContractSelected(string? contractId)` helper method on runner
- [x] 3.3 Apply filter to `CheckContract` (dependency contracts)
- [x] 3.4 Apply filter to `CheckLayerContract` (layer contracts)
- [x] 3.5 Apply filter to `CheckAllowOnlyContract` (allow-only contracts)
- [x] 3.6 Apply filter to `CheckCycleContract` (cycle contracts)
- [x] 3.7 Apply filter to `CheckMethodBodyContract` (method body contracts)
- [x] 3.8 Apply filter to `CheckAsmdefContract` (asmdef contracts)
- [x] 3.9 Apply filter to `CheckIndependenceContract` (independence contracts)
- [x] 3.10 Ensure `CheckConfiguration` is NOT filtered

## 4. CLI — --contract flag

- [x] 4.1 Add `--contract` argument definition to `Program.cs` (multi-value, no default)
- [x] 4.2 Collect `--contract` values into `List<string>`
- [x] 4.3 Validate selected IDs exist in loaded contracts (strict or audit based on mode)
- [x] 4.4 Return exit code 2 with diagnostic on unknown IDs (list unknown + valid IDs)
- [x] 4.5 Pass `HashSet<string>` filter to `ArchitectureContractRunner` constructor

## 5. Output — Include contract IDs

- [x] 5.1 Add `string? ContractId` field to `ArchitectureViolation` record
- [x] 5.2 Update all violation creation sites to pass `ContractId`
- [x] 5.3 Update `FormatViolationsForHumans` to include `[{id}]` prefix
- [x] 5.4 Update `FormatCyclesForHumans` to include `[{id}]` prefix
- [x] 5.5 Update `FormatResultForCiArtifacts` to include `contract_id` in JSON
- [x] 5.6 Update `FormatViolationsForCiArtifacts` to include `contract_id`
- [x] 5.7 Update `FormatCyclesForCiArtifacts` to include `contract_id`

## 6. Schema — JSON Schema update

- [x] 6.1 Add optional `id` property (type string, minLength 1) to `dependencyContract` `$def`
- [x] 6.2 Add optional `id` to `layerContract` `$def`
- [x] 6.3 Add optional `id` to `allowOnlyContract` `$def`
- [x] 6.4 Add optional `id` to `cycleContract` `$def`
- [x] 6.5 Add optional `id` to `methodBodyContract` `$def`
- [x] 6.6 Add optional `id` to `asmdefContract` `$def`
- [x] 6.7 Add optional `id` to `independenceContract` `$def`

## 7. Tests — Contract model + loading

- [x] 7.1 Test: explicit `id` deserialized correctly
- [x] 7.2 Test: omitted `id` produces null before normalization
- [x] 7.3 Test: fallback ID normalization from name (standard and edge cases)
- [x] 7.4 Test: duplicate IDs in same contract type + mode group throw
- [x] 7.5 Test: same ID across different contract types allowed
- [x] 7.6 Test: same ID across strict and audit allowed
- [x] 7.7 Test: `IArchitectureContract` interface contract (all types implement it)

## 8. Tests — Selective execution

- [x] 8.1 Test: runner with no filter executes all contracts
- [x] 8.2 Test: runner with single-selected ID executes only matching contract
- [x] 8.3 Test: runner with multiple selected IDs executes all matching contracts
- [x] 8.4 Test: runner with non-matching ID skips all contracts (no violations)
- [x] 8.5 Test: configuration check runs regardless of filter
- [x] 8.6 Test: CLI `--contract single` integration test
- [x] 8.7 Test: CLI `--contract a --contract b` integration test
- [x] 8.8 Test: CLI `--contract nonexistent` returns exit code 2 with diagnostic
- [x] 8.9 Test: CLI `--contract` with `--mode audit` integration test

## 9. Tests — Output includes IDs

- [x] 9.1 Test: human output includes `[{id}]` prefix
- [x] 9.2 Test: JSON output includes `contract_id` field
- [x] 9.3 Test: contract with fallback ID shows normalized ID in output
- [x] 9.4 Test: cycle results include contract ID in output

## 10. Documentation

- [x] 10.1 Update YAML reference docs with `id` field on all contract types
- [x] 10.2 Update AI policy-authoring docs/manifest to include `id` guidance
