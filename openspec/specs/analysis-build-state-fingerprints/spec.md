# Analysis Build State Fingerprints Specification

## Purpose

Define the versioned build-input, analysis-input, artifact-verification, preflight-state, explicit-preparation, and immutable-snapshot contract used by ArchLinterNet 0.5.1 analysis workflows.

## Requirements

### Requirement: Versioned canonical fingerprint envelope
The system SHALL represent persisted or machine-readable analysis/build-state fingerprints with envelope id `analysis-build-state/v1`, SHA-256 lowercase hexadecimal digests, canonical UTF-8 JSON, ordinal lexicographic object-property ordering, and explicitly canonicalized array ordering.

#### Scenario: Equivalent portable serialization
- **WHEN** two supported hosts serialize the same logical fields in different in-memory map iteration orders
- **THEN** they produce the same canonical bytes and digest

#### Scenario: Equality semantics change
- **WHEN** a future implementation adds or reinterprets an equality-affecting field
- **THEN** it introduces a new versioned envelope or an explicitly compatible extension instead of silently changing v1

### Requirement: Build and analysis identity are separate
The system SHALL distinguish project-evaluation fingerprints, effective build-input fingerprints, effective analysis-input fingerprints, expected build-output identities, verified artifact fingerprints, completed analysis-session fingerprints, and process-local snapshot handles. Artifact freshness SHALL be proven against the build-input fingerprint; policy/configuration changes SHALL affect analysis/session identity without making unchanged artifacts stale.

#### Scenario: Policy-only change
- **WHEN** source, project, imports, compiler inputs, configuration, TFM, and artifacts are unchanged but effective architecture policy changes
- **THEN** the analysis-input and completed-session fingerprints change while build-input identity and artifact freshness remain unchanged

#### Scenario: Same repository state in different checkout roots
- **WHEN** two machines evaluate equivalent repository content, project graph, build inputs, policy, configuration, TFM, RID, and execution request under different absolute checkout roots
- **THEN** they produce equivalent build-input and analysis-input fingerprints

#### Scenario: Same logical inputs produce different artifact bytes
- **WHEN** two builds have the same build-input and analysis-input fingerprints but different verified PE/PDB byte digests
- **THEN** their artifact-set and completed-session fingerprints differ

#### Scenario: Process-local ownership differs
- **WHEN** two snapshot instances represent the same completed session fingerprint
- **THEN** they may have different opaque process-local handles without changing persisted equality

### Requirement: Portable path normalization and containment
The system SHALL use repository-relative paths with `/` separators and ordinal comparison/order for stable identity, SHALL exclude absolute checkout paths and host-specific prefixes from stable identity, and SHALL resolve traversal/symlink/junction containment before fingerprinting. External or ambiguous paths not covered by a typed logical coordinate SHALL fail as unverifiable.

#### Scenario: Windows and POSIX path equivalence
- **WHEN** the same repository path is discovered as `src\Product\Product.csproj` on Windows and `src/Product/Product.csproj` on POSIX
- **THEN** both are fingerprinted using `src/Product/Product.csproj`

#### Scenario: Selected path escapes through a symlink
- **WHEN** a selected source or project input resolves outside the repository root and no versioned external-input declaration exists
- **THEN** preflight reports an unverifiable input and no contract executes

#### Scenario: Case aliases collide
- **WHEN** two discovered repository-relative spellings map to the same host file on a case-insensitive file system
- **THEN** preflight rejects the ambiguous identity instead of silently collapsing the entries

### Requirement: Build-input fingerprint includes all relevant content
The system SHALL fingerprint the selected project graph and each project/configuration/TFM/platform/RID using raw content digests for selected source files, project files, and relevant imported MSBuild files together with the effective compiler/build manifest, generated/analyzer inputs, and project/package/framework/SDK/reference identities that can affect the analyzed output.

#### Scenario: Configuration or TFM changes
- **WHEN** only selected configuration or target framework changes
- **THEN** project-evaluation, build-input, analysis-input, expected-output, and completed-session fingerprints are distinct

#### Scenario: Source changes
- **WHEN** any selected source content changes
- **THEN** the build-input fingerprint changes and previously verified artifacts are stale

#### Scenario: Project or imported build input changes
- **WHEN** a selected project file or relevant imported MSBuild file changes, including a textual change whose evaluated result appears equivalent
- **THEN** the build-input fingerprint changes and previously verified artifacts are stale

#### Scenario: Analyzer or reference input changes
- **WHEN** an analyzer, source generator, analyzer config, additional file, SDK/reference pack, package, framework reference, project reference, or metadata reference relevant to the selected build changes identity or content
- **THEN** the build-input fingerprint changes

### Requirement: Analysis-input fingerprint includes effective policy and requested semantics
The system SHALL derive the effective analysis-input fingerprint from the build-input fingerprint plus root/imported policy provenance and content, effective composed configuration, selected condition set, requested analysis views that change results, and analysis-affecting tool/schema versions.

