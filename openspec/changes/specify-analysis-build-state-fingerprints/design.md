## Context

Issue #355 requires one approved **Analysis and build state** slice before #362 can implement deterministic preflight and explicit build preparation. The slice must also remain usable by the immutable analysis snapshot (#363), verified cache (#365), final acceptance corpus (#366), profiling (#374), and cooperative cancellation (#375).

Today the repository has project discovery, MSBuild-backed Roslyn context resolution, assembly probing, and per-run session state, but no shared definition of:

- portable logical input identity;
- expected output identity for a project/configuration/TFM/RID;
- evidence that an existing PE/PDB was produced from the current effective inputs;
- exact artifact identity for snapshot/cache reuse;
- process-local snapshot ownership;
- fail-closed build-state categories.

A single hash cannot serve every purpose. A portable logical identity must ignore the absolute checkout location, while exact snapshot/cache reuse must distinguish different artifact bytes. Human diagnostics need local paths, timestamps, and searched locations, but those values are unstable and must not change portable equality.

## Goals / Non-Goals

**Goals:**

- Define the identity layers and their versioned relationship before implementation.
- Make configuration, TFM, RID, project graph, policy/configuration, source/compiler inputs, and verified artifacts explicit.
- Make identical repository inputs portable across supported operating systems without relying on identical absolute checkout paths.
- Reject missing, stale, mismatched, inconsistent, or unverifiable artifacts before any contract executes.
- Accept an ordinary current .NET SDK build when compiler-produced evidence is sufficient; require an explicit preparation path when it is not.
- Keep ordinary validation free of implicit build, restore, or network access.
- Keep policy YAML unable to start or parameterize build execution.
- Define CLI and Testing snapshot ownership/reuse consistently.
- Give #362, #363, #365, #374, and #375 one compatible contract.

**Non-Goals:**

- Implementing preflight, build orchestration, cache, profiling, or cancellation.
- Replacing MSBuild or defining non-.NET build orchestration.
- Making MSBuild evaluation or build execution a sandbox for untrusted repositories.
- Defining public CLI spelling beyond the already-approved `--ensure-built` / `--no-restore` semantics; #362 may choose equivalent command grouping while preserving this behavior.
- Publishing a new public policy field or allowing policy files to control preparation.
- Treating timestamps as proof of freshness.

## Decisions

### 1. Use a versioned canonical fingerprint envelope

Every persisted or machine-readable fingerprint uses the envelope identifier `analysis-build-state/v1`, SHA-256, lowercase hexadecimal digests, and canonical UTF-8 JSON. Object property names are serialized in ordinal lexicographic order. Arrays whose order has no semantic meaning are sorted by their declared canonical key before hashing; order-sensitive arrays retain their effective order and state that fact in their schema.

The envelope carries both `logicalInputFingerprint` and `artifactSetFingerprint`. The completed `analysisSessionFingerprint` hashes the envelope version, tool analysis-semantics version, execution request, logical input fingerprint, and artifact set fingerprint.

This split is intentional:

- two machines can produce the same logical input fingerprint from the same repository state even when checkout roots differ;
- exact session/snapshot/cache reuse still requires the same verified artifact bytes and tool semantics;
- nondeterministic builds may therefore share logical identity but have different artifact/session fingerprints.

### 2. Define six identity layers

1. **Project evaluation fingerprint** — one selected project under one configuration/TFM/RID. It describes the effective evaluated project/compiler manifest, not raw machine paths.
2. **Effective analysis input fingerprint** — selected project graph, effective policy/configuration provenance, source/compiler inputs, project-evaluation fingerprints, requested condition set, and analysis-affecting tool/schema versions.
3. **Expected build-output identity** — the logical output expected for a project/configuration/TFM/RID: project key, assembly name, output kind, target framework, RID, and canonical output role/path.
4. **Verified artifact fingerprint** — expected output identity plus SHA-256 digests and verified metadata for the PE, associated PDB or build receipt, reference assembly where applicable, and required first-party dependency artifacts.
5. **Completed analysis-session fingerprint** — effective analysis input fingerprint + verified artifact-set fingerprint + execution request + tool analysis-semantics version. It is not publishable until preflight and snapshot construction complete successfully.
6. **Testing snapshot identity and ownership** — the completed session fingerprint identifies reusable content; a separate opaque process-local handle identifies one owned object instance. The handle is never serialized, persisted, or used as a cache key.

