## Context

Issue #355 requires an approved **Analysis and build state** slice before #362 can implement deterministic preflight and explicit build preparation. The same identity model must also be consumed by #363 (immutable snapshot), #365 (verified cache), #366 (acceptance corpus), #374 (profiling), and #375 (cancellation).

The repository already has project discovery, MSBuild-backed Roslyn context resolution, assembly probing, and per-run session state, but it does not define:

- portable build-input identity;
- policy-dependent analysis-input identity;
- expected output identity for a selected project/configuration/TFM/RID;
- evidence that an existing artifact was built from the current build inputs;
- exact artifact identity for snapshot/cache reuse;
- process-local snapshot ownership;
- fail-closed build-state categories.

One hash cannot safely represent all of these concerns. In particular, changing an architecture policy must invalidate an analysis snapshot but must not make an unchanged assembly stale. Conversely, changing source, project, or relevant imported build content must invalidate previously verified artifacts even when timestamps appear current.

## Goals / Non-Goals

**Goals:**

- Define versioned, portable build and analysis fingerprint layers before implementation.
- Separate build freshness from policy-dependent analysis identity.
- Include configuration, TFM, RID, project graph, source/project/import content, compiler inputs, policy/configuration, and verified artifacts explicitly.
- Reject missing, stale, mismatched, inconsistent, or unverifiable artifacts before any contract executes.
- Keep ordinary validation free of implicit build, restore, or network preparation.
- Keep policy YAML unable to start or parameterize build execution.
- Define equivalent CLI and `ArchLinterNet.Testing` snapshot semantics.
- Give downstream issues one contract that they must reuse rather than redefine.

**Non-Goals:**

- Implementing preflight, build orchestration, cache, profiling, or cancellation.
- Replacing MSBuild or defining non-.NET build orchestration.
- Treating MSBuild evaluation/build as a sandbox for untrusted repositories.
- Publishing new public CLI/API/diagnostic behavior before #362/#363 implement it.
- Treating timestamps or file size as proof of freshness.

## Decisions

### 1. Use a versioned canonical envelope

Every persisted or machine-readable fingerprint uses envelope id `analysis-build-state/v1`, SHA-256, lowercase hexadecimal digests, and canonical UTF-8 JSON. Object properties are serialized in ordinal lexicographic order. Set-like arrays are sorted by a declared canonical key; semantically ordered arrays retain their effective order.

Repository paths use repository-relative `/`-separated keys. Absolute checkout paths, drive letters, UNC prefixes, current working directory, host temp paths, and CI-provider identifiers are excluded from stable identity.

Changing or reinterpreting an equality-affecting field requires a new versioned envelope or an explicitly compatible extension. Implementations must not silently change v1 equality.

### 2. Define seven identity layers

1. **Project evaluation fingerprint** — one selected project under one configuration/TFM/platform/RID, including the selected effective MSBuild/compiler manifest and raw content digests for relevant project/import inputs.
2. **Effective build-input fingerprint** — the selected project graph plus all project-evaluation fingerprints, source/generated/analyzer/reference input identities and content digests required to prove whether outputs are current.
3. **Effective analysis-input fingerprint** — the build-input fingerprint plus effective policy/configuration provenance and content, selected condition set, requested analysis views, and analysis-affecting tool/schema versions.
4. **Expected build-output identity** — project key, assembly name, output kind, configuration, TFM, platform, optional RID, and canonical output role/path.
5. **Verified artifact fingerprint** — expected output identity plus exact SHA-256 digests for PE, associated PDB or receipt, reference assembly where applicable, and required first-party dependency artifacts.
6. **Completed analysis-session fingerprint** — analysis-input fingerprint plus verified artifact-set fingerprint, execution request, and tool analysis-semantics version. It is published only after successful full preflight and snapshot construction.
7. **Testing snapshot handle** — an opaque process-local ownership identifier for one snapshot instance. It is never serialized, persisted, or used as a cache key.

Two machines with equivalent repository state can produce the same build/analysis fingerprints without identical checkout roots. Two nondeterministic builds may share build/analysis input identity but have different artifact and completed-session fingerprints.

### 3. Normalize paths portably and fail closed on ambiguity

For repository files, stable identity uses the repository-relative spelling with `/` separators and ordinal comparison/order. The implementation resolves traversal and symlink/junction containment before fingerprinting.

A selected path that escapes the repository root is `unverifiable` unless a future version introduces an explicit typed external-input contract. A symlink identity includes its repository-relative link path, normalized link-target text, and resolved content digest. Case aliases that map to one host file are rejected instead of silently collapsed.

Legitimate SDK/package/framework inputs outside the repository use typed coordinates such as SDK identity/version, NuGet package id/version/content hash, framework-reference name/version, or assembly identity/content hash rather than machine-local absolute paths.

