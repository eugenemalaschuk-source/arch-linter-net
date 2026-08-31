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
        - identity_version: 1
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

#### Scenario: Baseline entries are resolved when violations are fixed
- **WHEN** user fixes a violation that has a baseline entry, then runs validation
- **THEN** the fixed violation SHALL NOT be reported, and the resolved baseline entry SHALL be reported as an unmatched ignored violation (governed by `unmatched_ignored_violations` config)

#### Scenario: Baseline merges with manual ignores without duplicates
- **WHEN** user runs validation with both policy manual ignores and baseline ignores for the same contract
- **THEN** duplicate identities SHALL only suppress the violation once; the deduplication SHALL NOT affect other entries

#### Scenario: Baseline validation fails with unknown contract ID
- **WHEN** baseline references a `contract_id` that does not exist in the loaded policy document
- **THEN** validation SHALL report an error and exit with a non-zero code, listing the unknown IDs; baseline lifecycle commands SHALL classify the entry as `stale`

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

`baseline generate` and `baseline update` SHALL additionally accept repeatable `--reason-for-contract <contract-id>=<text>` and `--reason-for-family <family>=<text>` options that map a reason to newly added entries by contract id and by contract family respectively, where the family is the contract group name with its `strict_`/`audit_` prefix removed (for example `package_dependency`, `composition`).

For each newly added entry, the reason SHALL be resolved in this order: a `--reason-for-contract` value for the entry's contract id, then a `--reason-for-family` value for the entry's contract family, then `--reason`, then the built-in default.

Reason mapping SHALL apply only to entries an operation adds. Entries carried through from an existing baseline SHALL keep their recorded `reason` verbatim regardless of any mapping.

A malformed mapping argument (no `=` separator, empty key, or empty value) or a duplicate key within one option SHALL be rejected with a non-zero exit code and an explicit diagnostic, rather than silently ignored.

#### Scenario: Custom reason overrides default
- **WHEN** user runs `arch-linter baseline generate --config policy.yml --output baseline.yml --reason "legacy debt accepted Q2 2026"`
- **THEN** all entries in the generated baseline SHALL have `reason: "legacy debt accepted Q2 2026"` instead of `"generated baseline"`

#### Scenario: Per-family reasons distinguish new package and composition entries
- **WHEN** user runs `baseline update --reason-for-family package_dependency="package debt — #501" --reason-for-family composition="composition debt — #502" --reason "other debt"` against a codebase with new package-dependency, new composition, and new dependency violations
- **THEN** the new package-dependency entries SHALL use `"package debt — #501"`, the new composition entries SHALL use `"composition debt — #502"`, and the remaining new entries SHALL use `"other debt"`

#### Scenario: Per-contract reason wins over per-family reason
- **WHEN** both `--reason-for-contract app-boundaries="contract debt"` and `--reason-for-family strict="family debt"` apply to a newly added entry of contract `app-boundaries`
- **THEN** that entry SHALL use `"contract debt"`

#### Scenario: Reason mapping never rewrites carried-through entries
- **WHEN** user runs `baseline update` with a `--reason-for-contract` mapping that targets a contract whose existing baseline entry still matches a current violation
- **THEN** that existing entry SHALL keep its original `reason` unchanged

#### Scenario: Malformed reason mapping is rejected
- **WHEN** user runs `baseline update --reason-for-family package_dependency` with no `=` separator
- **THEN** the command SHALL exit with a non-zero code and report the malformed mapping

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
- retains, unchanged, every existing baseline entry whose identity still matches a current violation (`matched`), including its original `reason` and `issue` text verbatim;
- adds new entries, deterministically, for current violations that have no matching existing baseline entry (`new`), using the resolved reason mapping for new entries only;
- leaves entries with no matching current violation (`resolved`), entries correlating to more than one current candidate (`ambiguous`), and entries referencing unknown contract ids (`stale`) untouched in the output — `update` SHALL NOT remove them.

