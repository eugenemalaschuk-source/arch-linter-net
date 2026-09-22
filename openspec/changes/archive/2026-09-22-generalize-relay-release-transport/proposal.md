## Why

The first official `0.9.0-preview.1` publication run failed after package
creation because the frozen Relay transport packager still accepts only
`0.8.x` versions. The same transport inventory also carries historical
v0.8-specific release authority (#806) into generated manifests.

Those constraints were correct when #835 integrated Relay distribution only
for the v0.8.x maintenance line, but the existing release workflow now freezes,
verifies, attests, and attaches the transport for every candidate. Merely
widening the version regex would create false release provenance for later
release lines.

## What Changes

- Make frozen Relay transport candidate-version validation release-line neutral
  for SemVer-style NuGet versions, including `0.9.0-preview.1`.
- Version the reviewed transport inventory as
  `architecture-health-badge-release-inventory/v3`.
- Version generated transport manifests as
  `architecture-health-badge-release-distribution/v2`.
- Replace current-release-looking v0.8 fields with:
  - `support_status: experimental-opt-in`;
  - `publication_authority: external-checkpoint-b-release-scope`;
  - immutable historical `review_origin` (#825/#835/#806 and the original
    v0.8.x lifecycle).
- Preserve the candidate manifest binding, frozen subject set, Relay bundle
  allowlist, compatibility identities, workflow/action pins, checksums,
  attestation, independent provenance verification, and release attachment.
- Keep the current package release authority exclusively in exact Checkpoint B
  release-scope evidence.

## Capabilities

### Modified Capabilities

- `release-artifact-provenance`: frozen transport evidence becomes
  release-line neutral while preserving exact candidate/provenance guarantees.

## Impact

No analyzer, policy, schema-evaluator, Relay runtime, provider, OIDC, storage,
or support-status behavior changes. Historical v0.8 release assets remain
immutable. The failed run published no package, tag, Release, or Pages output.
