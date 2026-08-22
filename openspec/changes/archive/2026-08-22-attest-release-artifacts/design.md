## Context

The release workflow already packs one immutable candidate, records each
project-controlled package and symbol digest in a canonical manifest, produces
a derived checksum rendering, and re-verifies those bytes at Checkpoint B and
publication handoffs. The remaining gap is a signed, independently verifiable
binding between those frozen subjects and the GitHub Actions run that produced
them.

GitHub's current `actions/attest` action can create build-provenance
attestations for an explicit checksum inventory. Its complementary `gh
attestation verify` command can validate the subject digest and bind the result
to the repository, signer workflow, and source commit.

## Goals / Non-Goals

**Goals:**

- Attest exactly the canonical package/symbol inventory and, separately, the
  canonical manifest and checksum evidence files.
- Fail closed if package or outer evidence files are missing, unexpected,
  altered, or selected outside the frozen inventory.
- Verify every attestation in a distinct, non-publishing workflow job before
  NuGet upload or GitHub Release creation is reachable.
- Preserve the existing one-pack, manifest-selected handoff and least-privilege
  workflow permission model.

**Non-Goals:**

- NuGet author signing, SBOM generation, a formal SLSA-level claim, or a
  second checksum authority.
- Rebuilding, repacking, resigning, or regenerating a project-controlled
  subject after it is frozen.
- Raw-SHA-256 comparison of a NuGet.org repository-signed primary-package
  download with its pre-upload candidate.

## Decisions

### Use two explicit provenance subject classes

The package subject class is derived only from the verified v2 canonical
manifest. The evidence subject class contains exactly `package-manifest.json`
and the checksum file verified as its deterministic rendering. Each class is
attested in a separate `actions/attest` invocation using an exact, generated
checksum list rather than an optimistic output-directory glob.

This retains #610's non-recursive model: package records remain the sole
package-digest authority, while the manifest and checksum files gain their own
outer signed identities.

### Add a small release-evidence verifier and consumer verifier

`package_manifest.py` will gain verification/rendering operations for the two
outer evidence files and for machine-readable exact attestation inventories.
This is an extension of the existing canonical-manifest seam, not a second
manifest implementation. A focused provenance-verification script then uses
those exact paths to call `gh attestation verify` for every package and evidence
subject and performs controlled tamper negatives.

The scripts exist because the issue requires both an exact set check before
attesting and independent consumer-style verification after attesting; neither
can be expressed safely as a static workflow glob.

### Separate producing and verifying jobs

`attest-prepublication-provenance` runs after the candidate and Checkpoint B
are successful. It receives only `contents: read`, `id-token: write`, and
`attestations: write`, and pins `actions/attest` to its immutable commit SHA.
It downloads and re-verifies the frozen artifact before attesting.

`verify-prepublication-provenance` downloads that artifact independently and
uses the supported GitHub CLI with the exact repository, workflow identity, and
source digest. It must pass before the existing publish and release-attachment
jobs can run. The release path re-verifies the same artifact again immediately
before each handoff.

### Keep NuGet.org's signing boundary explicit

The attested subject is the pre-upload primary or symbol package selected from
the canonical manifest. GitHub Release assets retain this project-controlled
byte identity. NuGet.org may repository-sign a submitted primary package and
therefore publishes a different raw-byte artifact; documentation directs
post-publication verification to NuGet package identity and signature semantics
instead of the pre-upload digest.

## Risks / Trade-offs

- [GitHub permissions or platform capability are unavailable] → the
  attestation-producing job fails before any publication path runs; no fallback
  to an unauthenticated checksum is permitted.
- [The GitHub CLI or action contract changes] → pin the action, use the
  documented `gh attestation verify` interface, and cover its required command
  arguments in focused workflow tests.
- [An input selection file drifts from the candidate] → generate it only after
  strict manifest/evidence verification and validate all selected subjects
  independently in the next job.
- [Remote NuGet bytes differ] → document that repository signing is expected
  and keep the cross-boundary verification method distinct.

## Migration Plan

1. Extend release-manifest verification and add unit tests for exact evidence
   and attestation subject inventories.
2. Add the attestation and independent verification jobs plus static workflow
   tests for their ordering, permissions, pins, and same-byte handoffs.
3. Document consumer verification and the NuGet.org signing boundary.
4. Run release-tool/workflow tests, formatter, workflow lint, and strict
   OpenSpec validation; archive the synchronized change.

Rollback before publication is a normal revert of the workflow and release-tool
changes. There is no migration of public package contents or library API.

## Open Questions

None. The workflow fails closed if the current GitHub-hosted attestation
service cannot produce or verify the required provenance.
