# analysis-build-state-preflight Specification

## Purpose
TBD - created by archiving change analysis-build-state-preflight. Update Purpose after archive.

## Requirements

### Requirement: Build-state preflight applies only to projects assembly resolution actually attempted
Build-state preflight SHALL evaluate a discovered project only when assembly resolution actually attempted to resolve it — i.e. its assembly name appears in either the resolved-assemblies set or the missing-assemblies set produced by that resolution attempt. This applies uniformly whether resolution was driven by `analysis.target_assemblies` or by the discovered project graph itself; it is not gated on which one configured it. A policy may declare a project list solely to feed project-scope coverage contracts, independently of which assemblies get resolved — such projects have no necessary correspondence to a resolved/missing assembly and SHALL NOT be preflight-blocked merely for being discovered. Separately, when assembly resolution was not attempted at all (neither set populated — the project-scope-coverage-tolerant path deliberately defers to the coverage engine), preflight SHALL NOT run for that request.

#### Scenario: target_assemblies-configured policy with a matching discovered project is preflight-blocked
- **WHEN** a policy declares `analysis.target_assemblies` naming an assembly that also corresponds to a discovered project (via `analysis.projects`), and that assembly's build output is missing
- **THEN** build-state preflight blocks with a `missing-artifact` diagnostic for that project

#### Scenario: a project list declared only for coverage is not preflight-blocked
- **WHEN** a policy declares a project list that project-scope coverage contracts consume, and none of those project assembly names appear in `analysis.target_assemblies` or were otherwise attempted by resolution
- **THEN** build-state preflight does not block on those projects, and coverage classifies them independently

#### Scenario: resolution deliberately deferred to coverage is not preflight-blocked
- **WHEN** no `analysis.target_assemblies` is configured and a project-scope coverage contract is active for the current mode, so assembly resolution is skipped entirely in favor of the coverage engine
- **THEN** build-state preflight does not run for that request

### Requirement: Build-input and analysis-input fingerprint computation
The system SHALL compute a build-input fingerprint and an analysis-input fingerprint for the selected project graph using a versioned canonical envelope (SHA-256 digest over sorted repository-relative content digests) consistent with the `analysis-build-state/v1` model defined in the `analysis-build-state-fingerprints` capability. This implementation computes the digest over the project file, relevant compiled/imported source content under the project directory (`.cs`, `.csproj`, `.props`, `.targets`, `.rsp`, `.editorconfig`), and `Directory.Build.props`/`.targets`/`.rsp`/`Directory.Packages.props` between the project and the repository root — a real, meaningfully narrower subset of the capability's full evaluated-input model, **not** a general guarantee that any equality-affecting build input is covered. Linked out-of-directory `Compile` items, custom `<Import>`s, analyzer/source-generator inputs and configs, out-of-directory additional files, package/reference/framework identities, and configuration/TFM/platform/RID are explicitly not part of this digest yet; a change to any of those does not invalidate a receipt in this version. Closing that gap (the full evaluated-input manifest) is an explicit follow-up against the fingerprint capability, not covered by this change.

#### Scenario: Same inputs produce the same fingerprint
- **WHEN** the same project graph is evaluated twice from checkouts with equivalent repository-relative content and no relevant input has changed
- **THEN** the computed build-input fingerprint is identical both times

#### Scenario: Source change invalidates the build-input fingerprint
- **WHEN** a compiled source file, project file, or relevant imported MSBuild file selected by the graph changes content
- **THEN** the computed build-input fingerprint differs from the previous computation

#### Scenario: Policy-only change does not invalidate build identity
- **WHEN** only the architecture policy document changes and no source/project/import content changes
- **THEN** the build-input fingerprint is unchanged and the analysis-input fingerprint changes

### Requirement: Preflight state machine with fixed precedence
The system SHALL evaluate the complete selected project graph before any contract executes and SHALL emit exactly one primary preflight state per project, chosen by this precedence order: `cancelled`, `restore-required`, `missing-artifact`, `wrong-configuration`, `wrong-target-framework`, `wrong-project-output`, `inconsistent-dependency-artifact`, `stale-artifact`, `unverifiable-artifact`, `current`.

