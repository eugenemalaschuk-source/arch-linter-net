# Baseline Generation Specification

## Purpose
Generates and consumes baseline files that record pre-existing violations so policies can be enforced incrementally going forward.
## Requirements
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

The merged ignores SHALL participate in all existing validation behavior: matching via `ArchitectureIgnoreMatcher.IsIgnored`, stale tracking via `ArchitectureIgnoreUsageTracker`, and unmatched ignore alerting via `unmatched_ignored_violations` config. For an ignore entry merged from a `version: 2` baseline, `IsIgnored` SHALL match by full structured-identity equality (contract family, kind, source/target assembly, source/target type and member, and occurrence) against the live candidate identity computed at the same call site — never by `(source_type, forbidden_reference)` text matching. For an entry with no structured identity (a manually authored policy ignore, or one merged from a `version: 1` baseline), `IsIgnored` SHALL continue to match by the legacy glob pair exactly as before. This guarantee applies to `validate --baseline` itself, not only to `baseline diff`/`verify`/`migrate` — two same-named types in different assemblies, or two distinct forbidden calls in the same source type, SHALL be distinguished at validation time.

Occurrence discrimination SHALL be computed live and unconditionally, in deterministic call order, at the same choke point that decides whether a call is ignored — not as a separate pass over only the non-suppressed occurrences — so a baselined occurrence's index matches what generation originally assigned it, whether or not this particular run's `--baseline` merge suppresses it.

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

#### Scenario: validate --baseline distinguishes same-named types in different assemblies
- **WHEN** user runs `validate --baseline` with a `version: 2` baseline entry that baselines a violation from one specific assembly, and the current codebase also contains a same-named violation from a different assembly
- **THEN** the baselined assembly's violation SHALL NOT be reported; the other assembly's same-named violation SHALL still fail validation

#### Scenario: validate --baseline distinguishes multiple occurrences in one type
- **WHEN** user runs `validate --baseline` with a `version: 2` baseline entry that baselines one specific occurrence of a repeated forbidden call within a source type, and the current codebase still contains a second, distinct occurrence of that same call
- **THEN** the baselined occurrence SHALL NOT be reported; the second occurrence SHALL still fail validation

#### Scenario: A version: 2 document whose entries lack structured identity fields is rejected
- **WHEN** a baseline file declares `version: 2` but one or more `ignored_violations` entries are missing `identity_version`, `contract_family`, `kind`, or `occurrence`
- **THEN** loading SHALL fail with an explicit error identifying the offending entry, rather than silently defaulting the missing fields

#### Scenario: A version: 1 document with structured identity fields is rejected
- **WHEN** a baseline file declares `version: 1` but one or more `ignored_violations` entries carry an `identity_version` field
- **THEN** loading SHALL fail with an explicit error, since structured identity fields are only valid in a `version: 2` document

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

The system SHALL support baseline generation for the following contract groups: `strict`, `audit`, `strict_layers`, `audit_layers`, `strict_allow_only`, `audit_allow_only`, `strict_cycles`, `audit_cycles`, `strict_acyclic_siblings`, `audit_acyclic_siblings`, `strict_method_body`, `audit_method_body`, `strict_independence`, `audit_independence`, `strict_protected`, `audit_protected`, `strict_external`, `audit_external`, `strict_coverage`, `audit_coverage`.

The system SHALL NOT generate baseline entries for `strict_asmdef`, `audit_asmdef`, `strict_layer_templates`, or `audit_layer_templates` (contract types that do not support `ignored_violations`).

#### Scenario: Unsupported contract groups produce no baseline entries
- **WHEN** user runs baseline generation on a project with asmdef contracts
- **THEN** the baseline SHALL NOT contain a `strict_asmdef` or `audit_asmdef` section

### Requirement: Baseline generation covers coverage contracts

The system SHALL support baseline generation for the `strict_coverage` and `audit_coverage` contract groups, using the same `id` + `ignored_violations` (`source_type` + `forbidden_reference`) entry format as all other supported contract groups.

Coverage findings from `strict_coverage`/`audit_coverage` contracts (uncovered namespace, unresolved rule reference, empty-input rule reference) SHALL be eligible as baseline candidates the same way ordinary dependency violations are, using the `(source_type, forbidden_reference)` pair already produced for each finding.

#### Scenario: Generate baseline captures uncovered namespaces
- **WHEN** user runs baseline generation on a project with namespaces not covered by any layer or layer template, evaluated against a `strict_coverage` or `audit_coverage` contract
- **THEN** each uncovered namespace SHALL appear as an exact `(source_type, forbidden_reference)` entry under the `strict_coverage` or `audit_coverage` group and the corresponding contract ID

#### Scenario: Generate baseline captures unresolved and empty-input rule references
- **WHEN** user runs baseline generation on a project with a `rule_input`-scoped coverage contract that finds unresolved layer references or layer references with no matching code
- **THEN** each finding SHALL appear as an exact `(source_type, forbidden_reference)` entry under the corresponding `strict_coverage` or `audit_coverage` contract ID

#### Scenario: Coverage baseline generation is deterministic
- **WHEN** user runs baseline generation twice against the same unchanged codebase with coverage contracts configured
- **THEN** the `strict_coverage`/`audit_coverage` sections of both output files SHALL be byte-identical

