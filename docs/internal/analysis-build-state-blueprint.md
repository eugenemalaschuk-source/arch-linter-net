# Analysis and Build-State Blueprint

This internal blueprint is the implementation reference for the **Analysis and build state** design slice in #355, specified by #387. It is authoritative for #362 (preflight/preparation), #363 (immutable snapshot), #365 (verified cache), #366 (acceptance corpus), #374 (profiling), and #375 (cancellation).

The normative requirements live in the `analysis-build-state-fingerprints` OpenSpec capability. This document explains the intended component boundaries, field roles, and implementation sequence without advertising unimplemented behavior as a current public capability.

## Core rule

A validation run may execute contracts only from one immutable snapshot whose complete selected project graph has been evaluated and whose expected artifacts are proven current for the selected configuration, target framework, platform, and optional runtime identifier.

No implicit restore or build is permitted during ordinary validation. Explicit preparation is caller-controlled and policy YAML never controls executable paths or arguments.

## Identity model

```text
ProjectEvaluationFingerprint(s)
        + effective policy/configuration/source inputs
        + selected graph / condition set
        = LogicalAnalysisInputFingerprint

ExpectedBuildOutputIdentity(s)
        + verified PE/PDB/receipt/dependency digests
        = VerifiedArtifactSetFingerprint

LogicalAnalysisInputFingerprint
        + VerifiedArtifactSetFingerprint
        + execution request
        + tool analysis-semantics version
        = CompletedAnalysisSessionFingerprint

CompletedAnalysisSessionFingerprint
        + process-local owned object handle
        = Testing/CLI snapshot instance
```

The logical fingerprint is portable across checkout roots. The artifact/session fingerprint is exact: two nondeterministic builds may share logical identity while having different artifact and session identity.

## Canonical envelope

- Envelope id: `analysis-build-state/v1`.
- Digest: SHA-256, lowercase hexadecimal.
- Serialization: canonical UTF-8 JSON.
- Object properties: ordinal lexicographic order.
- Set-like arrays: sorted by the documented canonical key.
- Semantically ordered arrays: preserved and explicitly marked as ordered.
- Repository paths: repository-relative, `/` separators, ordinal spelling/equality.
- External SDK/package/framework inputs: typed logical coordinates, never raw machine paths.

Changing an equality-affecting field requires a versioned compatibility decision. Implementations must not silently reinterpret v1.

## Field-role matrix

| Field/evidence | Stable logical identity | Exact artifact/session identity | Validation evidence | Display only |
|---|---:|---:|---:|---:|
| Envelope/schema version | yes | yes | yes | yes |
| Tool analysis-semantics version | yes | yes | yes | yes |
| Repository-relative project/source/import path | yes | yes | yes | yes |
| Absolute checkout/output path | no | no | yes | yes |
| Configuration / platform / TFM / RID | yes | yes | yes | yes |
| Project graph and canonical edges | yes | yes | yes | yes |
| Effective compiler/MSBuild manifest | yes | yes | yes | summarized |
| Policy/configuration provenance and content digest | yes | yes | yes | summarized |
| Source/import/analyzer/reference content digest | yes | yes | yes | optional |
| Expected assembly/output identity | yes | yes | yes | yes |
| PE/PDB/reference-assembly/dependency SHA-256 | no | yes | yes | optional |
| PE MVID / assembly metadata details | no | no | yes | optional |
| Portable PDB/receipt attestation kind | no | no | yes | yes |
| Timestamp and file size | no | no | supporting only | yes |
| Searched candidate paths | no | no | yes | yes |
| Build/restore exit category | no | no | yes | yes |
| Timings | no | no | no | yes |
| Process-local snapshot handle | no | no | ownership only | optional |
| CI provider, TTY, color state | no | no | no | no |
| Output destination path | no | no | no | yes |

## Project-evaluation manifest v1

Each selected project/configuration/TFM/platform/RID contributes one canonical manifest containing at least:

- project key and selected project-reference graph edges;
- selected configuration, platform, TFM, and optional RID;
- relevant project and imported MSBuild file content digests;
- SDK identity/version and effective global properties that affect evaluation;
- assembly name, output type, target name/extension, expected output role, reference-assembly behavior;
- compile/generated compile items and source digests;
- analyzer config, additional files, analyzers, source generators, and their typed identities/digests;
- defines, language version, nullable, unsafe, checked, optimization, deterministic/path-map, and other effective compiler options that can change emitted code or semantic analysis;
- project, package, and framework reference manifests with canonical identities and resolved analysis-affecting content.

A raw project/import edit that does not alter the effective manifest is not stale merely because the file timestamp changed. Any effective input change must change the manifest fingerprint.

## Expected output identity

Each selected project evaluation produces one or more expected logical outputs. The primary analyzed assembly identity includes:

- canonical project key;
- assembly name;
- output kind;
- configuration;
- TFM;
- platform;
- optional RID;
- canonical output role/path relative to the repository/build root where portable;
- whether a reference assembly, PDB, deps file, or runtime config is expected.