`baseline update` SHALL accept `--policy`/`--config`, `--baseline` (existing baseline file to update), `--output`, `--mode` (strict/audit/all), `--condition-set`, `--contract` (repeatable), `--reason`, `--reason-for-contract`, `--reason-for-family`, `--dry-run`, `--force`, and `--json`, consistent with `baseline generate`.

`baseline update` SHALL report each affected entry with its lifecycle value and disposition in both human-readable and `--json` output, so the proposed change is reviewable before it is applied.

#### Scenario: Update preserves reason on still-valid entries
- **WHEN** user runs `baseline update` against a baseline containing an entry with a custom `reason` whose violation is still present in the current codebase
- **THEN** the updated baseline SHALL contain that entry with the identical `reason` text, unchanged, and the entry SHALL be reported with `status: matched` and `disposition: retained`

#### Scenario: Update adds new violations deterministically
- **WHEN** user runs `baseline update` against a baseline and the current codebase has a new violation not present in the baseline
- **THEN** the updated baseline SHALL contain a new entry for that violation using the resolved reason, reported with `status: new` and `disposition: added`

#### Scenario: Update does not remove stale entries
- **WHEN** user runs `baseline update` against a baseline containing an entry whose violation has been fixed in the current codebase
- **THEN** the updated baseline SHALL still contain that entry unchanged, reported with `status: resolved` and `disposition: retained` (removal is handled by `baseline prune`, not `baseline update`)

#### Scenario: Update carries an ambiguous entry through without broadening it
- **WHEN** user runs `baseline update` against a `version: 1` baseline entry whose legacy pair correlates to more than one current violation candidate
- **THEN** the entry SHALL be carried into the output unchanged and reported as `ambiguous`, and SHALL NOT be rewritten into any structured identity

### Requirement: User can prune stale entries from a baseline

The system SHALL provide a `baseline prune` CLI subcommand that reads an existing baseline file and the current codebase's violations, removes:
- entries whose identity no longer matches any current violation (`resolved`), and
- entries whose contract id does not exist in the current policy (`stale`),

writes the pruned baseline to `--output` (or stdout when `--output` is omitted), and reports every entry with its lifecycle value in both human-readable and `--json` output.

`baseline prune` SHALL remove only entries whose exact identity matched no current candidate. An entry correlating to more than one current candidate SHALL be reported as `ambiguous` and retained, never removed.

`baseline prune` SHALL NOT add entries for new violations — pruning only removes.

`baseline prune` SHALL accept `--policy`/`--config`, `--baseline`, `--output`, `--mode`, `--condition-set`, `--contract`, `--dry-run`, `--force`, and `--json`, consistent with `baseline generate`.

#### Scenario: Prune removes resolved debt and reports it
- **WHEN** user runs `baseline prune` against a baseline containing an entry whose violation no longer exists in the current codebase
- **THEN** the pruned baseline SHALL NOT contain that entry, and the command output SHALL list it with lifecycle `resolved`

#### Scenario: Prune removes entries with unknown contract ids and reports it
- **WHEN** user runs `baseline prune` against a baseline containing an entry whose contract id does not exist in the current policy
- **THEN** the pruned baseline SHALL NOT contain that entry, and the command output SHALL list it with lifecycle `stale`

#### Scenario: Prune leaves frozen entries untouched
- **WHEN** user runs `baseline prune` against a baseline where every entry still matches a current violation
- **THEN** the pruned baseline SHALL be byte-for-byte identical to the input baseline — including quoting, line endings, and blank lines — and no entries SHALL be reported as removed

A prune that removes no entry SHALL reproduce its input document verbatim rather than reserializing it, so a no-op prune cannot alter formatting.

#### Scenario: Prune previews removals before writing
- **WHEN** user runs `baseline prune --dry-run` against a baseline containing resolved entries
- **THEN** no file SHALL be written, and the output SHALL list the entries that would be removed together with the proposed pruned document