### 3. Stable identity uses repository-relative logical paths

For files inside the repository root, the stable path key is the repository-relative path with `/` separators and the repository/discovery spelling preserved. Equality and ordering are ordinal. Absolute checkout paths, drive letters, UNC prefixes, host temp paths, and current working directory are excluded.

Before creating a key, the implementation resolves path traversal and symlink/junction containment. A path escaping the repository root is rejected as `unverifiable` unless a future explicitly versioned external-input contract permits it. Symlink identity includes the repository-relative link path, normalized link target text, and resolved content digest. Duplicate logical paths caused by case aliases on a case-insensitive file system are rejected rather than silently collapsed.

Paths outside the repository that are legitimate SDK/package inputs use typed logical coordinates instead of absolute paths, for example SDK identity/version, NuGet package id/version/content hash, framework-reference name/version, or assembly identity/content hash.

### 4. Project evaluation fingerprints capture effective analysis-affecting inputs

The v1 project-evaluation manifest includes at minimum:

- canonical project path and selected graph edges;
- configuration, target framework, platform, and optional RID;
- project/import content digests for relevant MSBuild inputs;
- assembly name, output type, target name/extension, expected output role, reference-assembly behavior, and deterministic/debug settings;
- effective compiler options that can change emitted code or semantic analysis, including defines, language version, nullable, unsafe, checked, optimization, deterministic/path-map, and warning-as-error state where it affects compilation;
- canonical `Compile`, generated-compile, analyzer-config, additional-file, analyzer/source-generator, project-reference, package-reference, and framework-reference manifests relevant to the selected build;
- content digests or typed identities for every input whose content can affect the analyzed artifact.

Environment variables, global properties, and SDK selection are included only when they affect the evaluated manifest; machine-local values that do not affect it are diagnostic evidence only. An implementation must not key the fingerprint from a hand-picked subset while silently ignoring other MSBuild/compiler inputs it discovers as analysis-affecting.

A raw project/import file edit that leaves the effective manifest unchanged does not create a false stale state. A change that changes any effective field or digest necessarily changes the fingerprint.

### 5. Artifact verification requires an attestation, not timestamps

The current effective analysis input fingerprint is compared against one of two authoritative attestations:

1. **ArchLinterNet build receipt v1** produced after a successful product-owned preparation. It records the exact effective input fingerprint, expected output identities, output digests, and build command category.
2. **Equivalent compiler-produced evidence** for ordinary SDK builds. For v1 this means a matching PE plus a portable PDB (or equivalent supported compiler record) whose document checksums, compilation options, metadata-reference identities, and PE/PDB association are sufficient to reconstruct and compare the effective compilation manifest. PE metadata supplies assembly name, target-framework attribute, module MVID, assembly references, and debug-directory association. MVID is validation evidence; the PE/PDB SHA-256 digests are the exact artifact identity.

Timestamp and file size may help explain or short-circuit obviously stale candidates, but they never establish freshness and never appear in stable equality. If neither attestation is complete enough to prove the selected input/output relationship, the state is `unverifiable`, not current. The diagnostic recommends explicit preparation.

This permits default modern .NET SDK builds with portable PDB evidence to be accepted without repository-specific configuration, while fail-closing for copied PE files, stripped/unsupported debug evidence, opaque legacy builds, or incomplete outputs.

### 6. Distinct build-state categories have deterministic precedence

