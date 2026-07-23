# Analysis and Build-State Blueprint

This internal blueprint is the implementation reference for the **Analysis and build state** slice in #355, specified by #387. It is authoritative for #362 (preflight/preparation), #363 (immutable snapshot), #365 (verified cache), #366 (acceptance corpus), #374 (profiling), and #375 (cancellation).

The normative requirements live in the `analysis-build-state-fingerprints` OpenSpec capability. This document explains component boundaries, field roles, and implementation sequencing without advertising unimplemented behavior as a current public capability.

## Core rule

A validation run may execute contracts only from one immutable snapshot whose complete selected project graph has been evaluated and whose expected artifacts are proven current for the selected configuration, target framework, platform, and optional runtime identifier.

Ordinary validation never restores or builds implicitly. Explicit preparation is caller-controlled, and policy YAML never controls executable paths or arguments.

## Identity model

```text
ProjectEvaluationFingerprint(s)
        + selected graph
        + source/project/import/compiler/reference inputs
        = EffectiveBuildInputFingerprint

EffectiveBuildInputFingerprint
        + effective policy/configuration provenance
        + condition set / requested analysis semantics
        = EffectiveAnalysisInputFingerprint

ExpectedBuildOutputIdentity(s)
        + verified PE/PDB/receipt/dependency digests
        = VerifiedArtifactSetFingerprint

EffectiveAnalysisInputFingerprint
        + VerifiedArtifactSetFingerprint
        + execution request
        + tool analysis-semantics version
        = CompletedAnalysisSessionFingerprint

CompletedAnalysisSessionFingerprint
        + process-local owned object handle
        = CLI/Testing snapshot instance
```

This separation is load-bearing:

- changing architecture policy invalidates session/snapshot/cache identity but does not make an unchanged assembly stale;
- changing selected source, project, or relevant imported build content invalidates the build-input fingerprint and therefore prior artifact verification;
- two checkout roots can share portable build/analysis identity;
- two nondeterministic builds can share build/analysis inputs while having different exact artifact/session identity.

## Canonical envelope

- Envelope id: `analysis-build-state/v1`.
- Digest: SHA-256, lowercase hexadecimal.
- Serialization: canonical UTF-8 JSON.
- Object properties: ordinal lexicographic order.
- Set-like arrays: sorted by a declared canonical key.
- Semantically ordered arrays: preserved and explicitly marked as ordered.
- Repository paths: repository-relative, `/` separators, ordinal spelling/equality.
- External SDK/package/framework inputs: typed logical coordinates, never raw machine paths.

Changing an equality-affecting field requires a versioned compatibility decision. Implementations must not silently reinterpret v1.

## Field-role matrix

| Field/evidence | Build identity | Analysis/session identity | Validation evidence | Display only |
|---|---:|---:|---:|---:|
| Envelope/schema version | yes | yes | yes | yes |
| Tool analysis-semantics version | where build-affecting | yes | yes | yes |
| Repository-relative project/source/import path | yes | yes | yes | yes |
| Absolute checkout/output path | no | no | yes | yes |
| Configuration / platform / TFM / RID | yes | yes | yes | yes |
| Project graph and canonical edges | yes | yes | yes | yes |
| Raw relevant project/import/source content digest | yes | yes | yes | optional |
| Effective compiler/MSBuild manifest | yes | yes | yes | summarized |
| Policy/configuration provenance/content digest | no | yes | yes | summarized |
| Condition set / requested result-affecting view | no | yes | yes | yes |
| Expected assembly/output identity | yes | yes | yes | yes |
| PE/PDB/reference/dependency SHA-256 | no | yes | yes | optional |
| PE MVID / assembly metadata details | no | no | yes | optional |
| Receipt/compiler-attestation kind | no | no | yes | yes |
| Timestamp and file size | no | no | supporting only | yes |
| Searched candidate paths | no | no | yes | yes |
| Build/restore exit category | no | no | yes | yes |
| Timings | no | no | no | yes |
| Process-local snapshot handle | no | no | ownership only | optional |
| CI provider, TTY, color state | no | no | no | no |
| Output destination path | no | no | no | yes |

## Project/build manifest v1

Each selected project/configuration/TFM/platform/RID contributes one canonical manifest containing at least:

- canonical project key and selected project-reference graph edges;
- selected configuration, platform, TFM, and optional RID;
- raw content digests for the project file and every relevant imported MSBuild file;
- SDK identity/version and effective global properties that affect evaluation/build;
- assembly name, output type, target name/extension, expected output role, reference-assembly behavior;
- compile/generated source items and content digests;
- analyzer configs, additional files, analyzers, source generators, and their identities/digests;
- defines, language version, nullable, unsafe, checked, optimization, deterministic/path-map, and other output/semantic-analysis-affecting compiler options;
- project, package, framework, SDK/reference-pack, and metadata-reference identities/digests.

Any selected source, project, or relevant imported-build-input content change invalidates the build-input fingerprint. v1 intentionally does not attempt to prove that a textual `.csproj` or imported-props/targets change was semantically harmless.

## Analysis manifest v1

The analysis-input fingerprint adds to the build-input fingerprint:

- root policy and imported-fragment provenance/content digests;
- effective composed configuration;
- selected condition set;
- requested result-affecting validation/baseline/coverage views;
- tool, policy-schema, diagnostic-schema, and analysis-semantics versions that can change results.

Policy-only changes therefore invalidate analysis/session identity but do not change artifact freshness.

