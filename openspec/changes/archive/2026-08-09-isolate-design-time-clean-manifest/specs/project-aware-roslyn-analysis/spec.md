## MODIFIED Requirements

### Requirement: Resolve project-aware compilation context via MSBuild design-time build
The system SHALL, for a discovered project (from `analysis.solution`/`analysis.projects`), run an MSBuild design-time build (without invoking the compiler) to resolve that project's real `Compile`-item source files and its fully resolved reference assembly paths (project references' build outputs, package references, and framework references for its actual target framework), without invoking `dotnet build` or `dotnet restore` itself. The evaluation SHALL use an isolated MSBuild `CleanFile` manifest and SHALL NOT delete or rewrite an existing selected primary build output at any time. Every returned reference assembly path SHALL remain available after the resolver returns.

#### Scenario: Successful design-time build
- **WHEN** a discovered project has already been restored (its assets file is present) and its MSBuild design-time build succeeds
- **THEN** the resolver returns that project's resolved source file paths and resolved reference assembly paths

#### Scenario: Design-time build preserves existing primary outputs
- **WHEN** project-aware analysis evaluates an already-built selected project
- **THEN** its assembly, PDB, and other primary outputs remain present and unchanged by that evaluation

#### Scenario: Design-time build is safe for concurrent primary-output readers
- **WHEN** project-aware analysis evaluates an already-built selected project while another process reads its assembly and PDB
- **THEN** the reader can continuously access the original bytes without observing a missing, partial, or changed primary output

#### Scenario: Design-time build leaves unrelated outputs alone
- **WHEN** an unrelated file below the project's output directory changes while project-aware analysis evaluates the project
- **THEN** the evaluation does not restore or otherwise overwrite that unrelated file

#### Scenario: Returned project-reference path remains consumable
- **WHEN** project-aware resolution returns a reference assembly for a project reference
- **THEN** that path exists after `Resolve()` returns and can be loaded into the subsequent Roslyn compilation

#### Scenario: Design-time build fails due to missing restore
- **WHEN** a discovered project has not been restored and its design-time build fails as a result
- **THEN** the resolver returns a failure result naming the project and the failure reason, instead of throwing

#### Scenario: Design-time build fails for other MSBuild reasons
- **WHEN** a discovered project's design-time build fails for a reason other than missing restore (e.g. invalid project XML, missing SDK)
- **THEN** the resolver returns a failure result naming the project and the failure reason, instead of throwing

#### Scenario: Resolution is not attempted without project discovery
- **WHEN** `analysis.solution` and `analysis.projects` are both unset
- **THEN** the resolver is never invoked and no MSBuild evaluation occurs