### 4. Build-input identity is conservative and content based

The v1 project/build manifest includes at minimum:

- canonical selected project graph and project-reference edges;
- configuration, platform, target framework, and optional RID;
- raw content digests for the selected project file and every relevant imported MSBuild file;
- assembly name, output type, target name/extension, expected output role, reference-assembly behavior, deterministic/debug settings;
- effective compiler options that can affect emitted code or semantic analysis, including defines, language version, nullable, unsafe, checked, optimization, deterministic/path-map, and relevant warning/error settings;
- canonical compile/generated source items and their content digests;
- analyzer configs, additional files, analyzers, source generators, and their identities/digests;
- project, package, framework, SDK, reference-pack, and metadata-reference identities/digests relevant to the selected build.

Any selected source content change, project-file change, or relevant imported-build-input change changes the build-input fingerprint and invalidates prior artifact verification. This intentionally favors fail-closed correctness over trying to prove that a textual project/import edit was semantically harmless.

Environment variables and global properties are included when they affect project evaluation or build output. Secrets and unrelated environment values are never fingerprinted or displayed.

### 5. Analysis identity adds policy without making the assembly stale

The effective analysis-input fingerprint includes the effective build-input fingerprint plus:

- root policy and imported-fragment provenance and content digests;
- effective composed configuration;
- selected condition set;
- requested validation/baseline/coverage views whose choice changes results;
- tool, policy-schema, diagnostic-schema, and analysis-semantics versions that can change results.

A policy-only change therefore invalidates session/snapshot/cache identity but does not change artifact freshness. Artifact attestation is compared against the build-input fingerprint, never the policy-dependent analysis-input fingerprint.

### 6. Artifact verification requires authoritative attestation

An artifact is current only when the current effective build-input fingerprint is proven by one of these supported attestations:

1. **ArchLinterNet build receipt v1** emitted after successful explicit preparation. It records the build-input fingerprint, expected output identities, exact output digests, and preparation category.
2. **Equivalent supported compiler-produced evidence** for ordinary builds. For v1 this may be a matching PE plus portable PDB/compiler records that actually contain enough supported evidence to compare current document checksums, compilation options, metadata-reference identities, target/output metadata, and PE/PDB association.

Portable PDB presence alone is not sufficient. If the required compilation-option/reference records are absent, stripped, unsupported, or incomplete, the state is `unverifiable-artifact` and explicit preparation is recommended.

PE metadata supplies assembly name, target-framework attribute, assembly references, MVID, and debug-directory association. MVID is validation evidence; PE/PDB SHA-256 digests are exact artifact identity.

Timestamp and file size may support diagnostics or candidate selection, but they never establish freshness and never participate in stable equality.

### 7. Build-state categories have deterministic precedence

Preflight evaluates the complete selected graph before contract execution. Each project gets one stable primary category using this precedence:

1. `cancelled`;
2. prerequisite failure / `restore-required`;
3. `missing-artifact`;
4. `wrong-configuration`;
5. `wrong-target-framework`;
6. `wrong-project-output`;
7. `inconsistent-dependency-artifact`;
8. `stale-artifact`;
9. `unverifiable-artifact`;
10. `current`.

Secondary evidence may be attached without changing the primary category. No contract executes unless every selected project is `current`.

Examples:

- an output found only for another evaluated configuration is `wrong-configuration`;
- mismatched target-framework metadata is `wrong-target-framework`;
- a copied same-named assembly with the wrong project/output association is `wrong-project-output`;
- a first-party reference that does not match the selected dependency graph is `inconsistent-dependency-artifact`;
- attestation that proves different source/project/import/compiler inputs is `stale-artifact`;
- insufficient evidence to compare is `unverifiable-artifact`.

### 8. Preparation is explicit, structured, and outside policy YAML

Ordinary validation performs evaluation, fingerprinting, prerequisite inspection, and verification only. It never runs restore, build/compile targets, a caller hook, or network-dependent preparation.

Explicit ensure-built preparation:

- is opt-in from the trusted CLI/application caller;
- resolves the selected graph/configuration/TFM/RID first;
- invokes one supported graph-level build operation rather than building per contract;
- uses an executable plus structured argv, never a shell command string;
- stops distinctly on restore failure, build failure, cancellation, or incomplete evidence;
- emits or validates authoritative attestation;
- re-evaluates and re-verifies after build;
- analyzes only verified post-build artifacts.

`--no-restore` is independent. Without ensure-built it forbids prerequisite recovery that would require restore/network. With ensure-built it constrains the structured build invocation and never silently retries with restore.

An optional caller build hook may be accepted only from trusted CLI/application configuration as executable + argv. Typed placeholders may expand selected solution/project, configuration, TFM, or RID into individual argv values. Policy YAML, imported fragments, baselines, snapshots, receipts, and cache entries can never provide executable paths or arguments.

