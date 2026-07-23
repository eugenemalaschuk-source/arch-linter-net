## ADDED Requirements

### Requirement: Versioned canonical fingerprint envelope
The system SHALL represent persisted or machine-readable analysis/build-state fingerprints with the `analysis-build-state/v1` envelope, SHA-256 lowercase hexadecimal digests, canonical UTF-8 JSON, ordinal lexicographic object-property ordering, and explicitly canonicalized array ordering.

#### Scenario: Equivalent portable serialization
- **WHEN** two supported hosts serialize the same logical fingerprint fields in different in-memory map iteration orders
- **THEN** they produce the same canonical bytes and digest

#### Scenario: Identity semantics change
- **WHEN** a future implementation needs to add or reinterpret an equality-affecting field
- **THEN** it introduces a new versioned envelope or an explicitly compatible extension instead of silently changing `analysis-build-state/v1`

### Requirement: Separate logical, artifact, session, and snapshot identities
The system SHALL distinguish project-evaluation fingerprints, effective analysis-input fingerprints, expected build-output identities, verified artifact fingerprints, completed analysis-session fingerprints, and process-local snapshot handles. The completed session fingerprint SHALL include the logical input fingerprint, verified artifact-set fingerprint, execution request, and tool analysis-semantics version; a process-local snapshot handle SHALL NOT be serialized or used for equality/cache identity.

#### Scenario: Same repository state in different checkout roots
- **WHEN** two machines evaluate equivalent repository content, policy, project graph, configuration, TFM, RID, and execution request under different absolute checkout paths
- **THEN** they produce equivalent logical input fingerprints

#### Scenario: Same logical inputs produce different artifact bytes
- **WHEN** two builds have the same logical input fingerprint but different verified PE/PDB byte digests
- **THEN** their artifact-set and completed session fingerprints differ while their logical input fingerprints remain equivalent

#### Scenario: Two owned snapshots over the same completed content
- **WHEN** two process-local snapshot instances represent the same completed session fingerprint
- **THEN** they may have different opaque handle identifiers without changing logical, artifact, or session equality

### Requirement: Portable path normalization and containment
The system SHALL use repository-relative paths with `/` separators and ordinal comparison/order for stable identity, SHALL exclude absolute checkout paths and host-specific prefixes from stable identity, and SHALL resolve traversal/symlink/junction containment before fingerprinting. External or ambiguous paths not covered by a typed logical coordinate SHALL fail as unverifiable.

#### Scenario: Windows and POSIX checkout equivalence
- **WHEN** the same repository path is discovered as `src\Product\Product.csproj` on Windows and `src/Product/Product.csproj` on POSIX
- **THEN** both are fingerprinted using the canonical key `src/Product/Product.csproj`

#### Scenario: Repository path escapes through a symlink
- **WHEN** a selected source or project input resolves outside the repository root and no versioned external-input declaration exists
- **THEN** preflight reports an unverifiable input and no contract executes

#### Scenario: Case aliases collide on a case-insensitive file system
- **WHEN** two discovered paths map to the same host file while preserving different repository-relative casing
- **THEN** preflight rejects the ambiguous identity instead of silently collapsing the entries

### Requirement: Project evaluation fingerprint includes effective analysis-affecting inputs
The system SHALL fingerprint the selected project graph and each selected project/configuration/TFM/platform/RID using its effective MSBuild/compiler manifest, including relevant project/import content, output identity, compiler options, compile/generated inputs, analyzer inputs, project/package/framework references, and typed SDK/package/framework identities whose change can affect the analyzed artifact.

#### Scenario: Configuration or TFM changes
- **WHEN** only the selected configuration or target framework changes
- **THEN** the project-evaluation, effective input, expected output, and completed session fingerprints are distinct

#### Scenario: Relevant imported build input changes
- **WHEN** a project or imported MSBuild file change alters an effective compiler option, reference, source input, output identity, or other analysis-affecting field
- **THEN** the project-evaluation and effective input fingerprints change

#### Scenario: Raw project edit has no effective effect
- **WHEN** a project/import file changes but the selected effective analysis/compiler manifest remains identical
- **THEN** the fingerprint is not changed solely because of the raw file timestamp or formatting difference

### Requirement: Expected build-output identity is explicit
The system SHALL derive an expected build-output identity for each selected project/configuration/TFM/RID containing the canonical project key, assembly name, output kind, target framework, optional RID, and logical expected output role/path.

#### Scenario: Artifact exists only for another configuration
- **WHEN** the selected output is absent but a candidate matching the project/TFM exists only under another evaluated configuration output
- **THEN** preflight reports `wrong-configuration` rather than generic missing output

