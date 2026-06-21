## ADDED Requirements

### Requirement: Policy defines named preprocessor symbol sets

The policy SHALL support an `analysis.condition_sets` dictionary mapping named condition sets to lists of preprocessor symbol strings, and an optional `analysis.default_condition_set` naming the set to use when no explicit selection is provided.

#### Scenario: Named condition set defined in policy
- **WHEN** the policy declares `analysis.condition_sets` with `runtime: []` and `editor: [UNITY_EDITOR]`
- **THEN** the document SHALL contain both entries in the `ConditionSets` dictionary

#### Scenario: Default condition set
- **WHEN** the policy declares `analysis.default_condition_set: editor`
- **THEN** runs without explicit `--condition-set` SHALL resolve to the `editor` symbol set

#### Scenario: No condition configuration
- **WHEN** the policy omits both `condition_sets` and `default_condition_set`
- **THEN** the analysis SHALL use an empty symbol list (backward-compatible default)

### Requirement: CLI selects condition set by name

The CLI SHALL accept a `--condition-set <name>` option that selects a named condition set from the policy. Resolution order: CLI argument, then `analysis.default_condition_set`, then empty list.

#### Scenario: Explicit selection
- **WHEN** the user runs `--condition-set editor`
- **THEN** the analysis SHALL use symbols from `analysis.condition_sets.editor`

#### Scenario: Default fallback
- **WHEN** the user omits `--condition-set` and the policy has `analysis.default_condition_set: runtime`
- **THEN** the analysis SHALL use symbols from `analysis.condition_sets.runtime`

#### Scenario: Unknown condition set
- **WHEN** the user specifies `--condition-set nonexistent`
- **THEN** the tool SHALL exit with code 2 and print available condition set names

#### Scenario: Unknown default condition set
- **WHEN** `analysis.default_condition_set: missing` and `analysis.condition_sets` has no `missing`
- **THEN** the tool SHALL exit with code 2

### Requirement: Roslyn source scanner respects condition set symbols

The Roslyn source scanner SHALL pass the resolved preprocessor symbols to `CSharpParseOptions` when building the compilation, so that `#if`/`#elif`/`#else` branches are parsed according to the active symbol set.

#### Scenario: Runtime symbols exclude editor blocks
- **WHEN** condition set `runtime` has symbols `[]` and a source file contains `#if UNITY_EDITOR` with a forbidden call
- **THEN** the forbidden call SHALL NOT be reported as a violation

#### Scenario: Editor symbols include editor blocks
- **WHEN** condition set `editor` has symbols `[UNITY_EDITOR]` and a source file contains `#if UNITY_EDITOR` with a forbidden call
- **THEN** the forbidden call SHALL be reported as a violation

#### Scenario: Negation flips correctly
- **WHEN** condition set `runtime` has symbols `[]` and a source file contains `#if !UNITY_EDITOR` with a forbidden call
- **THEN** the forbidden call SHALL be reported as a violation

#### Scenario: Multiple symbols
- **WHEN** condition set `debug_editor` has symbols `[UNITY_EDITOR, DEBUG]` and a source file contains `#if DEBUG` with a forbidden call
- **THEN** the forbidden call SHALL be reported as a violation

### Requirement: Reflection and IL scanners are unaffected

Condition set symbols SHALL NOT affect reflection-based type scanning or IL-based method body scanning. Those scanners continue to analyze the assemblies provided to the run as-compiled.

#### Scenario: IL scanner ignores condition sets
- **WHEN** a condition set `editor` defines `[UNITY_EDITOR]` and the IL scanner runs against a compiled assembly
- **THEN** the IL scanner SHALL NOT receive or process the condition set symbols; it SHALL scan the compiled IL as-is

### Requirement: Diagnostics are deterministic

Under the same condition set, the same source files and assemblies SHALL produce the same violations regardless of environment or run sequence.

#### Scenario: Deterministic across runs
- **WHEN** a policy, source files, and condition set remain unchanged
- **THEN** two consecutive runs SHALL produce identical violation sets

### Requirement: Schema and documentation are updated

The JSON schema, reference YAML, examples, and AI-facing guidance SHALL document the `condition_sets` and `default_condition_set` fields and the `--condition-set` CLI option.

#### Scenario: Schema validates condition_set fields
- **WHEN** a policy file includes `analysis.condition_sets` with valid entries
- **THEN** the JSON schema SHALL accept it

#### Scenario: Schema rejects unknown condition_set type
- **WHEN** `analysis.condition_sets` contains a non-object value
- **THEN** the JSON schema SHALL reject it
