## Why

As architecture policies grow beyond a handful of contracts, developers and AI agents need to run only the contract they're actively fixing — not the entire policy on every iteration. Stable contract IDs make this possible, enable precise CI/debug output references, and lay groundwork for future features like policy diffing and contract ownership.

## What Changes

- **New optional `id` field** on all 7 contract types (dependency, layer, allow-only, cycle, method-body, asmdef, independence)
- **Fallback ID normalization**: when `id` is omitted, derive from `name` (lowercase, spaces→hyphens)
- **Duplicate ID validation**: contracts with duplicate IDs in the same group fail policy loading
- **CLI `--contract` flag**: multi-value flag to select contracts by ID; unknown IDs produce a clear diagnostic
- **Selective execution**: only selected contracts run in both strict and audit modes; configuration checks always run
- **Output enrichment**: human and JSON output include contract IDs
- **JSON Schema updated**: `id` as optional field on all contract `$defs`

## Capabilities

### New Capabilities
- `contract-id-selection`: stable contract IDs, fallback normalization, duplicate detection, selective execution via `--contract`, and enriched output

### Modified Capabilities
- `yaml-contract-loading`: deserialization must now accept and validate the optional `id` field; duplicate IDs cause loading failure
- `cli-validation`: new `--contract` flag with multi-value and unknown-ID-diagnostic behavior
- `violation-reporting`: human and JSON output must include contract ID
- `dependency-contracts`: contract model gains optional `id`
- `layer-contracts`: contract model gains optional `id`
- `allow-only-contracts`: contract model gains optional `id`
- `cycle-contracts`: contract model gains optional `id`
- `method-body-contracts`: contract model gains optional `id`
- `asmdef-contracts`: contract model gains optional `id`
- `independence-contracts`: contract model gains optional `id`

## Impact

- **Model**: all 7 contract classes in `ArchitectureContractModels.cs` gain `string? Id`
- **Loading**: `ArchitectureContractLoader` adds post-deserialization ID normalization + duplicate validation
- **Runner**: `ArchitectureContractRunner` accepts optional `HashSet<string>` contract filter; all check methods filter by ID
- **CLI**: `Program.cs` adds `--contract` argument parsing; passes filter to runner
- **Output**: `ArchitectureViolation` record gains `ContractId`; both human and JSON formatters updated
- **Schema**: `schema/dependencies.arch.schema.json` adds `id` to each contract `$def`
- **Docs**: YAML reference + AI policy-authoring docs updated
- **Tests**: ~8 new test areas across model, loading, runner, CLI, and output