### 9. Offline behavior is deterministic

Ordinary validation requires no network access. A prepared checkout with local SDK/reference packs, restored assets, and verifiable current artifacts can validate offline. Ensure-built without `--no-restore` may invoke restore and therefore may require network access; that boundary is stated before analysis. Ensure-built with `--no-restore` never falls back to restore.

MSBuild evaluation/build operates inside the repository trust boundary and is not a sandbox. Explicit ensure-built is a user decision to build the repository.

### 10. Snapshot publication closes the TOCTOU window

Verification and analysis use one immutable snapshot. After verification, the implementation retains immutable bytes/metadata or re-hashes inputs while materializing the snapshot. Any change between verification and materialization aborts the session.

The completed session fingerprint is published only after:

- project evaluation and prerequisite checks succeed;
- every selected artifact and dependency is current and verified;
- immutable inputs/indexes required by the snapshot are materialized or safely owned for lazy materialization;
- cancellation has not been requested.

CLI owns one snapshot per command. `ArchLinterNet.Testing` may expose an explicitly owned snapshot for multiple compatible assertions. Reuse requires the same completed session fingerprint, an undisposed successful snapshot, and compatible requested views. Cancelled, failed, disposed, changed, or partial snapshots are never reusable or cacheable.

### 11. Stable identity and evidence are separate

Stable build/analysis/session equality includes:

- envelope/schema/tool analysis-semantics versions;
- canonical repository-relative or typed logical coordinates;
- configuration/TFM/platform/RID;
- raw relevant project/import/source/analyzer/reference content digests;
- canonical effective project/compiler/policy manifests;
- expected output identities;
- verified artifact-set digests;
- execution request fields that change results.

Validation-only evidence includes PE MVID/metadata details, attestation kind, containment result, searched candidates, competing configurations/TFMs, and build/restore exit category.

Display-only evidence includes absolute paths, timestamps, file sizes, exact local command rendering, elapsed timings, and host descriptions.

Process/thread ids, random snapshot handles, current working directory, TTY/color state, output destinations, CI-provider identifiers, credentials, secrets, and raw untrusted command strings are excluded.

JSON/SARIF prefer repository-relative or typed logical locations and omit absolute paths by default. Human output may show a local absolute path when needed for an actionable command, clearly as evidence rather than identity.

### 12. Downstream issues consume this contract

- **#362** implements fingerprints, preflight categories, diagnostics, ordinary/ensure-built/no-restore behavior, receipts/compiler evidence, and post-build re-verification.
- **#363** creates exactly one immutable snapshot after successful preflight and exposes completed session identity and ownership.
- **#365** uses the completed session fingerprint as a cache-key input while adding an independent cache schema and trust-domain controls. Fingerprint equality alone is not cache authorization.
- **#366** adds cross-platform/adversarial fixtures for portability, all state categories, copied artifacts, source/project/import changes, offline/no-restore, and cancellation.
- **#374** measures evaluation, hashing, verification, restore/build, and snapshot phases without making timings identity fields.
- **#375** propagates cancellation through every phase and forbids successful partial identity publication.

## Risks / Trade-offs

- **Compiler evidence is not universally available.** Fail closed as `unverifiable-artifact`; explicit preparation can emit a receipt.
- **Hashing all relevant inputs costs I/O.** #374 must measure it; #365 may optimize only after its cache trust/invalidation design is approved. Timestamps may optimize candidate selection but never replace content identity.
- **MSBuild input surfaces evolve.** Adding a newly recognized equality-affecting field requires versioning or explicit compatibility handling.
- **Nondeterministic builds produce different artifact/session fingerprints.** This is correct for exact reuse; build/analysis input equivalence remains separately visible.
- **MSBuild evaluation/build can execute repository-controlled behavior.** The trust boundary must be documented; policy YAML never opts into execution.
- **Strict containment rejects some linked external-source layouts.** v1 chooses fail-closed portability/security; a future version may add typed external-input declarations.

## Migration Plan

No existing public data is migrated by this design-only change.

1. Archive and publish this OpenSpec capability in the same PR after review of the design slice.
2. Implement #362 against `analysis-build-state/v1`.
3. Implement #363 snapshot ownership using the completed session fingerprint.
4. Update public CLI/Testing/diagnostic schemas and capability manifest only when executable behavior exists.
5. Add cache/profiling/cancellation behavior in their own issues without silently changing v1 equality.

After downstream implementation ships, changing the fingerprint contract requires a versioned compatibility decision under #355.

## Open Questions

- None blocking for v1. Exact public type names, command grouping, and serialized diagnostic field names belong to #362/#363, but they must preserve the categories, field roles, and equality semantics defined here.
