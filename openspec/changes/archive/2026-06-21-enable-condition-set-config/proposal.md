## Why

.NET and Unity projects use conditional compilation (`#if UNITY_EDITOR`, `#if DEBUG`) to separate runtime, editor, debug, and platform-specific code. Architecture validation must be able to answer different questions depending on which symbols are active: production builds must not depend on editor APIs, editor builds may, debug tooling is audited separately. The tool currently has no awareness of conditional compilation — source analysis parses all `#if` blocks as if no symbols are defined, which produces false positives or misses real violations.

## What Changes

- Add `analysis.condition_sets` to the YAML policy — named collections of preprocessor symbols
- Add `analysis.default_condition_set` — optional default for runs without explicit selection
- Add `--condition-set <name>` CLI option to select a named set
- Pass selected symbols into Roslyn `CSharpParseOptions` during source/method-body scanning
- Restrict symbol-aware analysis to Roslyn source scanning only; reflection/IL scanners are unchanged
- Validate unknown condition set names (exit code 2)
- Update JSON schema to document the new fields
- Update reference docs, sample policies, and AI-facing guidance

## Capabilities

### New Capabilities
- `condition-set-config`: Policy-defined named preprocessor symbol sets for controlling which conditional compilation branches are active during Roslyn source analysis

### Modified Capabilities
*(None — existing spec-level behaviors are unchanged)*

## Impact

- **Core models**: `ArchitectureAnalysisConfiguration` gains `ConditionSets` and `DefaultConditionSet` properties
- **CLI**: New `--condition-set` option; unknown name produces exit code 2
- **Source scanner**: `BuildCompilation` passes symbols to `CSharpParseOptions`
- **Runner**: Accepts `preprocessorSymbols` parameter, passes to source scanner only
- **Testing adapter**: `ArchitectureValidationBuilder` gains `WithConditionSet(name)`
- **Validator**: Gains optional `preprocessorSymbols` parameter
- **Schema/docs**: `dependencies.arch.schema.json`, reference YAML, AI guidance all updated
- No change to reflection-based or IL-based scanners
