## ADDED Requirements

### Requirement: Resolve project-aware compilation context via MSBuild design-time build
The system SHALL, for a discovered project (from `analysis.solution`/`analysis.projects`), run an MSBuild design-time build (without invoking the compiler) to resolve that project's real `Compile`-item source files and its fully resolved reference assembly paths (project references' build outputs, package references, and framework references for its actual target framework), without invoking `dotnet build` or `dotnet restore` itself.

#### Scenario: Successful design-time build
- **WHEN** a discovered project has already been restored (its assets file is present) and its MSBuild design-time build succeeds
- **THEN** the resolver returns that project's resolved source file paths and resolved reference assembly paths

#### Scenario: Design-time build fails due to missing restore
- **WHEN** a discovered project has not been restored and its design-time build fails as a result
- **THEN** the resolver returns a failure result naming the project and the failure reason, instead of throwing

#### Scenario: Design-time build fails for other MSBuild reasons
- **WHEN** a discovered project's design-time build fails for a reason other than missing restore (e.g. invalid project XML, missing SDK)
- **THEN** the resolver returns a failure result naming the project and the failure reason, instead of throwing

#### Scenario: Resolution is not attempted without project discovery
- **WHEN** `analysis.solution` and `analysis.projects` are both unset
- **THEN** the resolver is never invoked and no MSBuild evaluation occurs

### Requirement: Map matched source files to an owning discovered project
The system SHALL determine a method-body contract's owning discovered project(s) by directory containment: a discovered project owns a matched source file when that project's directory is the nearest ancestor directory among all discovered projects.

#### Scenario: Single owning project
- **WHEN** all of a contract's matched source files fall under exactly one discovered project's directory
- **THEN** that project is used as the sole candidate for project-aware resolution

#### Scenario: Matched files span multiple projects
- **WHEN** a contract's matched source files fall under more than one discovered project's directory
- **THEN** project-aware resolution is not attempted for that contract; the existing fallback compilation is used

#### Scenario: No owning project found
- **WHEN** none of a contract's matched source files fall under any discovered project's directory
- **THEN** project-aware resolution is not attempted for that contract; the existing fallback compilation is used

### Requirement: Explicit diagnostics distinguish project-aware from fallback analysis
The system SHALL emit a Configuration diagnostic naming the project and the failure reason whenever project discovery is configured but project-aware resolution could not be used for a method-body contract check, so ambiguity is visible rather than silently degrading to the fallback compilation.

#### Scenario: Fallback diagnostic on resolution failure
- **WHEN** project discovery is configured and the owning project's design-time build fails
- **THEN** a Configuration diagnostic is produced naming the project and the failure reason, and the contract's check falls back to the existing lightweight compilation

#### Scenario: No diagnostic when discovery is not configured
- **WHEN** `analysis.solution` and `analysis.projects` are both unset
- **THEN** no project-aware fallback diagnostic is produced; behavior matches the pre-existing lightweight-only analysis exactly

#### Scenario: No diagnostic on successful project-aware resolution
- **WHEN** project-aware resolution succeeds for a contract's owning project
- **THEN** no fallback diagnostic is produced for that contract