#### Scenario: Imported policy fragment changes
- **WHEN** an imported policy fragment changes while build inputs and artifacts remain unchanged
- **THEN** analysis/session identity changes but artifact preflight remains current

#### Scenario: Requested analysis semantics change
- **WHEN** a request changes a result-affecting mode, condition set, or selected analysis view
- **THEN** the analysis-input or completed-session fingerprint changes as specified by the request schema

### Requirement: Expected build-output identity is explicit
The system SHALL derive an expected output identity for each selected project/configuration/TFM/platform/RID containing canonical project key, assembly name, output kind, configuration, target framework, platform, optional RID, and logical output role/path.

#### Scenario: Artifact exists only for another configuration
- **WHEN** selected output is absent but a matching project/TFM output exists only for another evaluated configuration
- **THEN** preflight reports `wrong-configuration` rather than generic missing output

#### Scenario: Filename matches but project identity does not
- **WHEN** a copied PE is placed at the expected path but its assembly/project evidence does not match the expected output identity
- **THEN** preflight reports `wrong-project-output`

### Requirement: Freshness requires authoritative build attestation
The system SHALL accept an artifact as current only when the current effective build-input fingerprint is proven by either an `analysis-build-state/v1` ArchLinterNet build receipt or equivalent supported compiler-produced evidence that associates the PE/PDB with current document checksums, compilation options, metadata references, and expected output identity. Timestamp and file size SHALL NOT establish freshness.

#### Scenario: Product-owned build receipt matches
- **WHEN** explicit preparation emits a v1 receipt whose build-input fingerprint, expected outputs, and output digests match current state
- **THEN** the artifacts are eligible to be classified current

#### Scenario: Ordinary build has sufficient compiler evidence
- **WHEN** an existing PE and portable PDB/compiler record actually contain supported evidence covering current source checksums, compilation options, metadata references, target/output metadata, and PE/PDB association
- **THEN** ordinary validation may accept the artifact without repository-specific preparation

#### Scenario: Portable PDB is present but evidence is incomplete
- **WHEN** a portable PDB exists but required compilation-option or metadata-reference records are absent, stripped, unsupported, or incomplete
- **THEN** preflight reports `unverifiable-artifact` and recommends explicit preparation

#### Scenario: Timestamp appears current but build inputs differ
- **WHEN** an artifact timestamp is newer than inputs but receipt/compiler evidence binds it to a different build-input fingerprint
- **THEN** preflight reports `stale-artifact`

### Requirement: Verified artifact identity uses exact content digests
The system SHALL fingerprint verified PE, PDB or receipt, reference assembly where applicable, and required first-party dependency artifacts using SHA-256 content digests combined with expected output identity. PE MVID, timestamps, file size, searched paths, and attestation kind SHALL remain validation/display evidence rather than stable equality fields.

#### Scenario: Same-named artifact belongs to another project
- **WHEN** a byte-identical or same-named artifact is associated with a different expected project/output identity
- **THEN** it is not accepted as the selected project's verified artifact

#### Scenario: Dependency artifact is inconsistent
- **WHEN** verified reference evidence resolves a first-party dependency artifact that does not match the selected dependency graph identity/digest
- **THEN** preflight reports `inconsistent-dependency-artifact`

### Requirement: Build-state categories are distinct and fail closed
The system SHALL classify each selected project with one stable primary category using precedence `cancelled`, prerequisite/`restore-required`, `missing-artifact`, `wrong-configuration`, `wrong-target-framework`, `wrong-project-output`, `inconsistent-dependency-artifact`, `stale-artifact`, `unverifiable-artifact`, `current`. No contract SHALL execute unless every selected project is current.

#### Scenario: Wrong target framework
- **WHEN** candidate PE/PDB target-framework evidence differs from the selected TFM
- **THEN** preflight reports `wrong-target-framework` and stops before analysis

#### Scenario: One project in the graph is stale
- **WHEN** all but one selected project are current and one is stale
- **THEN** the whole session fails before partial contract execution

#### Scenario: Multiple symptoms exist
- **WHEN** a project has evidence for multiple failure symptoms
- **THEN** one primary category is selected by normative precedence and secondary evidence is retained

### Requirement: Ordinary validation never builds or restores implicitly
The system SHALL limit ordinary validation to project evaluation, prerequisite inspection, fingerprinting, and artifact verification. It SHALL NOT invoke restore, compilation/build targets, caller hooks, or network-dependent preparation.

#### Scenario: Clean checkout without artifacts
- **WHEN** ordinary validation runs on a clean checkout with no selected artifacts
- **THEN** it reports `missing-artifact`, includes the deterministic recommended build command where known, and executes no contract

#### Scenario: Current prepared checkout
- **WHEN** all local prerequisites and verifiable current artifacts already exist
- **THEN** ordinary validation proceeds without build, restore, or network access