#### Scenario: Clean checkout reports missing-artifact
- **WHEN** validation runs against a project with no build output present
- **THEN** the system emits a `missing-artifact` diagnostic naming the affected project and the exact build command, and stops before contract execution

#### Scenario: Stale artifact after a source change
- **WHEN** a previously verified build receipt exists but a selected source file has changed since the receipt's build-input fingerprint was computed
- **THEN** the system emits a `stale-artifact` diagnostic and stops before contract execution

#### Scenario: Wrong configuration is rejected
- **WHEN** the discovered artifact was built for a configuration other than the one requested for validation
- **THEN** the system emits a `wrong-configuration` diagnostic naming the requested and observed configuration

#### Scenario: Wrong target framework is rejected
- **WHEN** the discovered artifact was built for a target framework other than the one requested for validation
- **THEN** the system emits a `wrong-target-framework` diagnostic naming the requested and observed target framework

#### Scenario: Current artifact passes preflight
- **WHEN** a valid, current, receipt-verified build exists matching the requested configuration and target framework
- **THEN** the system emits a `current` state for that project and proceeds to contract execution without rebuilding

### Requirement: Ordinary validation never builds or restores implicitly
The system SHALL NOT invoke restore or build during ordinary validation. When artifacts are missing, stale, or otherwise not verifiably current, the system SHALL stop before contract execution and report a typed diagnostic identifying the affected project/assembly, requested configuration/target framework, observed state, and the exact build command where it can be determined deterministically.

#### Scenario: Existing simple usage does not build or restore implicitly
- **WHEN** a user runs ordinary validation without `--ensure-built` or `--no-restore`
- **THEN** the system performs no restore, build, or network access and only inspects existing state

### Requirement: Explicit ensure-built preparation mode
The system SHALL provide an opt-in preparation mode (CLI `--ensure-built` flag; Testing API `ArchitectureValidationBuilder.WithEnsureBuilt()`) that evaluates the selected graph without loading any selected target artifact that the graph build may replace, invokes the supported `dotnet build` path once for the whole graph using a structured executable and argument list (never a shell command string, never sourced from policy YAML, baseline, receipt, or cache content), stops distinctly on restore or build failure, and analyzes only artifacts verified after that build completes. This metadata-only-before-build ordering SHALL apply to standalone validation, shared snapshots, baseline verification, and architecture debt-gate candidate collection, whether or not analysis caching is enabled. Preparation and subsequent project-aware analysis SHALL leave the verified selected primary outputs continuously coherent and consumable; a successful no-op verification SHALL NOT delete, rewrite, or temporarily make unavailable the selected assembly, PDB, or other verified primary artifact. When a selected build input changes, preparation SHALL verify the replacement artifact and publish a receipt whose assembly digest equals that replacement artifact's content digest. After a successful graph build, post-build authorization SHALL refresh the already selected artifact closure and verify its receipts and content digests without relying on a second timestamp-based project-output discovery; ordinary validation that has not just completed that build SHALL retain its timestamp-based stale-output detection.

#### Scenario: Ensure-built succeeds and validates
- **WHEN** `--ensure-built` is passed against a project graph with valid sources but no prior build output
- **THEN** the system builds the graph once, emits a build receipt, verifies the resulting artifacts, and proceeds to contract execution

#### Scenario: Ensure-built prepares target metadata before loading selected artifacts
- **WHEN** `--ensure-built` targets an output that the temporary graph build may replace
- **THEN** the validating process completes metadata selection and build preparation before it loads that target artifact for analysis

#### Scenario: Architecture debt gate rebuilds stale candidates before loading them
- **WHEN** `gate --ensure-built` evaluates a baseline against a stale selected project output
- **THEN** it completes metadata selection and its graph build before loading the output for candidate collection, then compares the receipt-verified rebuilt candidates against the baseline

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

### Requirement: Explicit no-restore preparation mode
The system SHALL provide an opt-in `--no-restore` mode (and Testing API equivalent) that succeeds offline when all build prerequisites are already available and fails with an actionable prerequisite diagnostic, without attempting network access, when restore would otherwise be required.

#### Scenario: No-restore succeeds offline with prerequisites present
- **WHEN** `--no-restore` is passed and all referenced packages are already present in the local NuGet cache
- **THEN** validation proceeds without any network access

