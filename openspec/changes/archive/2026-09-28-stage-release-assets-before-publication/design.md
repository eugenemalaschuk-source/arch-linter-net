## Context

The v0.9.0 workflow published the GitHub Release before attaching candidate assets. Repository policy makes published releases immutable, so GitHub rejected the later upload and left a public release with no assets. NuGet packages and documentation had already been published by earlier workflow jobs.

## Goals / Non-Goals

- Keep newly created releases as drafts until every expected asset has been read back and digest-verified.
- Resume matching drafts and complete published releases safely.
- Stop before upload when an incomplete published release is immutable.
- Keep package publication, tag identity, candidate binding, and Checkpoint B authorization unchanged.

## Decisions

- Read release draft, immutable, and asset metadata from GitHub's release API.
- Create with `gh release create --draft`; after verifying the complete inventory, publish with `gh release edit --draft=false` and verify the published inventory again.
- For existing published releases, preserve idempotent verification when complete. Reject an incomplete immutable inventory before any upload. Permit upload to an incomplete published release only when GitHub reports it is not immutable.
- Update the maintainer procedure and add a reviewed v0.9.1 scope declaration for this release-integrity correction.

## Risks / Trade-offs

- A failure after NuGet publication but before GitHub Release publication still requires a corrective patch version because already-published package versions cannot be replaced.
- The release job needs the existing GitHub token permissions to create drafts, upload assets, and publish releases; no new secret or permission is expected.

## Migration Plan

The fix applies to future releases. The published immutable v0.9.0 tag is not reused; publish the correction as v0.9.1 after merge, dry-run review, and the normal candidate gates.