### Requirement: Ensure-built preparation is explicit and structured
The system SHALL provide an opt-in ensure-built path that evaluates the selected graph first, invokes a supported graph-level build once using executable plus structured argv, stops distinctly on restore/build/cancellation failure, emits or validates authoritative evidence, and re-evaluates/re-verifies before analysis. Policy YAML SHALL NOT enable or control build execution.

#### Scenario: Ensure-built succeeds
- **WHEN** the trusted caller explicitly requests ensure-built and the supported build completes
- **THEN** the system re-evaluates and verifies post-build artifacts and analyzes only when every selected project is current

#### Scenario: Build fails
- **WHEN** the explicit build exits unsuccessfully
- **THEN** the system reports a typed build failure and publishes no successful session/snapshot fingerprint

#### Scenario: Policy attempts to provide executable data
- **WHEN** policy, imported fragment, baseline, snapshot, receipt, or cache content contains executable/argument data
- **THEN** that data is never used to start or parameterize preparation

#### Scenario: Caller supplies an optional hook
- **WHEN** #362 supports a trusted caller-provided build hook
- **THEN** it is executable plus argv with typed selected-input placeholders and is never executed through a shell command string

### Requirement: No-restore semantics are independent
The system SHALL treat `--no-restore` independently from ensure-built. Without ensure-built it forbids prerequisite recovery that requires restore/network; with ensure-built it constrains the build invocation and SHALL NOT silently fall back to restore.

#### Scenario: Offline prerequisites exist
- **WHEN** `--no-restore` is selected and all evaluated assets, SDK/reference packs, and verified artifacts exist locally
- **THEN** validation succeeds without network access

#### Scenario: No-restore prerequisites are missing
- **WHEN** `--no-restore` is selected and required restore assets are absent
- **THEN** preflight reports `restore-required` with an actionable command and executes no contract

#### Scenario: Ensure-built plus no-restore
- **WHEN** ensure-built and `--no-restore` are both selected
- **THEN** structured build preserves the no-restore boundary and reports failure instead of retrying with restore

### Requirement: Snapshot publication is atomic and cancellation safe
The system SHALL publish a completed session fingerprint or reusable snapshot only after evaluation, verification, and immutable snapshot materialization succeed for the full selected graph without cancellation. It SHALL retain immutable bytes/metadata or re-hash during materialization so TOCTOU changes cannot produce a successful partial snapshot.

#### Scenario: Input changes after verification
- **WHEN** an artifact or fingerprinted input changes between verification and snapshot materialization
- **THEN** the session fails and no reusable snapshot/cache result is published

#### Scenario: Cancellation during any preparation phase
- **WHEN** cancellation is requested during evaluation, build, hashing, verification, or materialization
- **THEN** the operation reports cancellation and publishes no successful or reusable partial identity

### Requirement: CLI and Testing share ownership semantics
The system SHALL expose equivalent state categories and completed-session identity through CLI, generic non-interactive CI, and `ArchLinterNet.Testing`. CLI SHALL own one snapshot per command; Testing MAY expose an explicitly owned reusable snapshot whose reuse requires identical completed-session fingerprint, compatible requested views, and an undisposed successful instance.

#### Scenario: Multiple Testing assertions reuse one snapshot
- **WHEN** multiple assertions request compatible views over one successful undisposed snapshot
- **THEN** they reuse immutable analysis content without re-evaluating or rebuilding

#### Scenario: Snapshot is failed, cancelled, partial, or disposed
- **WHEN** a caller attempts to reuse such a snapshot
- **THEN** reuse is rejected

### Requirement: Diagnostic evidence does not pollute portable identity
The system SHALL keep absolute local paths, timestamps, file sizes, searched candidate paths, exact local command rendering, timings, and host descriptions out of stable identity. JSON/SARIF SHALL prefer repository-relative or typed logical locations and omit absolute paths by default; human output MAY include actionable local paths clearly as evidence.

#### Scenario: Human and JSON report the same stale state
- **WHEN** a stale artifact is reported in human and JSON output
- **THEN** both preserve the same typed project/configuration/TFM evidence while only human output may include an absolute local path

#### Scenario: Execution provider changes
- **WHEN** the same validation runs under GitHub Actions, another CI provider, or a local non-TTY process
- **THEN** provider/TTY/color identifiers do not alter fingerprint equality or required diagnostic meaning

### Requirement: Downstream features reuse this contract
The system SHALL treat this capability as authoritative for #362, #363, #365, #366, #374, and #375. Downstream implementations MAY add versioned cache, profiling, diagnostic, or public API envelopes but SHALL NOT redefine v1 build/analysis/artifact/session equality or make timing/provider/output-path data part of it.

#### Scenario: Cache consumes completed session identity
- **WHEN** #365 constructs a cache key
- **THEN** it uses the completed session fingerprint as an input and adds independent cache schema/trust-domain controls rather than treating fingerprint equality alone as cache authorization

#### Scenario: Profiling records fingerprint phases
- **WHEN** #374 records evaluation, hashing, verification, build, or snapshot timings
- **THEN** those timings remain evidence and do not change any fingerprint
