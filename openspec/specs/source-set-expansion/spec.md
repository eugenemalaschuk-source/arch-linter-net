# source-set-expansion Specification

## Purpose
Let one reviewed contract target many assemblies, layers, or projects through named, schema-backed source sets, so shared rules are authored once instead of copy-pasted per module, while every resolved source stays visible, deterministic, bounded, and fail-closed across diagnostics, coverage, `explain`, JSON, and SARIF.
## Requirements
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

### Requirement: One authored contract expands into deterministic per-source instances
The system SHALL allow the single-source contract families for package dependency, package allow-only, framework reference, framework allow-only, external dependency, and external allow-only rules to declare `sources` and `source_sets` instead of `source`. The system SHALL expand such a contract into one contract instance per resolved source, deduplicated and ordered by ordinal comparison, so overlapping sets and repeated members produce exactly one instance per source. Expansion SHALL be bounded, and exceeding the bound SHALL be an actionable error.

#### Scenario: One rule targets many assemblies
- **WHEN** one package dependency contract references a named set that resolves to twenty declared target assemblies
- **THEN** the contract expands into twenty instances and emits at most one finding per resolved source

#### Scenario: Overlapping sets do not duplicate a diagnostic
- **WHEN** one contract references two sets that both resolve the same source
- **THEN** that source produces exactly one instance and one diagnostic

### Requirement: Expanded instances keep authored identity and exact resolved source
Each expanded instance SHALL carry a derived contract id composed of the authored contract id and the normalized resolved source, and SHALL retain an expansion origin naming the authored contract id, the set that produced it when applicable, and the exact selector that matched. Diagnostics and baseline entries for two different resolved sources SHALL be distinct identities. Contract selection and rule-input coverage `contract_ids` SHALL accept the authored contract id and resolve it to every instance it produced.

#### Scenario: Per-source baseline identities stay distinct
- **WHEN** two expanded instances of one authored contract each produce a finding
- **THEN** the two findings carry different contract ids and different sources

#### Scenario: The authored id selects every instance
- **WHEN** a request selects the authored contract id
- **THEN** every instance expanded from that contract is selected

### Requirement: List-shaped families reuse the same declarations without fanning out
The system SHALL allow project metadata contracts to declare `project_sets` and composition contracts to declare `allowed_only_in_assembly_sets`. Resolved members SHALL be unioned into the contract's existing list field, deduplicated and ordered deterministically, without producing additional contract instances.

#### Scenario: A shared host set is unioned into a composition boundary
- **WHEN** a composition contract references an assembly set naming several hosts
- **THEN** those hosts are added to `allowed_only_in_assemblies` as one contract

### Requirement: Zero-match and stale set inputs are first-class fail-closed diagnostics
The system SHALL fail policy loading when a contract references an unknown set name, when an `assembly`-kind member is absent from a non-empty `analysis.target_assemblies`, or when a referenced set resolves to no source. A set MAY declare `optional: true` with a non-empty `reason`; contracts referencing only optional empty sets SHALL expand to zero instances and SHALL be recorded as optional-empty rather than silently disappearing.

#### Scenario: A glob matching nothing fails
- **WHEN** a referenced set declares a glob that matches no declared input and is not optional
- **THEN** policy loading fails with a diagnostic naming the set, the selector, and the authored location

#### Scenario: An explicitly optional future set is accepted
- **WHEN** a referenced set declares `optional: true` with a reason and resolves to no source
- **THEN** policy loading succeeds and the expansion inventory records the contract as optional-empty with that reason

### Requirement: The resolved expansion is a typed, provenance-preserving inventory
The system SHALL record a deterministic expansion inventory listing each resolved set and each expanded contract with its authored id, resolved sources, and selectors. An instance produced through `source_sets[i]` SHALL retain independently the authored contract location, the `source_sets[i]` reference location, and the concrete resolved `members[i]`/`globs[i]` selector location, including imported-fragment locations. The inventory SHALL be exposed through the shared coverage inventory, `explain`, and structured JSON and SARIF output without requiring display-text parsing.

#### Scenario: Adding a matching module changes the inventory
- **WHEN** a newly declared target assembly matches an existing set glob
- **THEN** the expansion inventory gains that resolved source and the coverage inventory reports it

#### Scenario: Imported expansion keeps its authored location
- **WHEN** an expanded contract was authored in an imported fragment
- **THEN** its instances report the fragment's authored location

### Requirement: Source expansion supports bounded subtraction
The system SHALL allow the compatible source-scoped contract families (`package_dependency`, `package_allow_only`, `framework_dependency`, `framework_allow_only`, `external`, and `external_allow_only`) to subtract explicit sources or resolved source sets after ordered source expansion, without adding inputs beyond the declared source universe.

#### Scenario: Excluded expanded source creates no instance
- **WHEN** a source is resolved by an included source set and an exclusion
- **THEN** expansion SHALL not create an instance for that source and SHALL retain authored provenance for the exclusion evidence

### Requirement: Expanded contract baselines retain authored and concrete provenance
For a baseline-capable contract expanded from a source set, the identity SHALL retain both the
authored contract provenance and a stable concrete source-instance key. Different expanded sources
or source instances SHALL NOT share one baseline entry solely because their display fields match.

#### Scenario: Two expanded sources have matching display values
- **WHEN** one authored source-set contract expands to two concrete sources that produce otherwise equal display references
- **THEN** each finding SHALL have a distinct canonical baseline identity and baselining one SHALL not suppress the other.

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

