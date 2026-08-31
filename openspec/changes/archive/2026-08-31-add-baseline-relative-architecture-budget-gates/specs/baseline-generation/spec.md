## MODIFIED Requirements

### Requirement: User can generate a baseline file from current violations
The system SHALL provide a `baseline generate` CLI subcommand that runs
validation against the current codebase and writes a baseline file containing
`ignored_violations` entries for all current violations not already suppressed
by manual ignores. The generated baseline SHALL be deterministic — identical
output for identical input code, policy, and selected contracts.

The generated baseline SHALL only contain entries for violations that survive
manual `ignored_violations` in the policy file. Manually ignored violations
SHALL NOT appear in the generated baseline. The subcommand SHALL accept
repeatable `--contract <id>` options that scope generation to named contracts;
without that option it SHALL cover all contracts in the selected mode.

For policies without selected baseline-relative metric budgets, generated files
SHALL retain format version `2`, with exact structured finding identities as
before. When one or more selected relative metric budgets are present, the
generated file SHALL use format version `3`, retain those structured finding
entries, and add one deterministic `metric_baselines` entry for every unique,
complete metric referenced by the selected relative budgets. A metric baseline
entry SHALL record its canonical metric identity and current scalar value; it
SHALL not be derived from a threshold finding and SHALL not replace or suppress
an `ignored_violations` entry.

One finding baseline entry SHALL suppress exactly one
`ArchitectureViolationIdentity`. Multiple distinct occurrences that share the
same legacy display text SHALL remain distinct by their structured occurrence
identity. Display messages, including embedded source line numbers, SHALL NOT
be used as identity.

#### Scenario: Generate baseline for a clean project
- **WHEN** a user runs `baseline generate` on a project with no relative metric
  budgets and zero violations
- **THEN** the generated baseline contains `version: 2` and an empty
  finding-level `baseline` collection

#### Scenario: Generate baseline captures exact violations
- **WHEN** a user runs baseline generation on a project with known dependency
  violations
- **THEN** each violation appears under its correct contract group and ID with
  a structured `ArchitectureViolationIdentity`

#### Scenario: Generate baseline captures a relative metric without a violation
- **WHEN** a policy has a complete no-worse metric budget whose current value
  equals its reviewed starting point and the user runs `baseline generate`
- **THEN** the generated version-3 file contains the metric scalar entry even
  though the budget produces no threshold finding

#### Scenario: Deterministic output across repeated runs
- **WHEN** a user runs baseline generation twice on the same unchanged codebase
- **THEN** both output files are byte-identical, including finding occurrences
  and metric baseline entries

#### Scenario: Manual ignores are not duplicated in baseline
- **WHEN** a user runs baseline generation where some violations are covered by
  manual `ignored_violations`
- **THEN** those violations do not appear as generated finding baseline entries

#### Scenario: CLI help describes baseline subcommand
- **WHEN** a user runs `arch-linter --help` or `arch-linter baseline --help`
- **THEN** output includes usage information for `baseline generate`, `baseline
  update`, `baseline prune`, `baseline diff`, `baseline verify`, and `baseline migrate`

#### Scenario: Selected-contract generation scopes output
- **WHEN** a user runs baseline generation with `--contract app-budget` where
  only `app-budget` is a relative metric budget
- **THEN** generated finding and metric baseline output contains only values
  referenced by selected contract IDs

#### Scenario: Same-named types in different assemblies do not collide
- **WHEN** two assemblies contain a same-named violating type and baseline
  generation captures only one exact structured finding identity
- **THEN** the entry suppresses only that assembly's violation during validation

#### Scenario: Multiple forbidden calls in one type each get a distinct entry
- **WHEN** one type contains repeated distinct forbidden-call occurrences and
  baseline generation captures only the first occurrence
- **THEN** the remaining occurrence still fails validation as new finding debt

### Requirement: User can consume a baseline file during validation
The system SHALL accept `--baseline` on validation and baseline lifecycle
subcommands. It SHALL load a dedicated baseline document and merge only its
finding-level `ignored_violations` entries into matching policy contracts in
memory. Version-1 files retain legacy exact display-pair matching; version-2
and version-3 finding entries retain full structured
`ArchitectureViolationIdentity` matching. A version-3 `metric_baselines`
collection SHALL be available only to selected relative metric budgets and
shall never be merged as an ignore.

The merged finding ignores SHALL retain all existing matching, stale tracking,
and unmatched-ignore behavior. The loader SHALL accept versions 1, 2, and 3
only; other versions or malformed/ambiguous metric baseline entries SHALL fail
explicitly. A version-3 document's finding entries SHALL retain the structured
identity requirements of version 2.

#### Scenario: Baseline suppresses existing violations but allows new ones
- **WHEN** validation uses a baseline containing a subset of finding debt
- **THEN** only exact matched finding identities are suppressed and unmatched
  violations still fail normal validation

#### Scenario: Legacy versions retain finding-baseline behavior
- **WHEN** validation reads an existing version-1 or version-2 baseline with
  no metric baseline collection
- **THEN** its existing finding matching behavior remains unchanged

#### Scenario: Baseline entries are resolved when violations are fixed
- **WHEN** a finding baseline entry no longer matches a current violation
- **THEN** existing resolved/unmatched-ignore behavior remains available for
  finding debt without mutating metric baseline values

#### Scenario: Baseline merges with manual ignores without duplicates
- **WHEN** validation loads both a manual ignore and an exact matching baseline
  ignore for one finding
- **THEN** the violation is suppressed once and other identities are unaffected

#### Scenario: Baseline validation fails with unknown contract ID
- **WHEN** a finding-level baseline entry references a contract that the loaded
  policy does not declare
- **THEN** validation reports the unknown contract explicitly and does not
  reinterpret it as a metric baseline entry

#### Scenario: Legacy version 1 baseline files load and match unchanged
- **WHEN** validation reads a version-1 baseline that has not been migrated
- **THEN** its exact legacy `(source_type, forbidden_reference)` matching is
  unchanged

#### Scenario: Version-3 metric entries do not suppress a finding
- **WHEN** a version-3 baseline contains a metric baseline and current
  validation also produces an ordinary dependency violation
- **THEN** the metric entry does not suppress that dependency violation

#### Scenario: Unsupported baseline version is rejected
- **WHEN** a baseline command or validation reads a document whose version is
  not 1, 2, or 3
- **THEN** it fails with an explicit unsupported-version error

#### Scenario: validate --baseline distinguishes same-named types in different assemblies
- **WHEN** a version-2 or version-3 finding baseline entry selects one
  assembly's same-named violation
- **THEN** the similarly named violation in another assembly remains unsuppressed

#### Scenario: validate --baseline distinguishes multiple occurrences in one type
- **WHEN** a version-2 or version-3 finding baseline entry selects one repeated
  forbidden-call occurrence
- **THEN** another canonical occurrence in the same type remains unsuppressed

#### Scenario: A version: 2 document whose entries lack structured identity fields is rejected
- **WHEN** a version-2 document has an ignored-violation entry missing required
  structured identity fields
- **THEN** loading fails explicitly rather than defaulting the missing identity

#### Scenario: A version: 1 document with structured identity fields is rejected
- **WHEN** a version-1 document carries an ignored-violation identity version
- **THEN** loading fails because structured finding identity is unavailable in
  version 1