#### Scenario: No-restore fails closed when restore would be required
- **WHEN** `--no-restore` is passed and a required package is not available locally
- **THEN** the system emits a `restore-required` diagnostic naming the missing prerequisite and does not attempt network access

### Requirement: ArchLinterNet build receipt as authoritative freshness proof
The system SHALL treat a valid ArchLinterNet build receipt (v1), binding the project path, assembly name, requested configuration/target framework, the build-input fingerprint, and the built assembly's SHA-256 content digest, as the authoritative proof that an artifact is current. Artifacts without a valid, matching receipt SHALL be reported as `unverifiable-artifact` rather than accepted through timestamp or file-size heuristics. Fine-grained PE/PDB/reference-digest evidence and equivalent-compiler-evidence verification without a receipt (see proposal.md non-goals) are deferred follow-ups.

#### Scenario: Manually built artifact without a receipt is unverifiable
- **WHEN** a project was built outside of `--ensure-built` (e.g. by a plain `dotnet build`) and no matching ArchLinterNet receipt exists
- **THEN** the system emits an `unverifiable-artifact` diagnostic naming `--ensure-built` as the remediation

#### Scenario: Receipt digest mismatch is rejected
- **WHEN** a build receipt exists but its recorded build-input fingerprint does not match the freshly computed fingerprint
- **THEN** the system emits a `stale-artifact` diagnostic and does not treat the receipt as valid

### Requirement: Preflight diagnostics are normalized across human, JSON, and SARIF output
The system SHALL render every blocking preflight diagnostic through the existing typed diagnostic pipeline so human-readable, JSON, and SARIF output are projections of the same finding, with complete information in plain text without relying on color or TTY interactivity.

#### Scenario: JSON output includes typed preflight diagnostics
- **WHEN** validation is run with JSON output and a blocking preflight state occurs
- **THEN** the JSON output includes a preflight diagnostic entry with project, state, and expected-versus-observed evidence fields

#### Scenario: SARIF output includes typed preflight diagnostics
- **WHEN** validation is run with SARIF output and a blocking preflight state occurs
- **THEN** the SARIF output includes a result with a `build-state-preflight/<state>` rule id and a message naming the affected project and evidence

#### Scenario: Human output is complete without color
- **WHEN** validation is run in a non-interactive environment without color support
- **THEN** the human-readable preflight diagnostic conveys the same information as the colored variant

### Requirement: Testing API exposes typed preflight results
The `ArchLinterNet.Testing` API SHALL expose preflight diagnostics on the validation result alongside existing violation and coverage collections, and SHALL expose `WithEnsureBuilt()` and `WithNoRestore()` builder methods with behavior equivalent to the corresponding CLI flags.

#### Scenario: Testing API surfaces a blocking preflight state
- **WHEN** a Testing API caller validates a project graph with a missing artifact and does not call `WithEnsureBuilt()`
- **THEN** the returned result's preflight diagnostics include a `missing-artifact` entry and no contract results are produced

### Requirement: Preflight exposes evaluated-manifest eligibility consistently
The build-state preflight result, CLI diagnostics, and Testing API SHALL expose the same per-analysis-unit evaluated-manifest eligibility and sorted invalidation reasons. A cache-ineligible outcome SHALL not be presented as a cache hit or authorization, and it SHALL not redefine the existing primary ordinary-preflight state categories.

#### Scenario: Legacy receipt lacks evaluated evidence
- **WHEN** an otherwise current legacy receipt lacks the evaluated manifest and required artifact verification evidence
- **THEN** ordinary preflight retains its existing result while the cache eligibility is `cache-ineligible` with an explicit reason

#### Scenario: Machine-readable consumer observes the result
- **WHEN** CLI or Testing API emits build-state/profile diagnostics for a selected project
- **THEN** both projections contain the same eligibility value and invalidation reasons

### Requirement: Every selected analysis unit has an eligibility outcome
The system SHALL attach exactly one cache eligibility outcome to every selected analysis unit, including missing, stale, wrong-context, unverifiable, cancelled, and preparation-failed outcomes. `Platform` and runtime identifier SHALL participate in receipt and eligibility context.

#### Scenario: Preflight blocks a project
- **WHEN** preflight returns any state other than current
- **THEN** the diagnostic contains `cache-ineligible` and stable reason codes

