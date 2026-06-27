## Why

A green architecture validation run can hide a contract that no longer checks anything: a typo'd layer name, a namespace that was renamed or deleted, or a source/target scope that silently resolved to zero first-party code. Today these "empty-input" contracts pass silently — they produce zero violations, indistinguishable from a contract that is correctly enforcing a real boundary. The `architecture-coverage-model` design (#96) already reserved `scope: rule_input` for exactly this gap; #97 built the shared inventory and #129 shipped the first scope (`namespace`). This change implements the second scope so policy authors can detect stale or empty rule inputs explicitly, with an opt-in escape hatch for intentionally empty scopes.

## What Changes

- Implement `scope: rule_input` for `strict_coverage`/`audit_coverage` contracts: given a `contract_ids` list, classify each referenced contract's declared source/target/layer references against the shared `ArchitectureCoverageInventory`.
- Add two distinct findings per referenced contract:
  - `unresolved` — the contract references a layer name that is not declared in `layers` at all (dangling/typo reference).
  - `empty-input` — the contract's layer pattern(s) resolve, but currently match zero first-party namespaces in the discovered codebase (covers both "always empty" and "went stale after a rename/deletion" — both are the same current-state signal).
- Extend `ArchitectureCoverageExclusion` with an optional `contract_id` matcher so a `rule_input` coverage contract can declare a referenced contract is intentionally allowed to be empty, with a mandatory `reason`.
- Update loader validation (`ArchitectureContractLoader.ValidateCoverageNamespaces` and friends) to accept `scope: rule_input`: require a non-empty `contract_ids` list, and reject `roots`/`between` on a `rule_input` contract (mirroring the existing `namespace` scope's cross-field rejection).
- Update `ArchitectureRunnerFactory`'s unsupported-scope guard so only `project`, `assembly`, and `dependency_edge` remain rejected as reserved; `namespace` and `rule_input` are both implemented.
- Update `docs/contracts/coverage.md`, `docs/reference/yaml-schema.md`, and `schema/dependencies.arch.schema.json` additively for the now-implemented `contract_ids` field and the new exclusion `contract_id` field.

## Capabilities

### New Capabilities
- `rule-input-coverage-contracts`: Detect coverage contracts with `scope: rule_input` whose referenced contracts (`contract_ids`) have dangling layer references (`unresolved`) or layer patterns that currently match no first-party code (`empty-input`), with an explicit-reason exclusion escape hatch, following the same strict/audit severity model as `namespace-coverage-contracts`.

### Modified Capabilities
(none — `architecture-coverage-model` already specified this scope's YAML shape; this change only removes the "not yet implemented" gap and does not change any reviewed requirement.)

## Impact

- `src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs`: add optional `contract_id` to `ArchitectureCoverageExclusion` (additive).
- `src/ArchLinterNet.Core/Contracts/ArchitectureContractLoader.cs`: extend `ValidateCoverageNamespaces`-style validation to handle `scope: rule_input` cross-field rules.
- `src/ArchLinterNet.Core/Execution/ArchitectureContractRunner.Coverage.cs`: add a `rule_input` branch to `CheckCoverageContract`, reusing `ArchitectureViolation` and the existing `GetReferencedLayerNames`-style field extraction.
- `src/ArchLinterNet.Core/Execution/ArchitectureRunnerFactory.cs`: narrow the unsupported-scope list to `project`/`assembly`/`dependency_edge`.
- `tests/ArchLinterNet.Core.Tests/`: new fixtures and tests for non-empty, empty-source, empty-target, unresolved, intentionally-empty, and audit-only cases; update `CoverageContractReservedTests.cs` so `rule_input` is no longer asserted as unsupported.
- `docs/contracts/coverage.md`, `docs/reference/yaml-schema.md`, `schema/dependencies.arch.schema.json`: additive documentation/schema updates.
- No changes to policies that declare no `rule_input` coverage contracts.
