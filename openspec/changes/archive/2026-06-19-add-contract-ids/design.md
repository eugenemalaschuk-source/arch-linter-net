## Context

Contracts currently only have a `name` field — a human-readable string used in violation output and internal matching. There's no stable identifier for CLI selection, CI cross-referencing, or machine addressing. The runner iterates all contracts of a type unconditionally; there's no filtering mechanism.

The change adds an optional `id` field to all 7 contract model types, post-deserialization ID normalization + dedup validation, and a `--contract` filter on the runner and CLI.

## Goals / Non-Goals

**Goals:**
- Optional stable `id` field on every contract type
- Automatic fallback ID from `name` when `id` is omitted
- Duplicate IDs rejected (per contract type group, per mode)
- CLI `--contract <id>` filter (multi-value) for selective execution
- Unknown `--contract` IDs produce a clear error
- Human and JSON output include contract IDs

**Non-Goals:**
- Policy diffing (future concern)
- Contract ownership/reviewer metadata (future concern)
- `--contract` filtering in `ArchitectureValidator.Validate()` (used by self-validation make target; filtering is CLI-only for now)
- Changing the contract execution model (all contracts of a selected type still run; filtering is by ID match)

## Decisions

### D1: Interface over ad-hoc Id property
**Decision**: Add `IArchitectureContract` interface with `string Name` and `string? Id` to all contract types.
**Rationale**: The post-deserialization normalization + dedup logic needs to iterate contracts polymorphically. Without a shared interface, we'd need 7 parallel loops. The interface is lightweight (2 properties, no methods).
**Alternative considered**: Adding `Id` individually to each class without an interface. Rejected because it duplicates the normalization/dedup logic 7 times.

### D2: Eager fallback ID normalization at load time
**Decision**: Compute fallback IDs immediately after deserialization in `ArchitectureContractLoader`, before the document is returned.
**Rationale**: Downstream code (runner, formatter) never needs to handle null IDs — every contract always has an ID. Simplifies filtering and output logic.
**Normalization rule**: `name.ToLowerInvariant().Replace(" ", "-")` plus stripping non-alphanumeric characters (except hyphens). Simple and predictable.

### D3: Per-group, per-type duplicate scope
**Decision**: Duplicate ID validation is scoped to each contract type within each group (e.g., strict dependency contracts checked together, separate from audit dependency contracts).
**Rationale**: A dependency contract and a layer contract don't compete for IDs — they're different concepts. A strict contract and its audit counterpart should be allowed to share an ID since they express the same rule at different enforcement levels.
**Alternative considered**: Global uniqueness across all contracts. Rejected because it would force unnatural ID namespacing (e.g., `strict-dep-map-core` vs `audit-dep-map-core`).

### D4: Filter as `HashSet<string>` on the runner
**Decision**: `ArchitectureContractRunner` accepts an optional `HashSet<string>? SelectedContractIds` via constructor. Every check method checks `IsSelected(contract)` before running.
**Rationale**: Centralized filtering logic. Single `IsSelected` method handles null (no filter → all pass), empty set (no filter → all pass), and active filter (must match).
**Decision**: Configuration checks (`CheckConfiguration`) are NOT filtered — missing assemblies and empty layer namespaces are infrastructure concerns.

### D5: `ArchitectureViolation` gains `ContractId`
**Decision**: `ArchitectureViolation` record adds `string? ContractId` field. Both human and JSON formatters include it when present.
**Rationale**: Every violation is associated with a contract; including the ID enables CI tooling to group/filter violations by contract. The field is nullable primarily for backward compat during transition (though after eager normalization, it should always be populated).

### D6: CLI flag as `--contract <id>` (multi-value)
**Decision**: `--contract` accepts one ID per occurrence. Multiple `--contract` flags are supported: `--contract foo --contract bar`.
**Rationale**: Consistent with standard CLI conventions (e.g., `docker`, `grep -e`). Avoids comma-separated parsing complexity. Unknown IDs produce exit code 2 with a diagnostic listing unknown and valid IDs.

### D7: Output includes ID first
**Decision**: Human output format: `[{id}] source -> forbidden: refs`. JSON output: `contract_id` field added alongside existing `contract`.
**Rationale**: ID is the stable machine identifier; name is the human-readable label. Displaying ID first makes filtering/grep easier. JSON includes both for flexibility.

## Risks / Trade-offs

- **[Risk] Fallback normalization ambiguity**: Two contracts with different names might normalize to the same ID (e.g., "core-a" and "Core A" both → "core-a"). **Mitigation**: Duplicate ID validation catches this at load time. Authors are encouraged to use explicit IDs.
- **[Risk] Interface adds breaking change surface**: Adding `IArchitectureContract` could break external consumers if they implement the interface. **Mitigation**: This is an internal interface in the Core project; no external consumers implement it. Low risk.
- **[Trade-off] Configuration checks unfiltered**: Users running `--contract` still see assembly resolution warnings for assemblies not used by the selected contract. Acceptable because configuration issues are always relevant; the alternative (context-dependent filtering) adds complexity.
- **[Trade-off] CLI-only filtering**: The `ArchitectureValidator` class (used by `make lint-architecture`) doesn't support filtering. This means self-validation always runs all contracts. Acceptable because self-validation is an all-or-nothing check.