## Expected output identity

Each project evaluation produces expected logical outputs identified by:

- canonical project key;
- assembly name;
- output kind;
- configuration;
- TFM;
- platform;
- optional RID;
- canonical output role/path where portable;
- whether reference assembly, PDB, deps file, or runtime config is expected.

Absolute paths are retained only as local diagnostic/search evidence.

## Authoritative verification

### ArchLinterNet build receipt

Explicit ensure-built preparation emits a v1 receipt binding:

- effective build-input fingerprint;
- expected output identities;
- PE/PDB/reference/dependency content digests;
- configuration/TFM/RID;
- preparation category and receipt schema.

A receipt is untrusted data until every digest, identity, repository scope, and trust-boundary check succeeds.

### Equivalent compiler-produced evidence

An ordinary build may be accepted without a product receipt only when the actual PE plus portable PDB/compiler records contain enough supported evidence to compare:

- document checksums to current source/generated inputs;
- compilation options to the current compiler manifest;
- metadata-reference identities to current resolved references;
- target/output identity and PE/PDB association;
- exact PE/PDB SHA-256 digests.

Portable PDB presence alone is insufficient. Missing, stripped, unsupported, or incomplete compilation-option/reference records produce `unverifiable-artifact`, not a timestamp-based pass.

PE metadata contributes assembly name, target-framework attribute, references, MVID, and debug-directory association. MVID is validation evidence; content digests are exact identity.

## Preflight state machine

Primary state precedence:

1. `cancelled`
2. prerequisite failure / `restore-required`
3. `missing-artifact`
4. `wrong-configuration`
5. `wrong-target-framework`
6. `wrong-project-output`
7. `inconsistent-dependency-artifact`
8. `stale-artifact`
9. `unverifiable-artifact`
10. `current`

The complete graph is checked before any contract executes. A project may carry secondary evidence, but exactly one stable primary state is emitted.

## Preparation boundary

Ordinary validation may evaluate, hash, inspect prerequisites, and verify. It may not restore, compile, invoke build targets, call a hook, or access the network for preparation.

Ensure-built is explicit and must:

1. evaluate the selected graph;
2. construct a supported executable + argv invocation;
3. build the graph once rather than per contract;
4. preserve `--no-restore` when requested;
5. stop distinctly on restore/build/cancellation failure;
6. emit or obtain authoritative build attestation;
7. re-evaluate and re-verify post-build;
8. create the immutable snapshot only from verified post-build artifacts.

An optional caller hook may be accepted only from trusted CLI/application configuration as executable + argv. Never use a shell command string. Never read executable/argument data from policy, imports, baseline, snapshot, receipt, or cache content.

## Snapshot ownership

The snapshot owns or immutably references:

- effective policy and provenance;
- selected graph/configuration/TFM/RID;
- build-input and analysis-input manifests;
- verified artifacts and dependency metadata;
- type/reference/source/coverage facts and lazily materialized indexes;
- completed session fingerprint;
- cancellation/disposal state.

CLI owns one snapshot per command. `ArchLinterNet.Testing` may expose an explicitly owned snapshot for multiple compatible assertions. Reuse requires identical completed-session fingerprint, compatible views, and an undisposed successful snapshot.

Cancelled, failed, partial, changed-during-materialization, or disposed snapshots are never reusable or cacheable.

## TOCTOU rule

After verification, analysis consumes retained immutable bytes/metadata or re-hashes inputs while materializing the snapshot. A digest/identity change between verification and materialization aborts the session. Successful partial analysis is forbidden.

## Diagnostics and privacy

Every preflight diagnostic carries typed project/configuration/TFM state and expected-versus-observed evidence. Human output may include an actionable local absolute path or rendered command. JSON and SARIF prefer repository-relative or typed logical paths and omit absolute paths by default.

Credentials, environment secrets, raw command strings from untrusted content, and unrelated environment values are excluded from fingerprints and diagnostics.

## Security/trust boundary

- Repository paths are normalized and containment-checked; escaping symlinks/junctions fail closed in v1.
- Fingerprint equality is not cache authorization. #365 must add cache schema and workspace/trust-domain controls.
- Receipts and cache entries are untrusted until verified.
- MSBuild evaluation/build is not a sandbox. Explicit ensure-built means the user trusts the repository to build.
- Structured argv prevents shell injection; typed placeholders prevent policy-controlled argument injection.
- Policy, baseline, snapshot, receipt, and cache documents never grant build-execution permission.

## Downstream implementation map

| Issue | Must implement/reuse |
|---|---|
| #362 | Project/build/analysis fingerprints, preflight states, diagnostics, ordinary/ensure-built/no-restore behavior, receipt/compiler evidence, post-build verification |
| #363 | One immutable completed snapshot, session identity publication, ownership/disposal/reuse |
| #365 | Cache-key input from completed-session fingerprint plus independent cache schema/trust controls |
| #366 | Cross-platform/adversarial fixtures for every state, path portability, copied artifacts, source/project/import changes, offline/no-restore, cancellation |
| #374 | Timings/counters for evaluation, hashing, verification, restore/build, materialization; never identity fields |
| #375 | Cancellation through every phase and prohibition of reusable partial state |

## Public-surface timing

This blueprint does not add a current product capability. Public CLI spelling, diagnostic JSON/SARIF schema, Testing types, capability manifest, and user guidance are updated by implementation issues only when executable behavior exists.