#### Scenario: Prune never removes an ambiguous entry
- **WHEN** user runs `baseline prune` against a baseline entry that correlates to more than one current violation candidate
- **THEN** the entry SHALL be retained in the pruned output and reported as `ambiguous`

### Requirement: User can diff a baseline against current violations

The system SHALL provide a `baseline diff` CLI subcommand that compares an existing baseline file against the current codebase's violations without writing any file, and reports each violation/entry with an explicit structured lifecycle `status` drawn from the shared lifecycle model: `new`, `matched`, `resolved`, `stale`, or `ambiguous`.

The `status` field SHALL be present, using these exact values, in `--json` output, so consumers can branch on `status` without parsing display text. Each `--json` entry SHALL additionally carry the entry's canonical structured identity — every `ArchitectureViolationIdentity` field plus its canonical string form — for version-2 documents, and `null` where a version-1 document has no structured identity. `--json` output SHALL include a `counts` object keyed by lifecycle wire name. Human-readable output SHALL continue to group entries under labeled sections corresponding to each status. (Baseline comparison does not currently produce SARIF output or a dedicated Testing API surface — this requirement applies to the CLI human/JSON output that exists today. Extending SARIF/Testing API to baseline comparison results is out of scope for this change.)

`baseline diff` SHALL accept `--policy`/`--config`, `--baseline`, `--mode`, `--condition-set`, `--contract`, and `--json`, consistent with other baseline subcommands. `baseline diff` SHALL exit with code 0 when the comparison completes successfully, regardless of lifecycle counts (it is a report, not a gate).

#### Scenario: Diff reports all four categories
- **WHEN** user runs `baseline diff` against a baseline and codebase containing new debt, matched debt, stale debt, and a configuration error
- **THEN** the output SHALL list all categories with their respective entries, an explicit `status` field per entry, a `counts` object, and SHALL exit with code 0

#### Scenario: Diff reports an ambiguous entry separately from a matched one
- **WHEN** user runs `baseline diff` against a baseline containing one entry that matches exactly one current violation and one entry that matches more than one
- **THEN** the first SHALL be reported with `status: matched` and the second with `status: ambiguous`, and the command SHALL exit with code 0

#### Scenario: Diff on a fully synchronized baseline reports no drift
- **WHEN** user runs `baseline diff` against a baseline where every entry matches a current violation and every current violation has a baseline entry
- **THEN** the output SHALL report zero `new`, zero `resolved`, zero `stale`, and zero `ambiguous` entries, and SHALL exit with code 0

#### Scenario: Diff JSON exposes canonical identity
- **WHEN** user runs `baseline diff --json` against a `version: 2` baseline
- **THEN** each reported entry SHALL carry its canonical structured identity fields and canonical identity string

### Requirement: User can verify a baseline is synchronized with current validation results

The system SHALL provide a `baseline verify` CLI subcommand that performs the same comparison as `baseline diff`, without writing any file, and exits with a non-zero code if any `resolved`, `stale`, or `ambiguous` entries are found (the baseline is out of sync), and exits 0 otherwise. `baseline verify` SHALL NOT fail due to `new` debt — new, unbaselined violations are the concern of `validate`, not `baseline verify`.

An `ambiguous` entry fails verification because an entry that suppresses more than one distinct violation broadens the ratchet — the same condition `baseline migrate` refuses to write through.

`baseline verify` SHALL accept `--policy`/`--config`, `--baseline`, `--mode`, `--condition-set`, `--contract`, and `--json`, consistent with other baseline subcommands. Its `--json` output SHALL include the same structured lifecycle `status`, canonical identity, and `counts` object as `baseline diff`.

#### Scenario: Verify passes when baseline is in sync
- **WHEN** user runs `baseline verify` against a baseline where every entry still matches exactly one current violation
- **THEN** the command SHALL exit with code 0