Absolute paths are retained only as local diagnostic/search evidence.

## Authoritative verification

### ArchLinterNet build receipt

Explicit ensure-built preparation emits a v1 receipt after a successful build. The receipt binds:

- effective logical input fingerprint;
- expected output identities;
- PE/PDB/reference/dependency content digests;
- selected configuration/TFM/RID;
- preparation category and tool receipt schema.

A receipt is data, not authority by itself: it is accepted only inside the current repository/cache trust boundary and only when all recorded digests and identities match current files.

### Equivalent compiler-produced evidence

Ordinary modern .NET SDK builds can be accepted without a product receipt when supported evidence proves the same relationship. The v1 target is PE + portable PDB evidence sufficient to compare:

- PDB document checksums to current compile/generated inputs;
- PDB compilation options to the current compiler manifest;
- PDB metadata-reference identities to current resolved references;
- PE/PDB debug-directory association;
- PE assembly name, target-framework attribute, references, and MVID;
- exact PE/PDB SHA-256 digests.

If evidence is absent, stripped, unsupported, or incomplete, report `unverifiable-artifact`. Do not downgrade to timestamp freshness.

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

The full graph is checked before any contract executes. A project may carry secondary evidence, but exactly one stable primary state is emitted.

## Preparation boundary

Ordinary validation may evaluate, hash, and verify. It may not restore, compile, invoke build targets, call a hook, or access the network for preparation.

Ensure-built is explicit and must:

1. evaluate the selected graph;
2. construct a supported executable + argv invocation;
3. build the selected graph once rather than per contract;
4. preserve `--no-restore` when requested;
5. stop distinctly on restore/build/cancellation failure;
6. emit or obtain authoritative attestation;
7. re-evaluate and re-verify post-build;
8. create the immutable snapshot only from verified post-build artifacts.

An optional caller hook may be accepted only from trusted CLI/application configuration as executable + argv. Never use a shell command string. Never read executable/argument data from policy YAML, imports, baseline, snapshot, or cache content.

## Snapshot ownership

The snapshot owns or immutably references:

- effective policy and provenance;
- selected graph/configuration/TFM/RID;
- project-evaluation and logical input manifests;
- verified artifacts and dependency metadata;
- type/reference/source/coverage facts and lazily materialized indexes;
- completed session fingerprint;
- cancellation/disposal state.

CLI owns one snapshot for one command and disposes it at command completion. `ArchLinterNet.Testing` may expose an explicitly owned snapshot for multiple compatible assertions. Reuse requires an identical completed session fingerprint, compatible views, and an undisposed successful snapshot.

Cancelled, failed, partial, changed-during-materialization, or disposed snapshots are never reusable or cacheable.

## TOCTOU rule

After verification, analysis must consume immutable bytes/metadata already retained by the snapshot or re-hash inputs while materializing them. A digest/identity change between verification and materialization aborts the session. Successful partial analysis is forbidden.

## Diagnostics and privacy

Every preflight diagnostic carries typed project/configuration/TFM state and expected versus observed evidence. Human output may include an actionable local absolute path or rendered command. JSON and SARIF prefer repository-relative or typed logical paths and omit absolute paths by default.

Do not include credentials, environment secrets, raw command strings from untrusted content, or unrelated environment variables in fingerprints or diagnostics.

## Security/trust boundary

- Repository paths are normalized and containment-checked; escaping symlinks/junctions fail closed in v1.
- Fingerprint equality is not cache authorization. #365 must add a cache schema and workspace/trust-domain boundary.
- Build receipts and cache entries are untrusted data until their digests/identity/trust scope are verified.
- MSBuild evaluation/build is not a sandbox. Explicit ensure-built means the user trusts the repository to build.
- Structured argv prevents shell injection; typed placeholders prevent policy-controlled argument injection.
- Policy, baseline, snapshot, and cache documents never grant permission to execute a build.

## Downstream implementation map

| Issue | Must implement/reuse |
|---|---|
| #362 | Evaluation manifests, preflight states, diagnostics, ordinary/ensure-built/no-restore behavior, receipt/compiler evidence, post-build verification |
| #363 | One immutable completed snapshot, session identity publication, ownership/disposal/reuse |
| #365 | Cache key input from completed session fingerprint plus independent cache schema/trust controls |
| #366 | Cross-platform and adversarial acceptance fixtures for every state and portability rule |
| #374 | Timings/counters for evaluation, hashing, verification, restore/build, materialization; never identity fields |
| #375 | Cancellation through every phase and prohibition of reusable partial state |

## Public-surface timing

This blueprint does not add a current product capability. Public CLI spelling, diagnostic JSON/SARIF schema, Testing types, capability manifest, and user guidance are updated by implementation issues when executable behavior exists. Until then, the current capability manifest remains unchanged to avoid claiming support that is not shipped.
