## MODIFIED Requirements

### Requirement: User can generate a baseline file from current violations

The system SHALL provide a `baseline generate` CLI subcommand that runs validation against the current codebase and writes a baseline file containing `ignored_violations` entries for all current violations not already suppressed by manual ignores.

The generated baseline file SHALL be deterministic — identical output for identical input code, regardless of when or how many times generation is run.

The generated baseline SHALL only contain entries for violations that survive after manual `ignored_violations` in the policy file are applied. Manually ignored violations SHALL NOT appear in the generated baseline.

The `baseline generate` subcommand SHALL accept an optional `--contract <id>` flag, repeatable, that scopes generation to only the named contract id(s). Without `--contract`, generation SHALL cover all contracts in the selected mode, as today.

Newly generated baseline files SHALL use format version `2`, with each `ignored_violations` entry carrying a structured `ArchitectureViolationIdentity` (contract family, kind, source/target assembly, source/target type, source/target member, and an occurrence discriminator) in addition to human-readable `source_type`/`forbidden_reference` display fields and `reason`:

```yaml
version: 2
baseline:
  <contract-group>:
    - id: "<contract-id>"
      ignored_violations:
        - identity_version: 2
          contract_family: "<family>"
          kind: "<dependency|reference|call|package|framework|api_change|coverage>"
          source_assembly: "<assembly-name-or-null>"
          source_type: "<exact-source-type-fqn>"
          source_member: "<member-or-null>"
          target_assembly: "<assembly-name-or-null>"
          target_type: "<target-type-fqn-or-null>"
          target_member: "<exact-forbidden-symbol-or-null>"
          occurrence: 0
          forbidden_reference: "<exact-forbidden-reference-fqn>"
          reason: "generated baseline"
```

For contract families whose checks are qualified with assembly/member information (dependency-style and method-body/call contracts), `source_assembly`, `target_assembly`, and — for method-body/call contracts — `target_member` and `occurrence` SHALL be populated from the actual scanned symbols, not left null. For families not yet qualified with assembly/member data, those fields SHALL be `null` and matching SHALL fall back to `(contract family, contract id, source_type, target_type)` — this is strictly no less precise than the pre-existing `(source_type, forbidden_reference)` behavior for those families.

One baseline entry SHALL suppress exactly one `ArchitectureViolationIdentity`. Multiple distinct occurrences that previously collapsed into a single generated entry (because their legacy `(source_type, forbidden_reference)` strings were identical) SHALL now each produce their own entry, distinguished by the `occurrence` discriminator.

Display messages (including any embedded source line number) SHALL NOT be used as identity; identity SHALL be composed only of the structured fields above.

#### Scenario: Generate baseline for a clean project
- **WHEN** user runs `arch-linter baseline generate --config policy.yml --output baseline.yml` on a project with zero violations
- **THEN** the generated `baseline.yml` SHALL contain `version: 2` and `baseline:` with empty contract groups (no entries)

#### Scenario: Generate baseline captures exact violations
- **WHEN** user runs baseline generation on a project with known dependency violations
- **THEN** each violation SHALL appear as one or more baseline entries under the correct contract group and contract ID, each with a structured `ArchitectureViolationIdentity`

#### Scenario: Deterministic output across repeated runs
- **WHEN** user runs baseline generation twice on the same unchanged codebase
- **THEN** both output files SHALL be byte-identical, including `occurrence` discriminators

#### Scenario: Manual ignores are not duplicated in baseline
- **WHEN** user runs baseline generation on a project where some violations are already covered by manual `ignored_violations` in the policy
- **THEN** the baseline SHALL NOT contain entries for those already-ignored violations

#### Scenario: CLI help describes baseline subcommand
- **WHEN** user runs `arch-linter --help` or `arch-linter baseline --help`
- **THEN** output SHALL include usage information for `baseline generate`, `baseline update`, `baseline prune`, `baseline diff`, `baseline verify`, and `baseline migrate`

#### Scenario: Selected-contract generation scopes output
- **WHEN** user runs `arch-linter baseline generate --config policy.yml --output baseline.yml --contract app-boundaries` on a project with violations in multiple contracts
- **THEN** the generated baseline SHALL only contain entries for the `app-boundaries` contract id, even if other contracts also have current violations

#### Scenario: Same-named types in different assemblies do not collide
- **WHEN** two different assemblies each contain a violating type with the same simple name and namespace (e.g. two `Program` types), and baseline generation is run then one occurrence is baselined
- **THEN** the baseline entry SHALL suppress only the violation from its own `source_assembly`; the same-named violation in the other assembly SHALL still be reported as new debt by `validate --baseline`

#### Scenario: Multiple forbidden calls in one type each get a distinct entry
- **WHEN** a single source type contains multiple distinct forbidden-call occurrences to the same target member, and baseline generation is run then only the first occurrence's entry is baselined
- **THEN** the additional occurrences SHALL still be reported as new debt; baselining one occurrence SHALL NOT suppress the others

### Requirement: User can consume a baseline file during validation

The system SHALL accept a `--baseline` flag on the `validate` subcommand that loads a baseline file and merges its `ignored_violations` entries into the corresponding contracts' ignore lists in memory before running validation.