#### Scenario: Artifact filename matches but project identity does not
- **WHEN** a copied PE is placed at the expected path but its assembly/project evidence does not match the expected output identity
- **THEN** preflight reports `wrong-project-output`

### Requirement: Freshness requires authoritative attestation
The system SHALL accept an artifact as current only when the current effective input fingerprint is proven by either an `analysis-build-state/v1` ArchLinterNet build receipt or equivalent supported compiler-produced evidence that associates the PE/PDB, source document checksums, compilation options, metadata references, and expected output identity. Timestamp and file size SHALL be supporting evidence only and SHALL NOT establish freshness.

#### Scenario: Product-owned build receipt matches
- **WHEN** a successful explicit preparation emits a v1 receipt whose input fingerprint, expected outputs, and output digests match the current state
- **THEN** the artifacts are eligible to be classified current

#### Scenario: Ordinary SDK build has sufficient portable PDB evidence
- **WHEN** an existing SDK build has a matching PE and supported portable PDB evidence covering current source checksums, compilation options, metadata references, and PE/PDB association
- **THEN** ordinary validation accepts it without repository-specific build configuration

#### Scenario: Artifact timestamp is current but content evidence is stale
- **WHEN** an artifact timestamp is newer than the inputs but PDB/receipt/compiler evidence does not match the current effective input fingerprint
- **THEN** preflight reports `stale-artifact`

#### Scenario: Evidence is insufficient
- **WHEN** the expected PE exists but no supported receipt or compiler-produced evidence can prove its relationship to the current effective inputs
- **THEN** preflight reports `unverifiable-artifact` and recommends explicit preparation

### Requirement: Verified artifact fingerprint uses exact content identity
The system SHALL fingerprint verified PE, PDB or receipt, reference assembly where applicable, and required first-party dependency artifacts using SHA-256 content digests combined with expected output identity. PE MVID, timestamps, file size, searched paths, and attestation kind SHALL remain validation/display evidence rather than redundant stable equality fields.

#### Scenario: Assembly is copied from another project
- **WHEN** a byte-identical or same-named artifact is associated with a different expected project/output identity
- **THEN** it is not accepted as the selected project's verified artifact

#### Scenario: Dependency artifact is inconsistent
- **WHEN** a selected project's verified metadata/reference manifest resolves a first-party dependency artifact that does not match the selected dependency graph identity/digest
- **THEN** preflight reports `inconsistent-dependency-artifact`

### Requirement: Build-state categories are distinct and fail closed
The system SHALL classify each selected project with a stable primary category using the precedence `cancelled`, prerequisite/`restore-required`, `missing-artifact`, `wrong-configuration`, `wrong-target-framework`, `wrong-project-output`, `inconsistent-dependency-artifact`, `stale-artifact`, `unverifiable-artifact`, `current`. No contract SHALL execute unless every selected project is current.

#### Scenario: Wrong target framework
- **WHEN** the candidate PE/PDB target-framework evidence differs from the selected TFM
- **THEN** preflight reports `wrong-target-framework` and stops before analysis

#### Scenario: One project in a graph is stale
- **WHEN** all but one selected project are current and one project is stale
- **THEN** the whole analysis session fails before any partial contract execution

#### Scenario: Multiple symptoms exist
- **WHEN** a project has evidence for more than one failure symptom
- **THEN** one primary category is selected by the normative precedence and secondary evidence is retained without changing the category

### Requirement: Ordinary validation never builds or restores implicitly
The system SHALL limit ordinary validation to project evaluation, prerequisite inspection, fingerprinting, and artifact verification. It SHALL NOT invoke restore, compilation/build targets, caller hooks, or network-dependent preparation.

#### Scenario: Clean checkout without artifacts
- **WHEN** ordinary validation runs on a clean checkout with no selected artifacts
- **THEN** it reports `missing-artifact`, includes the deterministic recommended build command where known, and executes no contract

#### Scenario: Current prepared checkout
- **WHEN** all prerequisites and verifiable current artifacts already exist
- **THEN** ordinary validation proceeds without build, restore, or network access

### Requirement: Ensure-built preparation is explicit and structured
The system SHALL provide an opt-in ensure-built preparation path that evaluates the selected graph first, invokes a supported graph-level build once using an executable plus structured argument vector, stops distinctly on restore/build/cancellation failure, emits or validates authoritative build evidence, and re-evaluates/re-verifies before analysis. Policy YAML SHALL NOT enable or control build execution.

#### Scenario: Ensure-built succeeds
- **WHEN** the caller explicitly requests ensure-built and the supported build completes successfully
- **THEN** the system re-evaluates and verifies the post-build artifacts and analyzes only when every selected project is current

