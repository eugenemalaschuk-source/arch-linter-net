## ADDED Requirements

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

`baseline generate`, `update`, `prune`, `diff`, and `verify` SHALL report lifecycle counts, and their `--json` output SHALL expose those counts as a `counts` object carrying every lifecycle wire name, with `0` only for values the invoked operation cannot produce. Read-only `diff` and `verify` report every observed classification, including `resolved`; `resolved` describes fixed baseline debt, not a removal performed by the command.

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

## MODIFIED Requirements

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
- **THEN** the command SHALL exit with a non-zero code, and the entry SHALL be reported with `status: resolved`

#### Scenario: Verify fails when baseline references an unknown contract id
- **WHEN** user runs `baseline verify` against a baseline containing an entry whose contract id does not exist in the current policy
- **THEN** the command SHALL exit with a non-zero code, and the entry SHALL be reported with `status: stale`

#### Scenario: Verify fails when a baseline entry is ambiguous
- **WHEN** user runs `baseline verify` against a baseline entry that correlates to more than one current violation candidate
- **THEN** the command SHALL exit with a non-zero code, and the entry SHALL be reported with `status: ambiguous`

#### Scenario: Verify does not fail on new, unbaselined debt
- **WHEN** user runs `baseline verify` against a baseline that is otherwise fully in sync, but the current codebase has a new violation not present in the baseline
- **THEN** the command SHALL exit with code 0

#### Scenario: Verify JSON reports lifecycle counts
- **WHEN** user runs `baseline verify --json`
- **THEN** the output SHALL include a `counts` object reporting `new`, `matched`, `resolved`, `stale`, `changed`, `ambiguous`, and `configuration-error` counts with canonical identities on each entry