#### Scenario: Verify fails when baseline contains resolved debt
- **WHEN** user runs `baseline verify` against a baseline containing at least one entry whose violation has been fixed
- **THEN** the command SHALL exit with a non-zero code, and the entry SHALL be reported with `status: stale`

#### Scenario: Verify fails when baseline references an unknown contract id
- **WHEN** user runs `baseline verify` against a baseline containing an entry whose contract id does not exist in the current policy
- **THEN** the command SHALL exit with a non-zero code, and the entry SHALL be reported with `status: resolved`

#### Scenario: Verify fails when a baseline entry is ambiguous
- **WHEN** user runs `baseline verify` against a baseline entry that correlates to more than one current violation candidate
- **THEN** the command SHALL exit with a non-zero code, and the entry SHALL be reported with `status: ambiguous`

#### Scenario: Verify does not fail on new, unbaselined debt
- **WHEN** user runs `baseline verify` against a baseline that is otherwise fully in sync, but the current codebase has a new violation not present in the baseline
- **THEN** the command SHALL exit with code 0

#### Scenario: Verify JSON reports lifecycle counts
- **WHEN** user runs `baseline verify --json`
- **THEN** the output SHALL include a `counts` object reporting `new`, `matched`, `resolved`, `stale`, `changed`, `ambiguous`, and `configuration-error` counts with canonical identities on each entry

### Requirement: User can migrate a legacy baseline file to structured identity

The system SHALL provide a `baseline migrate` CLI subcommand that deterministically upgrades a `version: 1` baseline file to `version: 2` by correlating every legacy `ignored_violations` entry, from every contract group in the file, against freshly collected current-codebase candidates carrying full `ArchitectureViolationIdentity` data.

`baseline migrate` SHALL NOT accept `--mode` or `--contract`. A `version: 2` document cannot preserve `version: 1` matching semantics for only part of a file — an entry left unexamined could itself be ambiguous under structured identity, discoverable only by correlating it — so the command always classifies every entry in the file; there is no partial/scoped migration and no separate "out of scope" status.

For each legacy entry, scoped only to its own contract id (to find candidates belonging to the same contract), the system SHALL classify it as exactly one of:
- `matched`: exactly one current candidate's legacy-projected `(source_type, forbidden_reference)` pair equals the entry's pair — the entry SHALL be rewritten using that candidate's full structured identity, with `reason` and any issue metadata preserved verbatim;
- `stale`: zero current candidates match — the entry SHALL be excluded from the migrated output and reported;
- `ambiguous`: more than one current candidate matches — the entry SHALL be excluded from the migrated output and reported; migration SHALL NOT guess or silently broaden the entry to cover multiple identities.

`baseline migrate` SHALL accept `--policy`/`--config`, `--baseline` (required, the legacy file to migrate), `--output` (the destination path for the migrated file), `--condition-set`, `--dry-run`/`--check` (aliases for a report-only run), and `--json`.

`baseline migrate` SHALL NOT write to a path equal to the resolved `--baseline` input path under any circumstance.

`baseline migrate` SHALL pass through the same write gate as `generate`/`update`/`prune`: it SHALL refuse to replace an existing `--output` file without `--force`, and it SHALL write atomically so a failed write leaves the destination unchanged. Its `--dry-run`/`--check` run SHALL produce and report the proposed migrated document, so the classification report can be reviewed together with the content it would write.

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

#### Scenario: Dry-run reports the proposed document without writing
- **WHEN** user runs `baseline migrate --baseline legacy.yml --dry-run`
- **THEN** no file SHALL be written, the command SHALL report the classification (`matched`/`stale`/`ambiguous`) of every entry together with the proposed migrated document, and SHALL exit non-zero only if any entry is `ambiguous`

