# assembly-coverage-contracts Specification

## Purpose
TBD - created by archiving change add-project-assembly-coverage. Update Purpose after archive.
## Requirements
### Requirement: Assembly coverage contracts classify resolved first-party assemblies
The system SHALL execute `strict_coverage` and `audit_coverage` contracts with `scope: assembly` by classifying every assembly in `ArchitectureAnalysisContext.TargetAssemblies` as `covered`, `excluded`, or `uncovered`.

#### Scenario: An uncovered resolved assembly produces a coverage finding
- **GIVEN** a `scope: assembly` coverage contract
- **AND** a resolved first-party assembly containing only namespaces matching no declared layer, namespace-glob layer, or expanded layer-template layer
- **AND** the assembly matches no `exclude` entry
- **WHEN** validation runs for that contract's mode
- **THEN** the system reports that assembly as an `"uncovered assembly"` finding identifying the assembly's name and file path

### Requirement: Assembly coverage works independent of project discovery
Assembly coverage classification SHALL operate on `ArchitectureAnalysisContext.TargetAssemblies` regardless of whether `analysis.target_assemblies` was set explicitly or seeded by project discovery, so it requires no `analysis.solution`/`analysis.projects` configuration.

#### Scenario: Assembly coverage runs with explicit target_assemblies and no discovery
- **GIVEN** a policy that sets `analysis.target_assemblies` explicitly and declares no `analysis.solution`/`analysis.projects`
- **AND** the policy declares a `scope: assembly` coverage contract
- **WHEN** validation runs
- **THEN** assembly coverage classification runs normally against the explicitly resolved assemblies

### Requirement: Declared layers, namespace globs, and expanded layer templates provide assembly coverage
Assembly coverage classification SHALL treat a resolved assembly as `covered` when at least one type inside it matches a declared layer, declared namespace-glob layer, or expanded layer-template layer.

#### Scenario: An assembly with at least one matching type is covered
- **GIVEN** a resolved first-party assembly containing one type under a declared layer's namespace and other types under namespaces matching no layer
- **WHEN** assembly coverage classification runs
- **THEN** the assembly is classified `covered`

### Requirement: Assembly coverage exclusions require a reason and match by assembly name
Assembly coverage contracts SHALL honor `exclude` entries that set `assembly` (matched against the assembly's simple name, ordinal) together with a non-empty `reason`.

#### Scenario: A test-utility or generated assembly can be excluded
- **GIVEN** a `scope: assembly` coverage contract
- **AND** an `exclude` entry with `assembly: MyApp.TestUtilities` and a documented reason
- **WHEN** the run resolves an assembly named `MyApp.TestUtilities`
- **THEN** that assembly does not produce an uncovered finding
- **AND** unrelated uncovered assemblies still produce findings

### Requirement: Assembly coverage findings and summary carry identity and path evidence
Assembly coverage findings and the `coverage_summary` entries for `scope: assembly` SHALL include the assembly's simple name, its file path when available, and (when classified `uncovered`) a representative type from that assembly.

#### Scenario: Uncovered assembly evidence includes path and representative type
- **WHEN** a resolved assembly is reported as uncovered
- **THEN** the finding/summary evidence includes that assembly's file path (when `Assembly.Location` is non-empty) and a representative type name

