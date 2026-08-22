# release-forensics-dotnet-enrichment Specification

## Purpose
TBD - created by archiving change add-release-forensics-dotnet-enrichment. Update Purpose after archive.
## Requirements
### Requirement: Optional revision-safe .NET enrichment
After canonical Git history analysis succeeds, the system SHALL support an
optional downstream .NET enrichment pass. The pass SHALL bind its facts to the
resolved `to` commit and SHALL expose exactly one deterministic repository-level
status: `not_requested`, `not_applicable`, `available`, or `unavailable` with a bounded reason.

#### Scenario: Enrichment is not requested
- **WHEN** a caller runs history analysis without .NET enrichment
- **THEN** the successful result reports `not_requested` and retains no .NET facts

#### Scenario: Requested enrichment matches the clean checkout
- **WHEN** the resolved `to` commit is the exact clean checkout `HEAD` and verified project/source facts can be materialized
- **THEN** the successful result reports `available` and attaches deterministic .NET context to eligible logical files

#### Scenario: Historical revision cannot be trusted from the worktree
- **WHEN** requested enrichment finds a dirty worktree or a checked-out `HEAD` that differs from resolved `to`
- **THEN** canonical Git analysis remains successful and enrichment reports `unavailable` with a deterministic revision-state reason

### Requirement: Existing Core fact services own .NET context
The enrichment pass SHALL use Core policy loading, project discovery, verified
post-build assembly resolution, and `ArchitectureSourceFileFactIndex` to obtain
.NET facts. It SHALL NOT introduce a release-forensics-specific project scanner,
compiler, or Roslyn source parser.

#### Scenario: Project or build facts cannot be materialized
- **WHEN** policy loading, project discovery, build-state verification, assembly resolution, or source fact materialization fails
- **THEN** enrichment reports `unavailable` and every canonical Git-level result remains usable

#### Scenario: Deterministic fact projection
- **WHEN** project or source enumeration order differs between two eligible runs
- **THEN** projected project, assembly, namespace, type, kind, abstractness, and path facts have identical order and values

### Requirement: Canonical logical-file identity remains authoritative
The enricher SHALL join source facts only by the finalized logical file’s exact
canonical path. It SHALL NOT split same-path delete/re-add identity, merge
aliases, repair `ambiguous_dag` or lifecycle-broken components, or alter file
events, TaskKeys, authors, temporal evidence, graph evidence, scores, ranks, or
candidates.

#### Scenario: Same-path reuse stays one logical file
- **WHEN** a canonical path has delete/re-add generations in the analyzed range
- **THEN** enrichment attaches at most one current-revision projection to that existing logical-file identity and does not create generations

#### Scenario: Ambiguous rename lineage stays separate
- **WHEN** canonical Git evidence retains `ambiguous_dag` paths as separate logical files
- **THEN** enrichment does not join or suppress those files even if their source facts appear related

### Requirement: Per-file applicability is explicit
For an available repository-level enrichment, every logical file SHALL have an
explicit deterministic file status of `available` or `not_applicable`. Only an
exact canonical `.cs` path with trustworthy source facts can be `available`.

#### Scenario: Non-.NET or unavailable source path
- **WHEN** a logical file is not a C# source path or has no current-revision source facts
- **THEN** it reports `not_applicable` without affecting any other file or the repository-level availability