#### Scenario: Migrate refuses to replace an existing output without force
- **WHEN** user runs `baseline migrate --baseline legacy.yml --output migrated.yml` and `migrated.yml` already exists
- **THEN** the command SHALL exit with a non-zero code, SHALL leave `migrated.yml` unchanged, and SHALL report that `--force` is required

#### Scenario: Migrate has no --mode/--contract options
- **WHEN** user runs `baseline migrate` with a `--mode` or `--contract` flag
- **THEN** the command SHALL reject the flag as an unrecognized option and exit with a non-zero code

#### Scenario: Every entry in the file is classified, regardless of contract group
- **WHEN** user runs `baseline migrate` against a legacy baseline containing entries under multiple contract groups (for example both `strict` and `audit`)
- **THEN** every entry, from every contract group, SHALL be classified as `matched`, `stale`, or `ambiguous` against the full current candidate set — none SHALL be carried through unclassified

#### Scenario: An entry that would have been out of an old scope is still detected as ambiguous
- **WHEN** a legacy entry under a non-`strict` contract group correlates to more than one current violation candidate
- **THEN** the command SHALL classify it as `ambiguous` and fail closed exactly as it would for a `strict`-group entry, never silently upgrading it to a single fabricated identity

#### Scenario: Migrate refuses to overwrite the source file
- **WHEN** user runs `baseline migrate --baseline legacy.yml --output legacy.yml`
- **THEN** the command SHALL refuse to run and exit with a non-zero code, reporting that `--output` must differ from `--baseline`

#### Scenario: Real run without --output is refused
- **WHEN** user runs `baseline migrate --baseline legacy.yml` without `--output` and without `--dry-run`/`--check`
- **THEN** the command SHALL refuse to run and exit with a non-zero code, reporting that `--output` is required for a non-dry-run migration

#### Scenario: CLI help describes the migrate subcommand
- **WHEN** user runs `arch-linter baseline --help`
- **THEN** output SHALL include usage information for `baseline migrate`

### Requirement: Baseline entry lifecycle is a single shared model

The system SHALL classify every baseline entry and every current violation candidate considered by a `baseline` subcommand into exactly one value of the lifecycle vocabulary fixed by the `adoption-stabilization-compatibility` capability, using these canonical wire names in all `--json` output and the same words in human-readable output:

- `new`: a current finding has no exact baseline entry;
- `matched`: an entry and a current finding have equal canonical identity;
- `resolved`: a valid, evaluable baseline identity has no current finding;
- `stale`: the entry references a contract, family, source instance, schema, or identity form that is no longer valid or evaluable, distinct from resolved debt;
- `changed`: a deterministic predecessor/successor relationship can be shown but canonical identity differs, so the entry does not suppress until explicitly reviewed;
- `ambiguous`: more than one candidate could correspond to an entry and the tool refuses to guess;
- `configuration-error`: malformed, unsupported, or inconsistent input prevents safe classification.

These seven values are the entire vocabulary. A subcommand SHALL NOT introduce an additional status value, and SHALL NOT reuse one of these for a condition other than the one defined above.

Only `matched` SHALL be treated as an entry that suppresses a current finding. `changed`, `stale`, `ambiguous`, and `configuration-error` SHALL NOT silently suppress a current finding.

An entry whose contract id no longer exists in the policy SHALL classify as `stale`, since it references a contract that is no longer valid or evaluable; `configuration-error` is reserved for input that cannot be safely classified at all.

Regenerating an entry's display text (`source_type`/`forbidden_reference`) while its canonical identity is unchanged SHALL classify as `matched`, not `changed`: display text is not identity. `changed` requires that canonical identity actually differ, which is why a `changed` entry does not suppress.

What a subcommand *did* with an entry SHALL be reported separately from its lifecycle value, as a disposition of exactly one of `reported`, `added`, `retained`, or `removed`, so that `update` and `prune` can act differently on the same classification without either renaming it. `--json` output SHALL expose `status`, `disposition`, and a boolean `suppresses` per entry.

