## Why

Adopting ArchLinterNet in existing repositories with accumulated architecture debt requires freezing current violations while keeping new violations visible. Hand-authoring `ignored_violations` entries for every existing violation is error-prone, non-deterministic, and blocks AI-assisted adoption. Automatic baseline generation solves this by capturing current violations into a machine-generated baseline file that the validation pipeline consumes alongside the main policy.

## What Changes

- Add `baseline generate` CLI subcommand that runs validation and writes current violations as a baseline file
- Add `--baseline` flag to `validate` subcommand that merges baseline ignore entries into contract validation in-memory
- Introduce `ArchitectureBaselineDocument` model and loader for the baseline file format
- Introduce baseline candidate collection in the contract runner — collects (source_type, forbidden_reference) pairs at every `IsIgnored` call site for pairs that were NOT suppressed by manual ignores
- Add baseline entries to existing contract `IgnoredViolations` lists at merge time, reusing existing ignore matching, stale tracking, and unmatched alerting unchanged
- Generate deterministic output (sorted, stable across unchanged runs, no timestamp)
- Update JSON schema, documentation, and AI-facing guidance

## Capabilities

### New Capabilities
- `baseline-generation`: Generate a baseline file from current validation results, and consume baseline entries during validation via in-memory merge

### Modified Capabilities

None. Baseline generation is additive — it does not change requirements of existing capabilities.

## Impact

- **Core**: New model `ArchitectureBaselineDocument`, new loader, baseline candidate collector in `ArchitectureContractRunner`
- **CLI**: New `baseline generate` subcommand, `--baseline` flag on `validate`
- **Schema**: New baseline JSON schema file (separate from policy schema)
- **Testing**: New tests for baseline generation, baseline merge, deterministic output, cycle coverage
- **Docs**: New migration guide and CLI reference entries
