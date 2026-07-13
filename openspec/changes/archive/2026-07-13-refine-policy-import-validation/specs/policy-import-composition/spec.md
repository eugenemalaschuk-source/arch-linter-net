## MODIFIED Requirements

### Requirement: Imports use an explicit ordered field
The root and fragments SHALL support a top-level `imports` sequence containing
non-empty explicit portable relative file paths. Each path SHALL resolve
relative to the document that declares it. An import string SHALL be Unicode
NFC and contain one or more non-empty `/`-separated segments. A segment SHALL
be `.` or `..`, or a portable file-name segment that contains no control
character or `<`, `>`, `:`, `"`, `/`, `\\`, `|`, `?`, or `*`; does not end in dot
or space; and is not a Windows reserved device name (`CON`, `PRN`, `AUX`,
`NUL`, `COM1`–`COM9`, or `LPT1`–`LPT9`, case-insensitively). Backslashes,
leading slashes, empty segments, drive/URI colons, non-NFC strings, and
interpolation tokens (`${`, `$(`, `%...%`, or a leading `~`) SHALL be rejected.
The grammar SHALL be validated before any host filesystem resolution. Absolute
paths, UNC/device paths, URI-like values, globs, environment interpolation,
and non-scalar entries SHALL be rejected.

#### Scenario: Root combines inline content with imports
- **WHEN** a root defines ordinary `layers`, `analysis`, or `contracts` content
  and an ordered `imports` sequence
- **THEN** the root inline content and imported fragment content participate in
  one composed policy

#### Scenario: Portable slash-separated path
- **WHEN** an import entry is `policy/contracts/domain.yml`
- **THEN** every supported host treats it as the same sequence of relative
  segments before canonical filesystem resolution

#### Scenario: Platform-native or interpolation form
- **WHEN** an import uses `\\`, `/etc/policy.yml`, `C:\\policy.yml`,
  `\\\\server\\share\\policy.yml`, `\\\\?\\C:\\policy.yml`, a URI, a glob, or
  an interpolation token
- **THEN** policy loading fails before the target is read with a portable-path
  grammar diagnostic

### Requirement: Fragment role and shape come from the import graph
Every document reached through `imports` SHALL be validated as a fragment
regardless of filename. A fragment MAY contain `imports`, `layers`,
`external_dependencies`, `packages`, `legacy_runtime_layers`, `analysis`,
`contracts`, and `classification`; it SHALL contain at least one mergeable
section or a non-empty `imports` sequence. A fragment SHALL NOT contain
`version`, `name`, baseline content, or unknown top-level fields. The explicitly
selected root SHALL be validated as a root source document: it SHALL declare
`version` and `name`, and it MAY declare `imports` and every existing root
policy section. Root-source validation SHALL not require sections that can be
contributed by fragments.

#### Scenario: Arbitrary fragment filename
- **WHEN** `architecture/policy.yml` imports `parts/domain.yaml`
- **THEN** `parts/domain.yaml` is treated as a fragment even though it does not
  match `*.arch.yml`

#### Scenario: Root-only field in fragment
- **WHEN** an imported document declares `version` or `name`
- **THEN** loading fails with a fragment-shape diagnostic naming that field and
  source

#### Scenario: Root defers a required effective section
- **WHEN** a root declares `version`, `name`, and `imports`, while fragments
  provide `layers`, `analysis`, and `contracts`
- **THEN** root-source validation succeeds and the complete graph proceeds to
  effective-policy validation

## ADDED Requirements

### Requirement: Composed effective policy is validated before semantic loading
After all sources pass role/shape validation and are composed, the system SHALL
validate the composed document against the full effective-policy schema before
fallback contract ID assignment and before the ordered
`IArchitecturePolicyDocumentValidator` pipeline. The full schema SHALL require
`version`, `name`, `layers`, `analysis`, and `contracts`, matching the current
production policy schema. The effective-policy schema SHALL validate the
composed document rather than any individual source.

#### Scenario: Fragments complete the root
- **WHEN** a root source omits `layers`, `analysis`, and `contracts` but its
  fragments compose all three sections
- **THEN** the full effective-policy schema accepts the composed document before
  fallback IDs and semantic validators run

#### Scenario: Required effective section absent from graph
- **WHEN** no source contributes one of `layers`, `analysis`, or `contracts`
- **THEN** loading fails against the effective-policy schema before fallback ID
  assignment and family-specific semantic validation

### Requirement: Raw classification sections compose without lost behavior
The composer SHALL preserve raw `classification.path`,
`classification.overrides`, and `classification.exclusions` nodes from every
role-valid source with their source descriptors and composed order.
`classification.path` entry counts SHALL be aggregated across the complete
graph; a non-zero aggregate SHALL set `ClassificationPathDeferred` to that
count, preserving the existing deferred-support diagnostic. `overrides` and
`exclusions` SHALL remain schema-valid, provenance-preserving deferred no-ops
until their separately specified runtime behavior exists. Model-bound
classification lists (`attributes`, `assembly_attributes`, `inheritance`, and
`namespace`) SHALL append in composed order, while `precedence` remains a
singleton conflict-checked field.

#### Scenario: Imported path entries remain visible
- **WHEN** root and fragments contribute two and three valid
  `classification.path` entries respectively
- **THEN** the composed document exposes `ClassificationPathDeferred` with a
  declared entry count of five and the diagnostics retain the contributing
  sources

#### Scenario: Deferred raw entries do not gain silent semantics
- **WHEN** a fragment declares schema-valid `classification.overrides` or
  `classification.exclusions`
- **THEN** the nodes are retained with provenance and composed order but do not
  alter classification behavior before their separate feature is implemented