The merge SHALL identify the target contract by `id` within each contract group (e.g., `baseline.strict[].id` matches `contracts.strict[].Id`).

For `version: 1` baseline files, the merge SHALL deduplicate by the legacy `(source_type, forbidden_reference)` pair within each contract, exactly as before — this behavior SHALL NOT change for existing v1 files.

For `version: 2` baseline files, the merge SHALL deduplicate by full `ArchitectureViolationIdentity` structural equality within each contract.

The merged ignores SHALL participate in all existing validation behavior: matching via `ArchitectureIgnoreMatcher.IsIgnored`, stale tracking via `ArchitectureIgnoreUsageTracker`, and unmatched ignore alerting via `unmatched_ignored_violations` config.

The baseline file SHALL NOT be validated against the main policy schema. It SHALL be loaded via a dedicated `ArchitectureBaselineDocument` model and loader that dispatches on `version` (`1` or `2`); any other value SHALL fail loading with an explicit unsupported-version error.

#### Scenario: Baseline suppresses existing violations but allows new ones
- **WHEN** user runs `arch-linter validate --config policy.yml --baseline baseline.yml` against code with a baseline on a subset of violations
- **THEN** violations present in the baseline SHALL NOT be reported; violations NOT in the baseline SHALL still fail validation

#### Scenario: Baseline entries go stale when violations are fixed
- **WHEN** user fixes a violation that has a baseline entry, then runs validation
- **THEN** the fixed violation SHALL NOT be reported, and the stale baseline entry SHALL be reported as an unmatched ignored violation (governed by `unmatched_ignored_violations` config)

#### Scenario: Baseline merges with manual ignores without duplicates
- **WHEN** user runs validation with both policy manual ignores and baseline ignores for the same contract
- **THEN** duplicate identities SHALL only suppress the violation once; the deduplication SHALL NOT affect other entries

#### Scenario: Baseline validation fails with unknown contract ID
- **WHEN** baseline references a `contract_id` that does not exist in the loaded policy document
- **THEN** validation SHALL report an error and exit with a non-zero code, listing the unknown IDs

#### Scenario: Legacy version 1 baseline files load and match unchanged
- **WHEN** user runs `validate --baseline` with an existing `version: 1` baseline file that has not been migrated
- **THEN** the file SHALL load successfully and match violations using the exact legacy `(source_type, forbidden_reference)` pair semantics, with no reinterpretation of its entries

#### Scenario: Unsupported baseline version is rejected
- **WHEN** user runs any `baseline` subcommand or `validate --baseline` against a file whose `version` is neither `1` nor `2`
- **THEN** the command SHALL fail with an explicit unsupported-version error and a non-zero exit code

### Requirement: User can diff a baseline against current violations

The system SHALL provide a `baseline diff` CLI subcommand that compares an existing baseline file against the current codebase's violations without writing any file, and reports each violation/entry with an explicit structured `status` of exactly one of:
- `new`: a current violation with no matching baseline entry;
- `matched`: a baseline entry that still matches a current violation (previously "existing/frozen debt");
- `stale`: a baseline entry with no matching current violation, whose contract id is known (previously "resolved debt");
- a baseline entry whose contract id does not exist in the current policy remains reported as a configuration error.

The `status` field SHALL be present, using these exact values, in `--json` output, so consumers can branch on `status` without parsing display text. Human-readable output SHALL continue to group entries under labeled sections corresponding to each status. (Baseline comparison does not currently produce SARIF output or a dedicated Testing API surface — this requirement applies to the CLI human/JSON output that exists today. Extending SARIF/Testing API to baseline comparison results is out of scope for this change.)

`baseline diff` SHALL accept `--policy`/`--config`, `--baseline`, `--mode`, `--condition-set`, `--contract`, and `--json`, consistent with other baseline subcommands. `baseline diff` SHALL exit with code 0 when the comparison completes successfully, regardless of category counts (it is a report, not a gate).

#### Scenario: Diff reports all four categories
- **WHEN** user runs `baseline diff` against a baseline and codebase containing new debt, matched debt, stale debt, and a configuration error
- **THEN** the output SHALL list all categories with their respective entries and an explicit `status` field per entry (`new`/`matched`/`stale`, plus configuration errors reported separately), and SHALL exit with code 0

#### Scenario: Diff on a fully synchronized baseline reports no drift
- **WHEN** user runs `baseline diff` against a baseline where every entry matches a current violation and every current violation has a baseline entry
- **THEN** the output SHALL report zero `new`, zero `stale`, and zero configuration-error entries, and SHALL exit with code 0

### Requirement: User can verify a baseline is synchronized with current validation results

The system SHALL provide a `baseline verify` CLI subcommand that performs the same comparison as `baseline diff`, without writing any file, and exits with a non-zero code if any `stale` or configuration-error entries are found (the baseline is out of sync), and exits 0 otherwise. `baseline verify` SHALL NOT fail due to `new` debt — new, unbaselined violations are the concern of `validate`, not `baseline verify`.