An entry carried through by `update` or `prune` SHALL keep its `reason` and `issue` metadata verbatim.

`baseline generate`, `update`, `prune`, `diff`, and `verify` SHALL report lifecycle counts, and their `--json` output SHALL expose those counts as a `counts` object carrying every lifecycle wire name, with `0` only for values the invoked operation cannot produce. Read-only `diff` and `verify` report every observed classification, including `resolved`; `resolved` describes a fixed baseline entry, not a removal performed by the command.

`baseline migrate` keeps its own `matched`/`stale`/`ambiguous` classification and its `matchedCount`/`staleCount`/`ambiguousCount` fields, as specified in its own requirement: it classifies legacy entries for a one-time identity upgrade rather than dispositioning entries of an already-current baseline, and `matched` there means "rewritten with structured identity", which no lifecycle value denotes. Its `stale` and `ambiguous` carry the same meaning as the shared model's.

#### Scenario: Update and prune describe the same disappeared entry with one status and distinct dispositions
- **WHEN** a baseline entry no longer matches any current violation, and the user runs `baseline update` and then `baseline prune` against it
- **THEN** both SHALL report the entry with `status: resolved`; `update` SHALL report `disposition: retained` and keep it in the output, and `prune` SHALL report `disposition: removed` and drop it from the output

#### Scenario: Only a matched entry is reported as suppressing
- **WHEN** any baseline subcommand reports entries classified `changed`, `stale`, `ambiguous`, or `configuration-error`
- **THEN** each SHALL be reported with `suppresses: false`, and only `matched` entries SHALL be reported with `suppresses: true`

#### Scenario: Regenerated display text stays matched
- **WHEN** `baseline update` carries through an entry whose canonical identity still matches a current finding but whose display text the current finding now renders differently
- **THEN** the entry SHALL be reported with `status: matched`, not `changed`

#### Scenario: JSON lifecycle counts are present for every write and report subcommand
- **WHEN** user runs `baseline generate`, `update`, `prune`, `diff`, or `verify` with `--json`
- **THEN** the output SHALL contain a `counts` object using the canonical lifecycle wire names

### Requirement: Baseline writes are previewable before any file changes

`baseline generate`, `baseline update`, and `baseline prune` SHALL accept `--dry-run`, which performs the full classification and produces the proposed baseline document, writes no file, and reports the lifecycle classification together with the proposed document content.

When `--output` is omitted, `baseline generate`, `baseline update`, and `baseline prune` SHALL write the proposed baseline document to stdout instead of to a file, and SHALL NOT modify any file.

A `--dry-run` run SHALL exit 0 when classification completes successfully, regardless of lifecycle counts.

#### Scenario: Dry run reports the proposal without touching the output file
- **WHEN** user runs `baseline update --baseline baseline.yml --output baseline.yml --dry-run`
- **THEN** `baseline.yml` SHALL be unchanged, and the output SHALL contain the lifecycle report and the proposed document content

#### Scenario: Omitted output writes the proposal to stdout
- **WHEN** user runs `baseline generate --config policy.yml` without `--output`
- **THEN** the proposed baseline YAML SHALL be written to stdout and no file SHALL be created

### Requirement: Replacing an existing baseline file requires explicit intent

`baseline generate` SHALL refuse to write when the resolved `--output` path already exists, and SHALL exit with a non-zero code reporting that `--force` is required to replace it and that `--dry-run` can be used to review the proposal first. With `--force`, `baseline generate` SHALL replace the file.

The in-place exemption SHALL be decided by a case-sensitive comparison of the resolved paths, because on a case-sensitive filesystem `baseline.yml` and `BASELINE.yml` are different files and a case-insensitive match would grant permission to replace a file the author never named. `baseline update` and `baseline prune` SHALL write without `--force` when the resolved `--output` path equals the resolved `--baseline` path, because naming the same file as both input and output is itself the statement of in-place intent and the written content is derived from that file. When `--output` names a different path that already exists, `baseline update` and `baseline prune` SHALL require `--force` on the same terms as `generate`.

