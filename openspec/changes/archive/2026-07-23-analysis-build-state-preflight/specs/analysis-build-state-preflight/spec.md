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
The system SHALL compute a build-input fingerprint and an analysis-input fingerprint for the selected project graph using a versioned canonical envelope (SHA-256 digest over sorted repository-relative content digests) consistent with the `analysis-build-state/v1` model defined in the `analysis-build-state-fingerprints` capability. v1 of this implementation computes the digest over the project file and relevant compiled/imported source content (`.cs`, `.csproj`, `.props`, `.targets`) rather than the capability's full granular manifest field set (SDK identity, per-compiler-option digests, analyzer/generator identities) — sufficient to detect any selected content change, per that capability's own allowance that v1 need not prove a change was semantically harmless. Granular manifest fields are a follow-up.

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
The system SHALL provide an opt-in preparation mode (CLI `--ensure-built` flag; Testing API `ArchitectureValidationBuilder.WithEnsureBuilt()`) that evaluates the selected graph, invokes the supported `dotnet build` path once for the whole graph using a structured executable and argument list (never a shell command string, never sourced from policy YAML, baseline, receipt, or cache content), stops distinctly on restore or build failure, and analyzes only artifacts verified after that build completes.

#### Scenario: Ensure-built succeeds and validates
- **WHEN** `--ensure-built` is passed against a project graph with valid sources but no prior build output
- **THEN** the system builds the graph once, emits a build receipt, verifies the resulting artifacts, and proceeds to contract execution

#### Scenario: Ensure-built stops distinctly on build failure
- **WHEN** `--ensure-built` is passed and the invoked build fails
- **THEN** the system stops with a diagnostic distinguishing build failure from every preflight state and does not analyze partial or unverified artifacts

#### Scenario: Ensure-built preserves --no-restore
- **WHEN** both `--ensure-built` and `--no-restore` are passed
- **THEN** the build invocation includes `--no-restore` and does not access the network for package restore

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