### Requirement: Production validation preserves Platform and runtime identifier
The system SHALL preserve optional Platform and RuntimeIdentifier from public validation and snapshot requests through build-state preflight, output resolution, receipt publication, receipt verification, and evaluated-manifest collection. A requested Platform or RuntimeIdentifier that differs from receipt/output evidence SHALL produce a blocking wrong-context diagnostic and SHALL NOT share a cache-authorization context with another value.

#### Scenario: Validation request selects Platform
- **WHEN** a CLI, Testing, or application-service validation request specifies Platform
- **THEN** the preflight request, manifest, selected output, and emitted receipt contain that same Platform

#### Scenario: Validation request selects runtime identifier
- **WHEN** a CLI, Testing, or application-service validation request specifies RuntimeIdentifier
- **THEN** the preflight request, manifest, selected output, and emitted receipt contain that same RuntimeIdentifier

#### Scenario: Receipt context differs
- **WHEN** a receipt was published for a different requested Platform or RuntimeIdentifier
- **THEN** preflight reports a blocking wrong-context diagnostic before classifying the artifact current

### Requirement: Public API snapshot operations can explicitly prepare receipt-backed artifacts

The system SHALL allow public API surface operations to request the existing
explicit ensure-built preparation mode and optional no-restore behavior. After a
successful preparation, the operation SHALL rebuild its runner from the
post-build artifact state and run ordinary receipt verification before scanning
or writing a snapshot. Without explicit preparation, public API operations
SHALL retain ordinary fail-closed preflight behavior.

#### Scenario: Prepared public API capture from an ordinary build state
- **WHEN** a public API capture targets a discovered project graph with missing,
  stale, or receiptless artifacts and requests ensure-built
- **THEN** the system SHALL build the selected graph, publish and verify the
  receipt, re-resolve the target artifacts, and capture from the verified
  post-build state

#### Scenario: Ordinary public API operation remains fail closed
- **WHEN** a public API operation targets a receiptless artifact without
  explicit preparation
- **THEN** the system SHALL report an `unverifiable-artifact` preflight
  diagnostic and SHALL NOT capture, compare, or update a snapshot

#### Scenario: Prepared public API operation honours no-restore
- **WHEN** a public API operation requests both ensure-built and no-restore
- **THEN** the preparation build SHALL not restore dependencies and SHALL report
  a typed prerequisite failure when restore is required

### Requirement: Prepared metadata provenance survives preflight failure
The system SHALL retain each successfully created metadata preparation before beginning preflight or
post-build re-preparation. When snapshot construction then fails or is cancelled before runner
materialization, error, profile, and output-collision projections SHALL use that retained
preparation as the fallback source for repository root, selected and missing assembly counts,
prepared project paths, selected artifact paths, and receipt paths.

#### Scenario: Metadata preflight fails before runner materialization
- **WHEN** metadata preparation succeeds and preflight then fails before a runner is materialized
- **THEN** the evaluation error records the prepared projects, selected artifacts, and their
  receipt paths as consumed inputs

#### Scenario: Metadata preflight is cancelled before runner materialization
- **WHEN** metadata preparation succeeds and cancellation is observed during preflight
- **THEN** the cancellation profile reports the prepared selected/missing counts and consumed
  project, artifact, and receipt input paths

### Requirement: Ensure-built uses one effective output context
The system SHALL derive an effective output context by applying CLI overrides over policy defaults
before `--ensure-built` preflight. The same effective configuration, target framework, platform,
and runtime identifier SHALL constrain graph build arguments, post-build output selection,
evaluated manifests, receipt publication, receipt verification, and cache identity. A prepared
artifact path SHALL be reused only when it matches that effective output context.

#### Scenario: Policy-selected Release output is rebuilt without a CLI override
- **WHEN** a policy selects Release configuration, the selected Release output exists, a compiled
  input changes, and `--ensure-built` runs without CLI configuration override
- **THEN** the graph build replaces the Release output and its receipt records the digest of the
  replacement bytes

#### Scenario: Platform constrains prepared-path reuse
- **WHEN** a build request supplies Platform while a prepared output path exists
- **THEN** post-build output resolution does not treat the prepared path as unconstrained solely
  because configuration, framework, and runtime identifier were omitted
