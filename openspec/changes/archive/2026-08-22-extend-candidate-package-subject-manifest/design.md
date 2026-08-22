## Context

The release workflow packs each NuGet project once and freezes the resulting
candidate through `tools/release/package_manifest.py`. Its v1 manifest includes
only primary `.nupkg` files even though the pack output and GitHub Release also
contain `.snupkg` files. Checkpoint B therefore cannot prove the identity of
symbols later selected by NuGet's adjacent-symbol convention or a broad release
asset glob.

The source-of-truth boundary is project-controlled, pre-publication bytes.
NuGet.org repository signing is an external transformation and is deliberately
outside this change; #611 owns attestation of the canonical manifest and its
derived evidence files.

## Goals / Non-Goals

**Goals:**

- One deterministic, strict candidate inventory of every expected primary and
  symbol package, explicitly paired by package ID and version.
- One reusable verifier for all candidate consumers, including publish and
  release attachment paths.
- A deterministic checksum rendering derived solely from the canonical manifest.
- Clear, bounded v1 compatibility for fixtures and historical evidence.

**Non-Goals:**

- Attestation, NuGet signature verification, author signing, or SBOM work.
- Repacking, rebuilding, or changing package-version behavior.
- Treating a NuGet.org repository-signed download as byte-identical to the
  pre-publication primary package.

## Decisions

### Version the existing manifest authority in place

The existing manifest tool becomes the sole owner of a v2 schema rather than a
parallel checksum format. Each package record contains the common package ID,
version, and two typed subject records (`package` and `symbols`) carrying their
exact filename, byte size, and SHA-256 digest. The order is fixed by the
reviewed package-ID list and typed subjects are fixed by kind.

This preserves explicit pairing and makes unexpected or cross-candidate symbols
unrepresentable. A flat list was rejected because a caller could accidentally
separate a symbol record from its primary package.

### Verify both the manifest and directory inventory fail closed

Creation checks exactly the expected `.nupkg` and `.snupkg` filenames. Verification
checks schema, source commit/version/filename agreement, duplicate ambiguity,
the exact directory subject set, and each recorded digest/size. Expected
identity can also be asserted by workflow callers, binding a downloaded
candidate to its release version and source commit.

The v1 reader remains only as an explicit compatibility path for historical
evidence; it cannot be treated as a complete symbol inventory or used by the
v2 release workflow.

### Render checksums from verified canonical records

The tool exposes a rendering operation that validates the canonical v2 manifest
then writes a stable text checksum file ordered by package ID and subject kind.
The rendering has no independent inputs or digest computation, so it cannot
become a competing authority. It is not placed in the manifest's subject list;
#611 can later attest the manifest and checksum files as outer evidence.

### Consume manifest-selected paths rather than globs downstream

The release workflow verifies the v2 candidate before each handoff and derives
the exact primary-package upload paths and GitHub Release attachment paths from
the manifest. The NuGet push loop invokes `dotnet nuget push` once per primary
package, then verifies its adjacent manifest-selected symbol file is present;
this respects NuGet client symbol handling without double-pushing `.snupkg`.

## Risks / Trade-offs

- [Historical v1 evidence lacks symbol guarantees] → retain an explicit v1
  reader only where compatibility is necessary; use v2 only for new release
  creation and publication.
- [NuGet client symbol handling varies] → validate each adjacent symbol before
  the primary push but do not independently push the symbol file.
- [A workflow glob admits unrelated files] → emit verified manifest-selected
  path lists and consume those lists for publication/attachment.
- [Checksum text drifts from JSON] → generate it only after canonical-manifest
  validation and test deterministic bytes.

## Migration Plan

1. Add v2 creation, strict parsing/verification, deterministic checksum
   rendering, and focused Python tests.
2. Migrate CI and release workflow consumers to create, verify, render, and
   consume v2 paths.
3. Update Checkpoint B/release-evidence helpers and tests to require paired
   complete inventory.
4. Update release documentation, validate the spec, and archive this change.

Rollback is a normal revert of the workflow/tool/spec changes before a release;
v1 remains readable but a new release must not fall back to incomplete v1
subjects.

## Open Questions

- None. GitHub attestation of the generated manifest/checksum bytes is deferred
  explicitly to #611.