Preflight evaluates the complete selected graph before contract execution and reports typed per-project evidence. The primary category precedence is:

1. `cancelled`;
2. `restore-required` or project-evaluation prerequisite failure;
3. `missing-artifact`;
4. `wrong-configuration`;
5. `wrong-target-framework`;
6. `wrong-project-output`;
7. `inconsistent-dependency-artifact`;
8. `stale-artifact`;
9. `unverifiable-artifact`;
10. `current`.

A result may carry secondary evidence, but the primary category is stable. Examples:

- an assembly found only under another evaluated configuration is `wrong-configuration`, not merely missing;
- a PE whose target-framework metadata does not match the selected TFM is `wrong-target-framework`;
- a copied assembly with the expected filename but wrong project/assembly identity is `wrong-project-output`;
- a project reference whose resolved artifact digest/identity does not match the selected dependency graph is `inconsistent-dependency-artifact`;
- current output metadata with mismatched source/PDB/compiler-input evidence is `stale-artifact`;
- insufficient evidence to compare is `unverifiable-artifact`.

No contract family executes when any selected project is not `current`.

### 7. Preparation is explicit, structured, and outside policy YAML

Ordinary validation performs evaluation and verification only. It never runs `dotnet restore`, `dotnet build`, an MSBuild compile target, or a caller command.

Explicit ensure-built preparation:

- is opt-in from the CLI/application caller;
- resolves the selected graph/configuration/TFM/RID first;
- invokes one supported graph-level build operation, not one build per contract and not repeated builds per project when a solution/project graph can be built once;
- uses an executable plus structured argv, never a shell command string;
- stops distinctly on restore failure, build failure, cancellation, or incomplete receipt/evidence;
- re-evaluates and re-verifies after the build before analysis;
- analyzes only the post-build verified artifacts.

`--no-restore` is independent:

- without ensure-built, it forbids any prerequisite path that would require restore/network and returns `restore-required` with the exact recommended command;
- with ensure-built, it passes the no-restore boundary to the supported build invocation and fails actionably when assets/prerequisites are absent;
- it never means "silently use whatever output exists."

A caller-supplied build hook, if implemented by #362, is accepted only from trusted CLI/application configuration as an executable and argv array. Policy YAML, imported fragments, baselines, snapshots, and cache entries cannot provide executable paths or arguments. Placeholders may expand only from typed selected inputs (project/solution path, configuration, TFM, RID) and are inserted as individual argv values.

### 8. Offline and restricted execution is deterministic

Ordinary validation does not require network access. A prepared checkout with local SDK/reference packs, restored assets, and verifiable current artifacts can validate offline. Ensure-built without `--no-restore` may invoke restore and therefore may require network access; that boundary is stated before analysis. Ensure-built with `--no-restore` never falls back to restore.

The design does not claim MSBuild evaluation/build is safe for an untrusted repository. Evaluation and especially ensure-built operate inside the repository trust boundary. Explicit build remains a user decision.

### 9. Snapshot construction closes the TOCTOU window

Verification and analysis use one immutable snapshot. After verification, the implementation either retains immutable bytes/metadata for the artifacts and source manifests used by analysis or re-hashes immediately when loading them into the snapshot. Any change between verification and snapshot materialization aborts with a failed preflight/session; it never yields a successful partial snapshot.

The completed session fingerprint is published only after:

- project evaluation and prerequisite checks succeed;
- every selected artifact/dependency is current and verified;
- immutable analysis inputs/indexes required by the snapshot are materialized or safely owned for lazy materialization;
- cancellation has not been requested.

CLI owns and disposes one snapshot per command. `ArchLinterNet.Testing` may expose an explicitly owned snapshot for multiple assertions; reuse requires the same completed session fingerprint, an undisposed snapshot, and compatible requested views. A cancelled, failed, disposed, or partial snapshot is never reusable.

### 10. Stable identity and evidence are separate

