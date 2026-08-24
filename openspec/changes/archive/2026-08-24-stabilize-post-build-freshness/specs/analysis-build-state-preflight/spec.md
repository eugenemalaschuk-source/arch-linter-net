## MODIFIED Requirements

### Requirement: Explicit ensure-built preparation mode
The system SHALL provide an opt-in preparation mode (CLI `--ensure-built` flag; Testing API `ArchitectureValidationBuilder.WithEnsureBuilt()`) that evaluates the selected graph without loading any selected target artifact that the graph build may replace, invokes the supported `dotnet build` path once for the whole graph using a structured executable and argument list (never a shell command string, never sourced from policy YAML, baseline, receipt, or cache content), stops distinctly on restore or build failure, and analyzes only artifacts verified after that build completes. This metadata-only-before-build ordering SHALL apply whether or not analysis caching is enabled. Preparation and subsequent project-aware analysis SHALL leave the verified selected primary outputs continuously coherent and consumable; a successful no-op verification SHALL NOT delete, rewrite, or temporarily make unavailable the selected assembly, PDB, or other verified primary artifact. When a selected build input changes, preparation SHALL verify the replacement artifact and publish a receipt whose assembly digest equals that replacement artifact's content digest. After a successful graph build, post-build authorization SHALL refresh the already selected artifact closure and verify its receipts and content digests without relying on a second timestamp-based project-output discovery; ordinary validation that has not just completed that build SHALL retain its timestamp-based stale-output detection.

#### Scenario: Ensure-built succeeds and validates
- **WHEN** `--ensure-built` is passed against a project graph with valid sources but no prior build output
- **THEN** the system builds the graph once, emits a build receipt, verifies the resulting artifacts, and proceeds to contract execution

#### Scenario: Ensure-built prepares target metadata before loading selected artifacts
- **WHEN** `--ensure-built` targets an output that the temporary graph build may replace
- **THEN** the validating process completes metadata selection and build preparation before it loads that target artifact for analysis

#### Scenario: Ensure-built preserves the prepared output selection
- **WHEN** `--ensure-built` has no explicit configuration, framework, or runtime identifier and
  metadata preparation selects a Debug output while a newer Release output exists
- **THEN** receipt refresh verifies and records the selected Debug output rather than substituting
  the newer Release artifact

#### Scenario: Ensure-built replaces a stale selected output and binds its receipt
- **WHEN** a selected output exists, a compiled input changes after it was built, and
  `--ensure-built --no-restore` runs with restored prerequisites
- **THEN** the graph build replaces the selected output, its content digest changes, and the
  published receipt records that new content digest

#### Scenario: Post-build receipt verification survives timestamp ordering
- **WHEN** a successful `--ensure-built` graph build publishes a receipt and matching DLL digest
  for the selected output but the output timestamp is earlier than the source timestamp
- **THEN** post-build receipt verification treats the output as current and proceeds without
  weakening timestamp-based stale-output detection for ordinary validation

#### Scenario: Installed self-analysis can rebuild ArchLinterNet.Testing
- **WHEN** an installed CLI runs `--ensure-built` against a self-analysis policy selecting `ArchLinterNet.Testing`
- **THEN** the temporary graph build can replace the selected output and preparation completes with verified current receipts

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
