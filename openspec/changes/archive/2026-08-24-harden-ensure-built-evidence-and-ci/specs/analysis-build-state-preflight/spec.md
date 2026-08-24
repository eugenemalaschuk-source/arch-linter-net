## ADDED Requirements

### Requirement: Prepared metadata provenance survives preflight failure
The system SHALL retain each successfully created metadata preparation before beginning preflight or
post-build re-preparation. When snapshot construction then fails or is cancelled before runner
materialization, error, profile, and output-collision projections SHALL use that retained
preparation as the fallback source for repository root, selected and missing assembly counts,
prepared project paths, selected artifact paths, and receipt paths.

#### Scenario: Metadata preflight fails before runner materialization
- **WHEN** metadata preparation succeeds and preflight then fails before a runner is materialized
- **THEN** the evaluation error records the prepared projects, selected artifacts, and their
  receipt paths as consumed inputs

#### Scenario: Metadata preflight is cancelled before runner materialization
- **WHEN** metadata preparation succeeds and cancellation is observed during preflight
- **THEN** the cancellation profile reports the prepared selected/missing counts and consumed
  project, artifact, and receipt input paths

### Requirement: Ensure-built uses one effective output context
The system SHALL derive an effective output context by applying CLI overrides over policy defaults
before `--ensure-built` preflight. The same effective configuration, target framework, platform,
and runtime identifier SHALL constrain graph build arguments, post-build output selection,
evaluated manifests, receipt publication, receipt verification, and cache identity. A prepared
artifact path SHALL be reused only when it matches that effective output context.

#### Scenario: Policy-selected Release output is rebuilt without a CLI override
- **WHEN** a policy selects Release configuration, the selected Release output exists, a compiled
  input changes, and `--ensure-built` runs without CLI configuration override
- **THEN** the graph build replaces the Release output and its receipt records the digest of the
  replacement bytes

#### Scenario: Platform constrains prepared-path reuse
- **WHEN** a build request supplies Platform while a prepared output path exists
- **THEN** post-build output resolution does not treat the prepared path as unconstrained solely
  because configuration, framework, and runtime identifier were omitted