#### Scenario: Generate refuses to replace an existing file
- **WHEN** user runs `baseline generate --config policy.yml --output baseline.yml` and `baseline.yml` already exists
- **THEN** the command SHALL exit with a non-zero code, SHALL NOT modify `baseline.yml`, and SHALL report that `--force` is required

#### Scenario: Generate replaces an existing file with explicit intent
- **WHEN** user runs `baseline generate --config policy.yml --output baseline.yml --force` and `baseline.yml` already exists
- **THEN** the command SHALL replace `baseline.yml` with the generated baseline and exit 0

#### Scenario: In-place update needs no force flag
- **WHEN** user runs `baseline update --baseline baseline.yml --output baseline.yml`
- **THEN** the command SHALL write the updated baseline to `baseline.yml` and exit 0 without requiring `--force`

### Requirement: A failed baseline write leaves the original file unchanged

Every baseline subcommand that writes a file SHALL write the content to a temporary file first and only then rename it over the destination. If producing or writing the content fails, the destination file SHALL remain byte-for-byte unchanged.

#### Scenario: Failed write preserves the original baseline
- **WHEN** writing the proposed baseline fails after the destination file already existed
- **THEN** the destination file SHALL retain its original content and the command SHALL exit with a non-zero code

### Requirement: Reviewed baseline comments are preserved or the update is refused

`baseline update` and `baseline prune` SHALL preserve, verbatim and in position, the leading block of comment and blank lines that precedes the first non-comment line of the existing baseline file, re-emitting it above the regenerated document.

A comment is any `#` that opens a YAML comment token, whether it begins a line or trails content on it (`reason: legacy debt # reviewed by Alice`). A `#` inside a quoted scalar, or one appearing mid-token, is not a comment. When the existing baseline file contains a comment at or after its first non-comment line — leading or trailing — `baseline update` and `baseline prune` SHALL NOT write any file, SHALL exit with a non-zero code, and SHALL report an actionable diagnostic that names the 1-based line numbers of the comments that cannot be safely round-tripped and states that `--dry-run` produces the proposed document for a manual merge.

`--dry-run` SHALL still classify and report against such a file, so the refusal always has a path forward.

#### Scenario: Reviewed header survives an update
- **WHEN** user runs `baseline update` against a baseline file whose first lines are comments recording ownership and a tracking ticket
- **THEN** the updated file SHALL begin with those comment lines unchanged, followed by the regenerated baseline document

#### Scenario: Interior comments are reported instead of silently dropped
- **WHEN** user runs `baseline update` against a baseline file that carries a comment line next to one of its `ignored_violations` entries
- **THEN** no file SHALL be written, the command SHALL exit with a non-zero code, and the diagnostic SHALL name that comment's line number and point at `--dry-run`

#### Scenario: A trailing comment on a content line is reported
- **WHEN** user runs `baseline update` against a baseline whose entry reads `reason: legacy debt # reviewed by Alice`
- **THEN** no file SHALL be written and the diagnostic SHALL name that line, rather than the rewrite silently discarding the trailing comment

#### Scenario: A hash inside a quoted scalar is not a comment
- **WHEN** a baseline entry's value is a quoted scalar containing `#`, and no other comment appears after the first content line
- **THEN** `baseline update` SHALL proceed and write normally

### Requirement: Baseline entries carry preserved issue metadata

`ignored_violations` entries SHALL support an optional `issue` string field, valid in both `version: 1` and `version: 2` documents, recording a tracking reference for the accepted debt.

`issue` SHALL be informational only — it SHALL NOT participate in ignore matching, identity, or deduplication.

`baseline update`, `baseline prune`, and `baseline migrate` SHALL carry the `issue` value of every entry they retain through to their output verbatim, exactly as they do for `reason`.