### Requirement: Coverage gate accepts a baseline of existing uncovered areas

The system SHALL accept `ignored_violations` entries on `strict_coverage` and `audit_coverage` contracts, merged in the same way `--baseline` merges entries for other contract groups, so that coverage findings already present in the baseline are suppressed while new uncovered areas are still reported.

This baseline mechanism SHALL apply only to coverage contract findings. It SHALL NOT suppress, hide, or otherwise interact with ordinary dependency-violation findings from `strict`, `audit`, or any other non-coverage contract group.

#### Scenario: Baseline suppresses previously-accepted uncovered namespaces but flags new ones
- **WHEN** user runs `validate --baseline` against a project where some uncovered namespaces are recorded in the `strict_coverage` baseline and a new namespace becomes uncovered
- **THEN** the namespaces present in the baseline SHALL NOT be reported as coverage failures; the new uncovered namespace SHALL still fail validation

#### Scenario: Coverage baseline does not affect ordinary violations
- **WHEN** user runs `validate --baseline` against a project with both a coverage baseline and ordinary dependency violations not present in any baseline
- **THEN** the ordinary dependency violations SHALL still be reported as failures, unaffected by the coverage baseline entries

#### Scenario: Audit-only coverage baseline does not fail the gate
- **WHEN** an `audit_coverage` contract has uncovered areas recorded in its baseline and the corresponding `audit_coverage` contract is configured as non-blocking per existing audit semantics
- **THEN** validation SHALL report the audit coverage findings without failing the gate, consistent with how `audit` contract groups already behave for ordinary violations

### Requirement: Resolved coverage debt is detected as a stale baseline entry

When a `strict_coverage`/`audit_coverage` baseline entry's `(source_type, forbidden_reference)` pair no longer matches any current coverage finding (the namespace became covered, or the rule reference became resolved), the system SHALL report it as an unmatched ignored violation, using the same `unmatched_ignored_violations` configuration (`error`/`warn`/`off`) already applied to other contract groups.

#### Scenario: Resolved uncovered namespace becomes a stale baseline entry
- **WHEN** a namespace recorded in the `strict_coverage` baseline is later covered by a layer or layer template, then validation is run with `unmatched_ignored_violations: error`
- **THEN** the namespace SHALL NOT be reported as a coverage failure, and the stale baseline entry SHALL be reported as an unmatched ignored violation

#### Scenario: Resolved rule-input coverage debt becomes a stale baseline entry
- **WHEN** a `rule_input`-scoped coverage finding recorded in the baseline (unresolved or empty-input rule reference) is later resolved by adding matching code or a valid layer mapping, then validation is run
- **THEN** the resolved finding SHALL NOT be reported, and the stale baseline entry SHALL be reported as an unmatched ignored violation

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

### Requirement: User can migrate a legacy baseline file to structured identity

The system SHALL provide a `baseline migrate` CLI subcommand that deterministically upgrades a `version: 1` baseline file to `version: 2` by correlating each legacy `ignored_violations` entry against freshly collected current-codebase candidates carrying full `ArchitectureViolationIdentity` data.

Candidate collection for correlation purposes SHALL always cover the full current violation set (equivalent to `--mode all` with no `--contract` restriction), regardless of the `--mode`/`--contract` scope requested for this run, so that classification is never computed against a partial candidate set.

For each legacy entry:
- If the entry's contract group and contract id fall outside the requested `--mode`/`--contract` scope, the system SHALL classify it as `out_of_scope` and carry it through into the migrated output unchanged (uplifted to `version: 2` document shape via a deterministic fallback identity derived from its own contract family/id/source/target text, not reclassified against candidates). An out-of-scope entry SHALL NOT be counted toward `matched`, `stale`, or `ambiguous`, and SHALL NOT block a write due to unrelated ambiguity elsewhere in the file.
- Otherwise, scoped to its contract id, the system SHALL classify it as exactly one of:
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

#### Scenario: Scoped migrate carries out-of-scope entries through unchanged
- **WHEN** user runs `baseline migrate --mode strict` (or `--contract <id>`) against a legacy baseline that also contains audit-group entries (or other contracts') outside that scope
- **THEN** the migrated output SHALL still contain those out-of-scope entries, uplifted to version-2 shape, reported with `status: out_of_scope`, and SHALL NOT report or treat them as `stale`

#### Scenario: Migrate refuses to overwrite the source file
- **WHEN** user runs `baseline migrate --baseline legacy.yml --output legacy.yml`
- **THEN** the command SHALL refuse to run and exit with a non-zero code, reporting that `--output` must differ from `--baseline`

#### Scenario: Real run without --output is refused
- **WHEN** user runs `baseline migrate --baseline legacy.yml` without `--output` and without `--dry-run`/`--check`
- **THEN** the command SHALL refuse to run and exit with a non-zero code, reporting that `--output` is required for a non-dry-run migration

#### Scenario: CLI help describes the migrate subcommand
- **WHEN** user runs `arch-linter baseline --help`
- **THEN** output SHALL include usage information for `baseline migrate`