Stable/cache equality fields:

- envelope/schema/tool analysis-semantics versions;
- canonical repository-relative or typed logical coordinates;
- configuration/TFM/platform/RID;
- canonical effective project/compiler/policy/source manifests;
- SHA-256 content digests;
- expected output identities;
- verified artifact-set digests;
- execution request fields that change results.

Validation-only evidence:

- PE MVID and assembly metadata details already represented by the PE digest;
- PDB/receipt attestation kind;
- resolved symlink target and containment result;
- searched candidate paths and discovered competing configurations/TFMs;
- build/restore exit category.

Display-only evidence:

- absolute local paths;
- timestamps and file sizes;
- exact local command rendering;
- elapsed timings;
- host OS/runtime descriptions.

Intentionally excluded:

- process id, thread id, random snapshot handle, current working directory;
- terminal/TTY/color state;
- output destination paths;
- GitHub Actions or other CI-provider identifiers;
- credentials, environment secrets, and raw untrusted command strings.

Machine-readable diagnostics and SARIF use repository-relative paths where possible. Absolute paths are omitted by default from JSON/SARIF; human output may show a local absolute path when needed for an actionable command, clearly as evidence rather than identity.

### 11. Downstream issues consume the contract without redefining it

- **#362** implements project evaluation, preflight categories, diagnostics, ordinary/ensure-built/no-restore behavior, attestation/receipt verification, and post-build re-verification.
- **#363** constructs exactly one immutable snapshot after successful preflight and exposes completed session/snapshot identity and ownership.
- **#365** uses the completed session fingerprint as an input to cache keys, while adding its own cache schema and trust-domain controls. Fingerprint equality alone is not cache authorization.
- **#366** adds cross-platform fixtures for portable logical identity, all failure categories, copied/mismatched artifacts, offline/no-restore, and cancellation.
- **#374** measures evaluation, hashing, verification, restore/build, and snapshot phases without making timings part of identity.
- **#375** propagates cancellation through evaluation/build/hash/snapshot construction and forbids publication of successful partial identities.

## Risks / Trade-offs

- **Portable PDB evidence is not available for every build.** Fail closed as `unverifiable-artifact` and recommend explicit ensure-built preparation that emits a receipt.
- **Hashing source and artifact sets costs I/O.** #374 must measure it; #365 may cache only after the trust/invalidation design is approved. Timestamps may optimize candidate selection but never replace hashes/attestation.
- **MSBuild input surfaces evolve.** The fingerprint envelope is versioned. Adding a newly recognized analysis-affecting field requires a version/schema change or an explicitly compatible extension; silently changing v1 equality is forbidden.
- **Nondeterministic builds can produce different artifact fingerprints from the same logical inputs.** This is correct for exact reuse; logical equivalence remains separately visible.
- **MSBuild evaluation/build can execute repository-controlled behavior.** The product documents the trust boundary and never lets policy YAML opt into execution. This is not a sandbox.
- **Strict containment rejects some external linked source layouts.** v1 chooses fail-closed portability/security. A future version may add typed external-input declarations rather than accepting absolute paths implicitly.

## Migration Plan

No existing public data is migrated by this design-only change.

1. Merge and archive this OpenSpec slice after review.
2. Implement #362 against `analysis-build-state/v1`, including typed diagnostics and receipts/equivalent evidence.
3. Implement #363 snapshot ownership using the completed session fingerprint.
4. Update public CLI/Testing/diagnostic schemas and the capability manifest only when implementation exists.
5. Add cache/profiling/cancellation behavior in their own issues without changing v1 identity semantics silently.

Rollback before implementation is documentation-only. After downstream implementation ships, changing the fingerprint contract requires a versioned compatibility decision under #355.

## Open Questions

- None blocking for v1. Exact public type names, command grouping, and serialized diagnostic field names belong to #362/#363, but they must preserve the categories, field roles, and identity semantics defined here.