#### Scenario: Issue metadata survives update and prune
- **WHEN** user runs `baseline update` and then `baseline prune` against a baseline entry carrying both a custom `reason` and an `issue` reference, whose violation is still present
- **THEN** both outputs SHALL contain that entry with the identical `reason` and `issue` values, unchanged

#### Scenario: Issue metadata does not affect matching
- **WHEN** two baseline entries differ only by their `issue` value
- **THEN** they SHALL be treated as the same identity for matching and deduplication purposes

### Requirement: Baseline comparison results are available to machine consumers

The system SHALL project the canonical comparison entries from `baseline diff`,
`baseline verify`, and `baseline migrate` without reparsing display messages. Each
projection SHALL preserve an entry's structured identity when present and one of
the lifecycle statuses `new`, `matched`, `resolved`, `stale`, or `ambiguous`.

#### Scenario: Comparison result preserves exact identity
- **WHEN** a version 2 baseline comparison identifies a current or baseline entry
- **THEN** every machine-readable projection exposes the same canonical identity
  fields used for matching, rather than a display-text-derived key

#### Scenario: Comparison result preserves status
- **WHEN** a diff, verify, or migrate command classifies entries as new, matched,
  resolved, stale, or ambiguous
- **THEN** every machine-readable projection exposes the classification as a
  structured status value

### Requirement: Baseline identity is complete for every baseline-capable registered family
The system SHALL derive every version-2 baseline candidate from the canonical semantic identity
declared for its registered finding family. A candidate SHALL contain the authored contract ID,
concrete source-instance key when expansion applies, all applicable source/target assembly, type,
member, package, framework, API, configuration, and target-framework dimensions, plus a
deterministic non-line-based occurrence discriminator. Display text, reasons, paths, line numbers,
timings, report destinations, and rendering state SHALL NOT participate.

#### Scenario: A qualified family produces distinct exact candidates
- **WHEN** a baseline-capable family emits two otherwise similarly rendered findings with different semantic source, target, source-instance, or occurrence dimensions
- **THEN** baseline generation SHALL emit distinct structured entries and each entry SHALL suppress only its exact finding.

### Requirement: Requalified structured identities require review
The system SHALL never reinterpret a previously emitted structured baseline identity as broadly
matching after a required canonical dimension is introduced. It SHALL classify a proven one-to-one
predecessor/successor difference as `changed`, an unresolvable entry as `stale`, and multiple
possible successors as `ambiguous`; only `matched` SHALL suppress a live finding.

#### Scenario: An old under-qualified structured entry has one successor
- **WHEN** a version-2 baseline entry corresponds to exactly one live finding under legacy display projection but differs in its canonical structured identity
- **THEN** comparison SHALL report the entry as `changed`, SHALL not suppress the finding, and SHALL preserve its review metadata only in the explicit update path.

### Requirement: Baseline JSON errors are authoritative

When a baseline subcommand that accepts `--format json` terminates on an owned configuration, policy, or build-state failure, it SHALL write exactly one versioned JSON error document to stdout with a stable error category and typed details where available. Its exit code and human-format stderr output SHALL remain unchanged.

#### Scenario: Verify configuration failure is parseable JSON
- **WHEN** `baseline verify --format json` encounters an owned configuration failure
- **THEN** stdout parses as one JSON error document rather than human-readable configuration text

### Requirement: Baseline projection preserves imported finding identity
Baseline-capable consumers SHALL accept imported-diagnostic candidates expressed through the
existing structured `ArchitectureBaselineCandidate` identity contract. Candidate identity SHALL be
the same canonical occurrence identity as the imported normalized finding and SHALL exclude
transient artifact/run provenance.

#### Scenario: Native and imported candidates remain distinct
- **WHEN** a native finding and an imported diagnostic have similar display text or source labels
- **THEN** their structured baseline candidates remain distinct unless their full canonical
  identities are exactly equal

