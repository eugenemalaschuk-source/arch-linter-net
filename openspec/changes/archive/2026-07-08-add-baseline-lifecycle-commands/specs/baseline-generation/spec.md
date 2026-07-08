## MODIFIED Requirements

### Requirement: User can generate a baseline file from current violations

The system SHALL provide a `baseline generate` CLI subcommand that runs validation against the current codebase and writes a baseline file containing `ignored_violations` entries for all current violations not already suppressed by manual ignores.

The generated baseline file SHALL be deterministic — identical output for identical input code, regardless of when or how many times generation is run.

The generated baseline SHALL only contain entries for violations that survive after manual `ignored_violations` in the policy file are applied. Manually ignored violations SHALL NOT appear in the generated baseline.

The `baseline generate` subcommand SHALL accept an optional `--contract <id>` flag, repeatable, that scopes generation to only the named contract id(s). Without `--contract`, generation SHALL cover all contracts in the selected mode, as today.

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
- **THEN** output SHALL include usage information for `baseline generate`, `baseline update`, `baseline prune`, `baseline diff`, and `baseline verify`

#### Scenario: Selected-contract generation scopes output
- **WHEN** user runs `arch-linter baseline generate --config policy.yml --output baseline.yml --contract app-boundaries` on a project with violations in multiple contracts
- **THEN** the generated baseline SHALL only contain entries for the `app-boundaries` contract id, even if other contracts also have current violations

## ADDED Requirements

### Requirement: User can update a baseline from current violations while preserving existing entries

The system SHALL provide a `baseline update` CLI subcommand that reads an existing baseline file and the current codebase's violations, and writes a new baseline that:
- retains, unchanged, every existing baseline entry whose `(contract id, source_type, forbidden_reference)` still matches a current violation, including its original `reason` text verbatim;
- adds new entries, deterministically, for current violations that have no matching existing baseline entry, using the default reason (`"generated baseline"`) or the `--reason` override for new entries only;
- leaves entries with no matching current violation (resolved debt) and entries referencing unknown contract ids (configuration errors) untouched in the output — `update` SHALL NOT remove them.

`baseline update` SHALL accept `--policy`/`--config`, `--baseline` (existing baseline file to update), `--output`, `--mode` (strict/audit/all), `--condition-set`, `--contract` (repeatable), and `--reason`, consistent with `baseline generate`.

#### Scenario: Update preserves reason on still-valid entries
- **WHEN** user runs `baseline update` against a baseline containing an entry with a custom `reason` whose violation is still present in the current codebase
- **THEN** the updated baseline SHALL contain that entry with the identical `reason` text, unchanged

#### Scenario: Update adds new violations deterministically
- **WHEN** user runs `baseline update` against a baseline and the current codebase has a new violation not present in the baseline
- **THEN** the updated baseline SHALL contain a new entry for that violation using the default or `--reason` text

#### Scenario: Update does not remove stale entries
- **WHEN** user runs `baseline update` against a baseline containing an entry whose violation has been fixed in the current codebase
- **THEN** the updated baseline SHALL still contain that entry unchanged (removal is handled by `baseline prune`, not `baseline update`)

### Requirement: User can prune stale entries from a baseline

The system SHALL provide a `baseline prune` CLI subcommand that reads an existing baseline file and the current codebase's violations, removes:
- entries whose `(contract id, source_type, forbidden_reference)` no longer matches any current violation (resolved debt), and
- entries whose contract id does not exist in the current policy (configuration error),

writes the pruned baseline to `--output`, and reports the list of removed entries, each tagged with its removal reason (`resolved` or `configuration-error`), in both human-readable and `--json` output.

`baseline prune` SHALL NOT add entries for new violations — pruning only removes.

`baseline prune` SHALL accept `--policy`/`--config`, `--baseline`, `--output`, `--mode`, `--condition-set`, and `--contract`, consistent with `baseline generate`.

#### Scenario: Prune removes resolved debt and reports it
- **WHEN** user runs `baseline prune` against a baseline containing an entry whose violation no longer exists in the current codebase
- **THEN** the pruned baseline SHALL NOT contain that entry, and the command output SHALL list it as removed with reason `resolved`

#### Scenario: Prune removes entries with unknown contract ids and reports it
- **WHEN** user runs `baseline prune` against a baseline containing an entry whose contract id does not exist in the current policy
- **THEN** the pruned baseline SHALL NOT contain that entry, and the command output SHALL list it as removed with reason `configuration-error`

#### Scenario: Prune leaves frozen entries untouched
- **WHEN** user runs `baseline prune` against a baseline where every entry still matches a current violation
- **THEN** the pruned baseline SHALL be identical to the input baseline, and no entries SHALL be reported as removed

### Requirement: User can diff a baseline against current violations

The system SHALL provide a `baseline diff` CLI subcommand that compares an existing baseline file against the current codebase's violations without writing any file, and reports each violation/entry in exactly one of the following categories:
- **new debt**: a current violation with no matching baseline entry;
- **existing (frozen) debt**: a baseline entry that still matches a current violation;
- **resolved debt**: a baseline entry with no matching current violation, whose contract id is known;
- **configuration error**: a baseline entry whose contract id does not exist in the current policy.

`baseline diff` SHALL accept `--policy`/`--config`, `--baseline`, `--mode`, `--condition-set`, `--contract`, and `--json`, consistent with other baseline subcommands. `baseline diff` SHALL exit with code 0 when the comparison completes successfully, regardless of category counts (it is a report, not a gate).

#### Scenario: Diff reports all four categories
- **WHEN** user runs `baseline diff` against a baseline and codebase containing new debt, existing frozen debt, resolved debt, and a configuration error
- **THEN** the output SHALL list all four categories with their respective entries, and SHALL exit with code 0

#### Scenario: Diff on a fully synchronized baseline reports no drift
- **WHEN** user runs `baseline diff` against a baseline where every entry matches a current violation and every current violation has a baseline entry
- **THEN** the output SHALL report zero new, zero resolved, and zero configuration-error entries, and SHALL exit with code 0

### Requirement: User can verify a baseline is synchronized with current validation results

The system SHALL provide a `baseline verify` CLI subcommand that performs the same comparison as `baseline diff`, without writing any file, and exits with a non-zero code if any resolved-debt or configuration-error entries are found (the baseline is out of sync), and exits 0 otherwise. `baseline verify` SHALL NOT fail due to new debt — new, unbaselined violations are the concern of `validate`, not `baseline verify`.

`baseline verify` SHALL accept `--policy`/`--config`, `--baseline`, `--mode`, `--condition-set`, `--contract`, and `--json`, consistent with other baseline subcommands.

#### Scenario: Verify passes when baseline is in sync
- **WHEN** user runs `baseline verify` against a baseline where every entry still matches a current violation
- **THEN** the command SHALL exit with code 0

#### Scenario: Verify fails when baseline contains resolved debt
- **WHEN** user runs `baseline verify` against a baseline containing at least one entry whose violation has been fixed
- **THEN** the command SHALL exit with a non-zero code

#### Scenario: Verify fails when baseline references an unknown contract id
- **WHEN** user runs `baseline verify` against a baseline containing an entry whose contract id does not exist in the current policy
- **THEN** the command SHALL exit with a non-zero code

#### Scenario: Verify does not fail on new, unbaselined debt
- **WHEN** user runs `baseline verify` against a baseline that is otherwise fully in sync, but the current codebase has a new violation not present in the baseline
- **THEN** the command SHALL exit with code 0
