# project-coverage-contracts Specification

## Purpose
Detect first-party `.csproj` projects discovered via `analysis.solution`/`analysis.projects` that are not represented by any declared layer, namespace glob, expanded layer template, or explicit exclusion, including projects whose build output could not be resolved to a target assembly, so newly added or renamed projects cannot silently fall outside architecture policy.
## Requirements
### Requirement: Project coverage contracts classify discovered first-party projects
The system SHALL execute `strict_coverage` and `audit_coverage` contracts with `scope: project` by classifying every project in `ProjectDiscoveryResult.DiscoveredProjects` as `covered`, `excluded`, `uncovered`, or `unknown`.

#### Scenario: An uncovered discovered project produces a coverage finding
- **GIVEN** a `scope: project` coverage contract
- **AND** project discovery resolves a project whose assembly contains only namespaces matching no declared layer, namespace-glob layer, or expanded layer-template layer
- **AND** the project matches no `exclude` entry
- **WHEN** validation runs for that contract's mode
- **THEN** the system reports that project as an `"uncovered project"` finding identifying the project's path and assembly name

### Requirement: Project coverage requires project discovery to be configured
A `scope: project` coverage contract SHALL require `analysis.solution` or `analysis.projects` to be set, since `DiscoveredProjects` cannot exist otherwise.

#### Scenario: Project coverage without discovery configuration is rejected at load time
- **WHEN** a policy declares a `scope: project` coverage contract
- **AND** neither `analysis.solution` nor `analysis.projects` is set
- **THEN** the system rejects the policy with an actionable error naming the missing discovery configuration, instead of silently running the contract against zero units

### Requirement: Declared layers, namespace globs, and expanded layer templates provide project coverage
Project coverage classification SHALL treat a discovered project as `covered` when at least one type in its resolved assembly matches a declared layer, declared namespace-glob layer, or expanded layer-template layer — the same coverage-provider rule namespace coverage uses.

#### Scenario: A project with at least one matching type is covered
- **GIVEN** a discovered project whose resolved assembly contains one type under a declared layer's namespace and other types under namespaces matching no layer
- **WHEN** project coverage classification runs
- **THEN** the project is classified `covered`

### Requirement: A discovered project that cannot be resolved to a first-party assembly is unknown
When a discovered project's `AssemblyName` does not match any assembly in `ArchitectureAnalysisContext.TargetAssemblies` (filtered out, missing or stale build output, or ambiguous multi-target selection), project coverage SHALL classify that project as `unknown`, not `uncovered`.

#### Scenario: A discovered project with no resolved assembly is unknown
- **GIVEN** a discovered project whose assembly name is not present among the run's resolved target assemblies
- **WHEN** project coverage classification runs
- **THEN** the project is classified `unknown`, and the finding/summary evidence names the project's path and assembly name rather than a representative type

### Requirement: Project coverage exclusions require a reason and match by project identity
Project coverage contracts SHALL honor `exclude` entries that set `project` (matched against the discovered project's path or project-file name, exact match) together with a non-empty `reason`.

#### Scenario: A generated or sample project can be excluded
- **GIVEN** a `scope: project` coverage contract
- **AND** an `exclude` entry with `project: samples/Demo/Demo.csproj` and a documented reason
- **WHEN** project discovery resolves that exact project
- **THEN** that project does not produce an uncovered finding
- **AND** unrelated uncovered projects still produce findings

### Requirement: Project coverage findings and summary carry identity and path evidence
Project coverage findings and the `coverage_summary` entries for `scope: project` SHALL include the project's path, its discovered assembly name, and (when classified `uncovered`) a representative type from its resolved assembly.

#### Scenario: Uncovered project evidence includes path and representative type
- **WHEN** a discovered project is reported as uncovered
- **THEN** the finding/summary evidence includes that project's absolute or repository-relative path and a representative type name from its resolved assembly

