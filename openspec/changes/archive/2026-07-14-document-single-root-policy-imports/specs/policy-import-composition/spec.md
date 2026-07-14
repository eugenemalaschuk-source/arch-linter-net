## ADDED Requirements

### Requirement: Public import authoring guidance is complete and filename-neutral
ArchLinterNet SHALL publish public guidance that explains one explicitly selected root, ordered local `imports`, graph-derived fragment roles, allowed fragment sections, root-inline composition, deterministic nested order, merge and conflict rules, repository path boundaries, graph limits, diagnostics, unsupported behavior, and the distinction between naming conventions and runtime requirements. The guidance SHALL recommend `architecture/arch.yml` for a concise stable root and concern-specific `*.arch.yml` fragment names without describing those names or `architecture/dependencies.arch.yml` as mandatory.

#### Scenario: Author chooses recommended names
- **WHEN** an author follows the recommended root and fragment naming conventions
- **THEN** the documentation explains that behavior comes from the selected root path and import edges rather than those names

#### Scenario: Author chooses arbitrary names
- **WHEN** an author selects `config/company-policy.yaml` and imports `pieces/domain.data`
- **THEN** the documentation and examples describe behavior equivalent to a recommended-name graph

#### Scenario: Author checks unsupported behavior
- **WHEN** an author considers multiple roots, remote imports, globs, silent overrides, environment interpolation, arbitrary YAML tags, or cross-file anchors
- **THEN** the public guidance identifies each behavior as unsupported

### Requirement: Public schema, migration, and troubleshooting guidance covers both roles
ArchLinterNet SHALL document explicit root and fragment schema selection for common schema-aware editors without requiring filename associations. It SHALL provide a behavior-preserving migration from a monolithic policy to one root plus focused fragments and troubleshooting for missing imports, cycles, duplicate paths or IDs, composition conflicts, path-boundary violations, invalid fragment shapes, and editor schema association.

#### Scenario: Editor validates an arbitrary fragment filename
- **WHEN** an author assigns `schema/dependencies.arch.fragment.schema.json` explicitly to an arbitrary imported file
- **THEN** editor validation uses fragment shape without relying on a filename pattern

#### Scenario: Monolithic policy is migrated incrementally
- **WHEN** an author moves one concern at a time from a valid monolithic policy into imported fragments
- **THEN** the guide preserves one root, global contract identity, composition order, and equivalent validation behavior at each checked step

### Requirement: Committed acceptance fixtures prove public import behavior
The repository SHALL contain executable NUnit-backed fixtures that prove equivalent monolithic and imported policies produce equivalent validation outcomes, recommended and arbitrary filenames produce equivalent outcomes, and root-versus-fragment plus fragment-versus-fragment conflicts fail without silent precedence.

#### Scenario: Equivalent public fixtures load
- **WHEN** the acceptance suite loads the monolithic, recommended-name imported, and arbitrary-name imported fixtures
- **THEN** their behaviorally relevant resolved models and validation outcomes are equivalent

#### Scenario: Conflicting public fixtures load
- **WHEN** the acceptance suite loads root-versus-fragment or fragment-versus-fragment duplicate definitions
- **THEN** loading fails with a composition-conflict category identifying both participating sources

