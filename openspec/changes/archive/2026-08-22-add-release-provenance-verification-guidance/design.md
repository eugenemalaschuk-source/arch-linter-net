## Context

The #610 manifest and #611 workflow attestations establish project-controlled
release-byte identity before publication. The existing release-process page
summarizes that boundary, but is maintainer-oriented and does not give consumers
a complete executable order for verifying independently attested evidence before
using it as digest metadata. The first v0.7 release must be authorized from a
non-publishing rehearsal, so live-release verification cannot be a prerequisite.

## Goals / Non-Goals

**Goals:**

- Publish one public, evergreen, artifact-first verification guide.
- Make the trust order, NuGet.org boundary, and separate signing/SBOM decisions
  explicit and testable.
- Link the guide from README, release-process documentation, MkDocs navigation,
  and generated GitHub Release notes.

**Non-Goals:**

- Change candidate manifests, attestation generation, package contents, or
  publication authorization.
- Implement NuGet author signing, generate an SBOM, claim a SLSA level, or
  assert unverified NuGet.org symbol-server behavior.

## Decisions

### Use one public guide as the canonical consumer contract

Place the guide under `docs/guides/` and keep the release-process page as a
short maintainer-oriented link. This gives consumers one stable entrypoint and
avoids duplicating a fragile command tutorial in release bodies. The README and
release notes point to the same canonical URL.

### Verify outer evidence before package inventory

The guide first verifies attestations for `package-manifest.json` and
`package-checksums.txt`, then parses the verified manifest, checks the raw
SHA-256 of GitHub Release/rehearsal package subjects, and verifies each package
or symbol attestation. This is the only order that does not ask consumers to
trust an unauthenticated checksum list.

### Model NuGet.org as a distinct post-upload trust boundary

The guide deliberately separates exact GitHub Release assets from NuGet.org's
repository-signed primary-package download. It directs consumers to supported
NuGet package-signature and expected ID/version checks rather than byte
comparison or signature stripping. Symbol-package claims remain limited to the
supported symbol-server contract.

### Defer author signing and package-level SBOMs

The project defers both. Author signing needs durable certificate custody,
rotation, timestamping, and release-order review; an SBOM needs reliable
per-package component mapping and deterministic binding. Implementing either
requires a separate scoped issue before the project makes related claims.

## Risks / Trade-offs

- [GitHub or NuGet CLI syntax changes] → retain tested command fragments and
  update the guide with the supported tool contract.
- [A future release body omits the guide] → statically test the release-note
  append step.
- [Documentation conflates byte domains] → test required terminology and the
  explicit prohibition on NuGet.org raw-byte equality.
- [Consumers use a different symbol service path] → provide no raw-byte or
  signing guarantee beyond documented NuGet.org behavior.

## Migration Plan

1. Add the guide and entry-point links, then add deterministic tests.
2. Append the stable guide link during GitHub Release-note generation and test
   the same frozen attachments remain selected.
3. Validate through the existing release-tool fixture and docs checks.

Rollback is a normal documentation/workflow revert before publication; no
published package or data migration is involved.

## Open Questions

None. Live NuGet.org and GitHub Release checks remain post-publication
confirmation, not a prerequisite for the initial publication.
