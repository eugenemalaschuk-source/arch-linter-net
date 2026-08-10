## MODIFIED Requirements

### Requirement: Reusable named source sets are schema-backed and identity-stable
The policy schema SHALL allow a document-level `source_sets` map. Each entry SHALL have a stable
name, a `kind` of `assembly`, `layer`, or `project`, and SHALL declare explicit `members`,
constrained `globs`, or both. Assembly globs SHALL resolve only against
`analysis.target_assemblies`; layer globs SHALL resolve only against declared `layers` keys; and
project members/selectors SHALL resolve only against the final project-analysis universe. The
project universe SHALL be explicit `analysis.projects` when projects are declared explicitly, or
the repository-relative paths discovered from `analysis.solution` after `project_include` and
`project_exclude` filtering when solution discovery owns the inventory. Project globs SHALL use a
documented constrained repository-relative path-glob grammar, distinct from dot-segment assembly
and layer globs. Unrestricted regular expressions SHALL NOT be accepted, and no set SHALL expand
analysis beyond its declared or discovered boundary.

#### Scenario: A named assembly set resolves from declared targets
- **WHEN** a set of kind `assembly` declares a glob and the policy declares matching
  `analysis.target_assemblies`
- **THEN** the set resolves to exactly those declared target assemblies

#### Scenario: A project set resolves from filtered solution discovery
- **WHEN** a policy declares `analysis.solution`, excludes test projects, and a project-kind set
  declares a repository-relative path glob matching production projects
- **THEN** the set resolves to exactly the filtered discovered production project paths

#### Scenario: A project selector cannot add an undeclared project
- **WHEN** a project-kind set member or glob identifies a path outside the explicit or filtered
  discovered project universe
- **THEN** policy preparation fails with an actionable diagnostic naming the set and selector

#### Scenario: A glob without a declared universe fails closed
- **WHEN** a set declares a glob whose kind-specific universe is empty in the policy or discovery
- **THEN** policy preparation fails with an actionable diagnostic naming the set and missing
  declaration

## ADDED Requirements

### Requirement: Directional assembly contracts reuse deterministic source fan-out
The system SHALL allow `strict_assembly_dependency`, `audit_assembly_dependency`,
`strict_assembly_allow_only`, and `audit_assembly_allow_only` contracts to declare `sources`,
`source_sets`, `exclude_sources`, and `exclude_source_sets` using the established expansion model.
Each resolved source SHALL produce one ordinary contract instance with a derived ID, exact source,
authored identity alias, bounds, deduplication, and provenance identical to existing expandable
assembly-scoped contract families.

#### Scenario: One assembly allow-only rule expands to twenty sources
- **WHEN** one allow-only contract references an assembly set matching twenty declared target
  assemblies
- **THEN** the policy contains twenty distinct per-source instances with one authored identity

#### Scenario: A removed expanded source has no instance
- **WHEN** a directional assembly contract selects a source set and subtracts one matching source
- **THEN** no instance is created for the excluded source and exclusion provenance is retained

### Requirement: Project list unions retain deferred selector provenance
The system SHALL resolve `project_sets` for project-metadata contracts after final project
discovery, union their members into `projects` deterministically, and record set/reference/selector
provenance in the typed expansion inventory. A referenced non-optional project set that resolves to
zero paths SHALL fail closed with authored selector provenance.

#### Scenario: An authored project set covers multiple metadata rules
- **WHEN** multiple project-metadata contracts reference the same solution-derived project set
- **THEN** each contract receives the same deterministic resolved project list without duplicating
  project paths in `analysis.projects`

#### Scenario: An imported stale project selector fails with fragment provenance
- **WHEN** an imported fragment contains a project set selector that matches no filtered discovered
  project
- **THEN** preparation fails with the fragment's set and selector location