`baseline verify` SHALL accept `--policy`/`--config`, `--baseline`, `--mode`, `--condition-set`, `--contract`, and `--json`, consistent with other baseline subcommands. Its `--json` output SHALL include the same structured `status` field per entry as `baseline diff`.

#### Scenario: Verify passes when baseline is in sync
- **WHEN** user runs `baseline verify` against a baseline where every entry still matches a current violation
- **THEN** the command SHALL exit with code 0

#### Scenario: Verify fails when baseline contains resolved debt
- **WHEN** user runs `baseline verify` against a baseline containing at least one entry whose violation has been fixed
- **THEN** the command SHALL exit with a non-zero code, and the entry SHALL be reported with `status: stale`

#### Scenario: Verify fails when baseline references an unknown contract id
- **WHEN** user runs `baseline verify` against a baseline containing an entry whose contract id does not exist in the current policy
- **THEN** the command SHALL exit with a non-zero code

#### Scenario: Verify does not fail on new, unbaselined debt
- **WHEN** user runs `baseline verify` against a baseline that is otherwise fully in sync, but the current codebase has a new violation not present in the baseline
- **THEN** the command SHALL exit with code 0

## ADDED Requirements

### Requirement: User can migrate a legacy baseline file to structured identity

The system SHALL provide a `baseline migrate` CLI subcommand that deterministically upgrades a `version: 1` baseline file to `version: 2` by correlating each legacy `ignored_violations` entry against freshly collected current-codebase candidates carrying full `ArchitectureViolationIdentity` data.

For each legacy entry, scoped to its contract id, the system SHALL classify it as exactly one of:
- `matched`: exactly one current candidate's legacy-projected `(source_type, forbidden_reference)` pair equals the entry's pair — the entry SHALL be rewritten using that candidate's full structured identity, with `reason` and any issue metadata preserved verbatim;
- `stale`: zero current candidates match — the entry SHALL be excluded from the migrated output and reported;
- `ambiguous`: more than one current candidate matches — the entry SHALL be excluded from the migrated output and reported; migration SHALL NOT guess or silently broaden the entry to cover multiple identities.

`baseline migrate` SHALL accept `--policy`/`--config`, `--baseline` (required, the legacy file to migrate), `--output` (the destination path for the migrated file), `--mode`, `--condition-set`, `--contract`, `--dry-run`/`--check` (aliases for a report-only run), and `--json`.

`baseline migrate` SHALL NOT write to a path equal to the resolved `--baseline` input path under any circumstance.

Without `--dry-run`/`--check`, `baseline migrate` SHALL require `--output` to be provided and SHALL refuse to run without it. If any entries classify as `ambiguous`, a non-dry-run run SHALL NOT write the output file and SHALL exit with a non-zero code, reporting every ambiguous entry so it can be resolved manually.

`--dry-run`/`--check` SHALL perform classification and reporting only, writing no file, and SHALL exit with a non-zero code if any entries classify as `ambiguous` (so it can be used as a CI gate), and exit 0 otherwise regardless of `stale` count.

#### Scenario: Migrate rewrites an unambiguous legacy entry with full identity
- **WHEN** user runs `baseline migrate --baseline legacy.yml --output migrated.yml` against a legacy baseline entry that matches exactly one current violation candidate
- **THEN** `migrated.yml` SHALL contain `version: 2` with that entry rewritten to the candidate's full `ArchitectureViolationIdentity`, and its original `reason` preserved verbatim

#### Scenario: Migrate reports stale entries and excludes them
- **WHEN** a legacy baseline entry's `(source_type, forbidden_reference)` pair matches zero current violation candidates
- **THEN** the migration report SHALL list that entry with `status: stale`, and the migrated output (if written) SHALL NOT contain it

#### Scenario: Migrate fails closed on ambiguous entries
- **WHEN** a legacy baseline entry's `(source_type, forbidden_reference)` pair matches more than one current violation candidate
- **THEN** a non-dry-run `baseline migrate` run SHALL exit with a non-zero code, SHALL NOT write the `--output` file, and SHALL list every ambiguous entry with `status: ambiguous` in its report

#### Scenario: Dry-run reports without writing
- **WHEN** user runs `baseline migrate --baseline legacy.yml --dry-run`
- **THEN** no file SHALL be written, the command SHALL report the classification (`matched`/`stale`/`ambiguous`) of every entry, and SHALL exit non-zero only if any entry is `ambiguous`

#### Scenario: Migrate refuses to overwrite the source file
- **WHEN** user runs `baseline migrate --baseline legacy.yml --output legacy.yml`
- **THEN** the command SHALL refuse to run and exit with a non-zero code, reporting that `--output` must differ from `--baseline`

#### Scenario: Real run without --output is refused
- **WHEN** user runs `baseline migrate --baseline legacy.yml` without `--output` and without `--dry-run`/`--check`
- **THEN** the command SHALL refuse to run and exit with a non-zero code, reporting that `--output` is required for a non-dry-run migration

#### Scenario: CLI help describes the migrate subcommand
- **WHEN** user runs `arch-linter baseline --help`
- **THEN** output SHALL include usage information for `baseline migrate`
