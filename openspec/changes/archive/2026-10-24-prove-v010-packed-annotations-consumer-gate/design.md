## Context

The release package manifest already supports schema v3 for stable 0.10.x candidates. It contains the four existing packages with symbols plus `ArchLinterNet.Annotations` without a symbol package. Checkpoint B currently assumes the v2 four-package inventory, while `AnnotationsPackageConsumerTests` packs and restores the annotation package separately from the candidate CLI, Core, Testing, schema, and capability artifacts.

Checkpoint B is the release authority for the immutable candidate. The v0.10 annotation proof therefore belongs in its existing manifest-bound consumer path. The annotation role, metadata, compatibility, and policy semantics are already specified; this change adds release evidence for those contracts.

## Goals / Non-Goals

**Goals:**

- Load both existing v2 candidate manifests and stable v0.10 v3 manifests, preserving package identities, versions, and digests; accept the annotations package without requiring a symbol package.
- Exercise the exact manifest-bound package set in an external synthetic consumer matrix on each required Checkpoint B platform.
- Require and report the annotation scenario whenever the candidate manifest includes `ArchLinterNet.Annotations`.
- Preserve the four-package v2 scenario inventory for older and preview candidates.

**Non-Goals:**

- Changing canonical annotation roles, context metadata, conflict rules, or tool compatibility semantics.
- Publishing packages, creating a stable release declaration, or changing the current preview scope.
- Adding real adopter identities or repository topology to evidence.

## Decisions

### Candidate inventory follows the immutable manifest

The .NET candidate feed will accept v2 and v3 manifests and derive the package inventory from the validated manifest. v2 keeps its four package records with package and symbol subjects. v3 accepts the stable 0.10.x five-package inventory and the annotation package's package-only record. Locally packed candidates will apply the same version-to-inventory rules as `tools/release/package_manifest.py`.

This keeps candidate loading and package checks aligned with the existing release package authority. Maintaining a separate Checkpoint B package list would drift again; treating the annotation package as if it had a symbol subject would reject its reviewed package shape.

### Add annotation proof to the existing package-and-entrypoints shard

The `package-and-entrypoints` Checkpoint B shard will execute one annotation consumer scenario when its candidate includes `ArchLinterNet.Annotations`. The scenario will consume the feed created from that candidate and emit its outcome through the existing shard evidence writer. The platform merger and final evidence aggregator will derive the required scenario set from the candidate manifest and reject a missing or unexpected annotation outcome.

This reuses the existing cross-platform runner allocation and immutable evidence binding. A separate scenario shard would duplicate candidate setup and add a runner entry without changing the release authority boundary.

### Prove the consumer boundary with a synthetic multi-project solution

The scenario will create temporary SDK-style projects outside the repository and restore only from the candidate feed plus the configured package source needed for framework dependencies. Annotation-authoring projects will reference the packed annotations package directly. A custom-only project will use user-owned attributes and explicit YAML mappings without an annotations package; a mixed project will use both paths deliberately. No ArchLinterNet project reference will serve as evidence.

The matrix will cover plain class-library, modular/layered, DDD, CQRS/API, infrastructure/adapter, public reusable library, and supported Unity-style assembly shapes. The candidate CLI and a consumer using the packed Testing package will analyze the same synthetic semantics. Assertions will cover package/runtime/public API boundaries, canonical and equivalent custom facts, selectors and contextual contracts, deterministic role conflicts, bounded metadata, reviewed API membership, policy authority, version compatibility diagnostics, and aligned human/JSON/SARIF/Testing/explain/coverage projections.

This composes existing semantic guarantees through the actual package boundary instead of duplicating a second classifier or relying on source-linked unit fixtures.

## Risks / Trade-offs

- **The matrix adds process and restore time to a release-gate shard** → Reuse the isolated candidate feed and package cache, build one synthetic solution, and keep the scenario in the existing package-and-entrypoints shard.
- **Candidate schemas can drift from the Python package authority** → Validate the manifest schema, version-dependent package set, subject fields, filenames, sizes, and hashes in the .NET harness; retain the Python package-manifest tests.
- **Cross-platform consumer behavior can differ** → Run the required scenario on every existing Checkpoint B platform whenever the candidate contains the annotation package; encode pass evidence under the exact manifest digest.
- **The synthetic matrix could overstate Unity support** → Exercise only the repository's documented source-import and assembly-shape boundary, and record any unsupported consumer behavior as a release failure rather than inventing a package integration path.

## Migration Plan

No consumer migration is required. Existing v2 candidates retain their four-package inventory and scenario set. Stable v0.10.x candidates use v3 and must pass the additional annotation consumer scenario before Checkpoint B can authorize publication.

## Open Questions

None. The v3 package inventory and v0.10 release boundary are already defined by the package manifest and semantic annotation contract.
