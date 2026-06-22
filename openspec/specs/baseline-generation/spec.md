# Baseline Generation Specification

## Purpose
Generates and consumes baseline files that record pre-existing violations so policies can be enforced incrementally going forward.

## Requirements

### Requirement: User can generate a baseline file from current violations

The system SHALL provide a `baseline generate` CLI subcommand that runs validation against the current codebase and writes a baseline file containing `ignored_violations` entries for all current violations not already suppressed by manual ignores.

The generated baseline file SHALL be deterministic — identical output for identical input code, regardless of when or how many times generation is run.

The generated baseline SHALL only contain entries for violations that survive after manual `ignored_violations` in the policy file are applied. Manually ignored violations SHALL NOT appear in the generated baseline.

The baseline file SHALL use the following format:

```yaml
version: 1
baseline:
  <contract-group>:
    - id: "<contract-id>"
      ignored_violations:
        - source_type: "<exact-source-type-fqn>"
          forbidden_reference: "<exact-forbidden-reference-fqn>"
          reason: "generated baseline"
```

Each baseline entry SHALL contain an exact `(source_type, forbidden_reference)` pair — the same values that `ArchitectureIgnoreMatcher.IsIgnored` receives during validation. The baseline SHALL NOT infer glob patterns, namespace-level entries, or generalized patterns.

#### Scenario: Generate baseline for a clean project
- **WHEN** user runs `arch-linter baseline generate --config policy.yml --output baseline.yml` on a project with zero violations
- **THEN** the generated `baseline.yml` SHALL contain `version: 1` and `baseline:` with empty contract groups (no entries)

#### Scenario: Generate baseline captures exact violations
- **WHEN** user runs baseline generation on a project with known dependency violations
- **THEN** each violation SHALL appear as one or more exact `(source_type, forbidden_reference)` entries under the correct contract group and contract ID

#### Scenario: Deterministic output across repeated runs
- **WHEN** user runs baseline generation twice on the same unchanged codebase
- **THEN** both output files SHALL be byte-identical

#### Scenario: Manual ignores are not duplicated in baseline
- **WHEN** user runs baseline generation on a project where some violations are already covered by manual `ignored_violations` in the policy
- **THEN** the baseline SHALL NOT contain entries for those already-ignored violations

#### Scenario: CLI help describes baseline subcommand
- **WHEN** user runs `arch-linter --help` or `arch-linter baseline --help`
- **THEN** output SHALL include usage information for `baseline generate`

### Requirement: User can consume a baseline file during validation

The system SHALL accept a `--baseline` flag on the `validate` subcommand that loads a baseline file and merges its `ignored_violations` entries into the corresponding contracts' ignore lists in memory before running validation.

The merge SHALL identify the target contract by `id` within each contract group (e.g., `baseline.strict[].id` matches `contracts.strict[].Id`).

The merge SHALL deduplicate by `(source_type, forbidden_reference)` pair within each contract — if an identical pair exists both in the baseline and in the policy's manual ignores, it SHALL only appear once.

The merged ignores SHALL participate in all existing validation behavior: matching via `ArchitectureIgnoreMatcher.IsIgnored`, stale tracking via `ArchitectureIgnoreUsageTracker`, and unmatched ignore alerting via `unmatched_ignored_violations` config.

The baseline file SHALL NOT be validated against the main policy schema. It SHALL be loaded via a dedicated `ArchitectureBaselineDocument` model and loader.

#### Scenario: Baseline suppresses existing violations but allows new ones
- **WHEN** user runs `arch-linter validate --config policy.yml --baseline baseline.yml` against code with a baseline on a subset of violations
- **THEN** violations present in the baseline SHALL NOT be reported; violations NOT in the baseline SHALL still fail validation

#### Scenario: Baseline entries go stale when violations are fixed
- **WHEN** user fixes a violation that has a baseline entry, then runs validation
- **THEN** the fixed violation SHALL NOT be reported, and the stale baseline entry SHALL be reported as an unmatched ignored violation (governed by `unmatched_ignored_violations` config)

#### Scenario: Baseline merges with manual ignores without duplicates
- **WHEN** user runs validation with both policy manual ignores and baseline ignores for the same contract
- **THEN** duplicate `(source_type, forbidden_reference)` pairs SHALL only suppress the violation once; the deduplication SHALL NOT affect other entries

#### Scenario: Baseline validation fails with unknown contract ID
- **WHEN** baseline references a `contract_id` that does not exist in the loaded policy document
- **THEN** validation SHALL report an error and exit with a non-zero code, listing the unknown IDs

### Requirement: Baseline entries cover cycle and sibling-cycle contracts

The system SHALL collect baseline candidates for `strict_cycles`, `audit_cycles`, `strict_acyclic_siblings`, and `audit_acyclic_siblings` contracts by recording the `(source_type, forbidden_reference)` pairs at the point where `IsIgnored` is called, before graph edges are aggregated.

Cycle/acyclic-sibling baseline entries SHALL use the same exact `(source_type, forbidden_reference)` format as other contract types.

#### Scenario: Cycle violations are baseline-able
- **WHEN** a cycle exists between types across layers and baseline generation is run
- **THEN** the exact type-level reference pairs that form the cycle edges SHALL appear in the baseline under the appropriate cycle contract

#### Scenario: Cycle baseline suppresses type-level edges
- **WHEN** user applies a cycle baseline and fixes some (but not all) cycle edges
- **THEN** only the unfixed edges SHALL be reported as new cycle violations; the fixed edges SHALL NOT appear as violations

### Requirement: Reason field is configurable

The baseline generator SHALL support an optional `--reason` flag that overrides the default `"generated baseline"` reason value in all generated entries.

Without `--reason`, the generator SHALL use `"generated baseline"` as the default reason.

The reason field SHALL be informational only — it SHALL NOT participate in ignore matching or deduplication.

#### Scenario: Custom reason overrides default
- **WHEN** user runs `arch-linter baseline generate --config policy.yml --output baseline.yml --reason "legacy debt accepted Q2 2026"`
- **THEN** all entries in the generated baseline SHALL have `reason: "legacy debt accepted Q2 2026"` instead of `"generated baseline"`

### Requirement: Baseline generation covers all contract types that support ignored violations

The system SHALL support baseline generation for the following contract groups: `strict`, `audit`, `strict_layers`, `audit_layers`, `strict_allow_only`, `audit_allow_only`, `strict_cycles`, `audit_cycles`, `strict_acyclic_siblings`, `audit_acyclic_siblings`, `strict_method_body`, `audit_method_body`, `strict_independence`, `audit_independence`, `strict_protected`, `audit_protected`, `strict_external`, `audit_external`.

The system SHALL NOT generate baseline entries for `strict_asmdef`, `audit_asmdef`, `strict_layer_templates`, or `audit_layer_templates` (contract types that do not support `ignored_violations`).

#### Scenario: Unsupported contract groups produce no baseline entries
- **WHEN** user runs baseline generation on a project with asmdef contracts
- **THEN** the baseline SHALL NOT contain a `strict_asmdef` or `audit_asmdef` section
