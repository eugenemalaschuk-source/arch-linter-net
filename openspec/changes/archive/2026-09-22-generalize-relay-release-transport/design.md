## Context

`release-nuget.yml` unconditionally creates and verifies the frozen Relay
transport before Checkpoint B. Its packager still encodes the original #835
v0.8.x integration boundary in two places:

1. candidate version validation accepts only `0.8.x`;
2. generated transport manifests copy `release_authority=#806`,
   `lifecycle=milestone-6/v0.8.x-completeness`, and the historical handoff.

For a v0.9 candidate those fields are not current publication authority.
Current publication authority already has a canonical owner:
`tools/release/scopes/<exact-version>.json` -> Checkpoint B candidate
authorization.

## Decisions

### Keep frozen transport in the ordinary release pipeline

Do not special-case v0.9 by skipping Relay transport verification. The release
workflow already treats transport bytes as candidate-bound release subjects,
attests them, and attaches the same frozen bytes. Removing that path would
weaken completeness for the existing experimental/opt-in transport surface.

### Separate review origin from current release authority

The transport inventory remains a closed, reviewed byte/pin boundary. Its new
header records:

- support status: `experimental-opt-in`;
- publication boundary: `external-checkpoint-b-release-scope`;
- review origin:
  - story #825;
  - distribution task #835;
  - first release authority #806;
  - original lifecycle `milestone-6/v0.8.x-completeness`.

These fields explain where the immutable transport contract came from without
claiming that #806 authorizes a v0.9 package release.

### Version the machine contract

The inventory changes from v2 to v3 and generated distribution manifests from
v1 to v2. Historical v0.8 manifests are not rewritten. New verification code
produces and validates the new schema for new candidates.

### Validate a generic SemVer-style package identity

The transport packager accepts the same broad SemVer-style NuGet identity class
needed for release candidates, including stable, preview, and bounded
prepublication suffixes. It still rejects malformed/path-like/non-ASCII
versions and binds the exact value to the candidate package manifest.

Transport version acceptance is not publication authorization. Exact stable or
preview publication authorization remains separately enforced by the
release-scope gate.

## Correctness and trust invariants

- candidate manifest version/source/digest remain exact;
- bundle member allowlist remains closed;
- workflow and action commits/blobs remain independently pinned;
- compatibility identities remain unchanged;
- generated archive remains deterministic;
- subject size/digest and outer checksum verification remain fail-closed;
- attestation subject selection remains manifest-driven;
- no current release is authorized by transport metadata.
