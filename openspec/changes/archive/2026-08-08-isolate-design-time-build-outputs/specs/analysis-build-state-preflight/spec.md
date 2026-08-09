## MODIFIED Requirements

### Requirement: Explicit ensure-built preparation mode
The system SHALL provide an opt-in preparation mode (CLI `--ensure-built` flag; Testing API `ArchitectureValidationBuilder.WithEnsureBuilt()`) that evaluates the selected graph, invokes the supported `dotnet build` path once for the whole graph using a structured executable and argument list (never a shell command string, never sourced from policy YAML, baseline, receipt, or cache content), stops distinctly on restore or build failure, and analyzes only artifacts verified after that build completes. Preparation and subsequent project-aware analysis SHALL leave the verified selected primary outputs continuously coherent and consumable; a successful no-op verification SHALL NOT delete, rewrite, or temporarily make unavailable the selected assembly, PDB, or other verified primary artifact.

#### Scenario: Ensure-built succeeds and validates
- **WHEN** `--ensure-built` is passed against a project graph with valid sources but no prior build output
- **THEN** the system builds the graph once, emits a build receipt, verifies the resulting artifacts, and proceeds to contract execution

#### Scenario: Ensure-built preserves verified primary outputs
- **WHEN** a selected project has been built and its primary output bytes are recorded before a successful `--ensure-built` validation
- **THEN** the selected primary outputs still exist with the same bytes after validation unless the requested build legitimately rebuilt them

#### Scenario: Ensure-built preserves verified primary outputs for concurrent consumers
- **WHEN** a selected project has been built and another process reads its primary outputs during a successful no-op `--ensure-built` validation
- **THEN** the reader can continuously access the original bytes and no selected primary output is missing, partial, or changed unless the requested build legitimately rebuilt it

#### Scenario: Ensure-built stops distinctly on build failure
- **WHEN** `--ensure-built` is passed and the invoked build fails
- **THEN** the system stops with a diagnostic distinguishing build failure from every preflight state and does not analyze partial or unverified artifacts

#### Scenario: Ensure-built preserves --no-restore
- **WHEN** both `--ensure-built` and `--no-restore` are passed
- **THEN** the build invocation includes `--no-restore` and does not access the network for package restore

#### Scenario: Sequential Testing API preparation remains consumable
- **WHEN** two `ArchitectureAssertions` validations use `WithEnsureBuilt()` sequentially in one process against unchanged selected outputs
- **THEN** both validations complete from verified artifacts without requiring an intervening consumer rebuild
