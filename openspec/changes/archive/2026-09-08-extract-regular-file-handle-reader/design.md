## Context

Before this change, `RegularFileHandleReader` has two handwritten declarations:

- `src/ArchLinterNet.Core/IO/RegularFileHandleReader.cs`
- `src/ArchLinterNet.Core/IO/RegularFileHandleReader.RepositoryLocal.cs`

The strict `production-partial-type-declaration-count-does-not-increase` policy freezes that exact two-file aggregate through one reviewed waiver in `architecture/policy/audit-conventions.arch.yml`. `ArchitectureFileSystem.OpenRepositoryLocalRegularFile` is the canonical `IArchitectureEvidenceFileSystem` seam consumed by `SarifEvidenceArtifactReader`; bounded byte acquisition and SHA-256 provenance remain owned there.

## Goals / Non-Goals

**Goals:**

- Make `RegularFileHandleReader` one handwritten declaration and make repository-local opening a named, internal, non-partial responsibility.
- Retain safe repository-relative traversal: reject empty, dot, parent, and alternate-stream segments; do not follow final or ancestor links; and reject non-regular files.
- Preserve the existing public filesystem seam, exception classification, regular-file identity checks, bounded artifact acquisition, and consumer behavior.
- Prove direct repository-local behavior and retain focused bounded-read evidence.

**Non-Goals:**

- Change public APIs, external-evidence schemas, SARIF statuses, ordering, hashes, cache/build state, cancellation, or release authority.
- Add another filesystem interface, reopen evidence, rescan repositories, or replace the existing artifact-reader bounded-read ownership.
- Modify any other policy waiver or complete/archive the umbrella `decompose-god-classes` change.

## Decisions

### Keep native regular-file validation separate from repository-local traversal

`RegularFileHandleReader` remains the single, one-declaration native owner of opened-handle regular-file validation, identity inspection, and native failure classification. Source-generated interop requires that one declaration to remain `partial`; the architecture rule governs the number of handwritten declarations, not the compiler-generated interop implementation.

`RepositoryLocalRegularFileReader` is an internal, non-partial collaborator. It owns repository-root lifetime, relative-path segmentation, Unix and Windows no-follow descendant traversal, and the `RepositoryRoot` handle state. It consumes narrowly scoped internal native primitives from `RegularFileHandleReader`; it does not expose another public seam or independently interpret evidence content.

`ArchitectureFileSystem` remains the only public composition point and delegates its existing `OpenRepositoryLocalRegularFile` method to the collaborator. The existing `IArchitectureEvidenceFileSystem` contract is unchanged.

### Preserve established security and acquisition boundaries

The extraction preserves the current exception types/details and no-follow checks for repository containment, ancestor/final reparse or symlink escapes, and non-regular files. `SarifEvidenceArtifactReader` remains the only owner of bounded stream reads, partial byte accounting, and SHA-256 facts; the new IO collaborator returns the same stream it returned before extraction.

Focused direct tests exercise a contained regular file, parent-path rejection, directory rejection, and final/ancestor symlink rejection where supported. Existing artifact-reader collaborator coverage remains the bounded-read authority by proving the maximum-plus-one byte result, limit flag, and digest.

### Remove only the exact policy debt

After structural proof confirms one `RegularFileHandleReader` declaration and a non-partial collaborator, remove only this exact `source_type` / `forbidden_reference` waiver. Update only the #813 evidence hunk in the active umbrella design/tasks; leave task 2.5 and all unrelated remediation obligations open.

## Risks / Trade-offs

- [Moving OS interop alters security behavior] -> retain existing native calls, error classification, and direct negative regressions.
- [A collaborator becomes a replacement aggregate] -> keep one focused non-partial repository-local responsibility and run declaration-count/code-size gates.
- [Path handling changes byte/provenance behavior] -> retain the existing filesystem API and artifact-reader bounded-read tests without another read path.
- [Shared coordination files conflict with sibling work] -> change only the precise waiver and #813 evidence hunks, then rebase before the pull request.

## Migration Plan

1. Introduce the non-partial repository-local reader and reduce `RegularFileHandleReader` to one declaration without changing the public filesystem API.
2. Add direct repository-local regression coverage and retain bounded artifact-read evidence.
3. Remove the exact waiver and synchronize only #813 evidence in `decompose-god-classes`.
4. Run focused Core tests, formatting, policy/code-size/API checks, and OpenSpec validation.
5. Archive this scoped no-delta change after synchronization, while keeping `decompose-god-classes` active.