#### Scenario: Build fails
- **WHEN** the explicit build process exits unsuccessfully
- **THEN** the system reports a typed build failure and publishes no successful session/snapshot fingerprint

#### Scenario: Policy attempts to provide a build command
- **WHEN** a root policy, imported fragment, baseline, snapshot, or cache entry contains executable/argument data
- **THEN** that data is never used to start or parameterize preparation

#### Scenario: Caller supplies an optional build hook
- **WHEN** #362 supports a caller-provided build hook
- **THEN** the hook is accepted only as trusted CLI/application executable plus argv values, uses only typed selected-input placeholders, and is never executed through a shell command string

### Requirement: No-restore semantics are independent
The system SHALL treat `--no-restore` independently from ensure-built. Without ensure-built it forbids prerequisite recovery that would require restore/network; with ensure-built it constrains the build invocation and SHALL NOT silently fall back to restore.

#### Scenario: Offline prerequisites exist
- **WHEN** `--no-restore` is selected and all evaluated assets, SDK/reference packs, and verified artifacts exist locally
- **THEN** validation succeeds without network access

#### Scenario: No-restore prerequisites are missing
- **WHEN** `--no-restore` is selected and required restore assets are absent
- **THEN** preflight reports `restore-required` with an actionable command and no contract executes

#### Scenario: Ensure-built plus no-restore
- **WHEN** ensure-built and `--no-restore` are both selected
- **THEN** the structured build invocation preserves the no-restore boundary and reports failure instead of retrying with restore

### Requirement: Completed snapshot publication is atomic and cancellation-safe
The system SHALL publish a completed analysis-session fingerprint or reusable snapshot only after evaluation, verification, and snapshot materialization succeed for the full selected graph without cancellation. It SHALL retain immutable bytes/metadata or re-hash inputs during materialization so a TOCTOU change cannot produce a successful partial snapshot.

#### Scenario: Artifact changes after verification
- **WHEN** an artifact or fingerprinted input changes between verification and immutable snapshot materialization
- **THEN** the session fails and no reusable snapshot/cache result is published

#### Scenario: Cancellation during evaluation, build, hashing, or materialization
- **WHEN** cancellation is requested before completion of any preflight/preparation/snapshot phase
- **THEN** the operation reports cancellation and publishes no successful or reusable partial identity

### Requirement: CLI and Testing API share identity and ownership semantics
The system SHALL expose equivalent build-state categories and completed session identity semantics through CLI, generic non-interactive CI, and `ArchLinterNet.Testing`. CLI SHALL own one snapshot per command; Testing MAY expose an explicitly owned reusable snapshot whose reuse requires the same completed session fingerprint, compatible requested views, and an undisposed successful instance.

#### Scenario: Multiple Testing assertions reuse one snapshot
- **WHEN** multiple Testing assertions request compatible views over one successful undisposed snapshot
- **THEN** they reuse the same immutable analysis content without re-evaluating or rebuilding

#### Scenario: Snapshot was cancelled, failed, or disposed
- **WHEN** a Testing caller attempts to reuse a cancelled, failed, partial, or disposed snapshot
- **THEN** reuse is rejected

### Requirement: Diagnostic evidence does not pollute portable identity
The system SHALL keep absolute local paths, timestamps, file sizes, searched candidate paths, exact local command rendering, timings, and host descriptions out of stable identity. JSON/SARIF SHALL prefer repository-relative or typed logical locations and omit absolute paths by default; human output MAY include actionable local paths clearly as evidence.

#### Scenario: Human and JSON diagnostics for the same failure
- **WHEN** a stale artifact is reported in human and JSON output
- **THEN** both preserve the same typed category and expected project/configuration/TFM evidence while only the human form may include an absolute local path

#### Scenario: CI provider changes
- **WHEN** the same validation runs under GitHub Actions, another CI provider, or a local non-TTY process
- **THEN** provider/TTY/color identifiers do not alter fingerprint equality or required diagnostic meaning

### Requirement: Downstream features reuse this contract
The system SHALL treat this capability as authoritative for #362, #363, #365, #366, #374, and #375. Downstream implementations MAY add versioned cache, profiling, diagnostic, or public API envelopes but SHALL NOT redefine v1 logical/artifact/session equality or make timing/provider/output-path data part of it.

#### Scenario: Cache implementation consumes a session fingerprint
- **WHEN** #365 constructs a cache key
- **THEN** it uses the completed session fingerprint as an input and adds its own cache schema/trust-domain controls rather than treating fingerprint equality alone as cache authorization

#### Scenario: Profiling records fingerprint phases
- **WHEN** #374 records evaluation, hashing, verification, build, or snapshot timings
- **THEN** those timings are observable evidence and do not change any fingerprint
