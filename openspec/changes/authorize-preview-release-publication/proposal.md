## Why

The public release workflow exposes a `preview` scenario and the public
versioning contract describes `*-preview.N` as a publishable early-validation
release, but publication authorization currently accepts only stable
`X.Y.Z` release-scope targets. A `publish=true` preview therefore fails
before publication even when the exact candidate has passed Checkpoint B.

The first v0.9 dogfood release needs an immutable `0.9.0-preview.1` package
without claiming that the stable v0.9 performance gates are complete.

## What Changes

- Allow a reviewed release-scope declaration to target either exact stable
  `X.Y.Z` or exact preview `X.Y.Z-preview.N` package versions.
- Keep publication authorization bound to exact candidate version, manifest
  digest, source commit, reviewed declaration bytes, and required issue states.
- Preserve fail-closed behavior for unmapped targets, arbitrary prerelease
  labels, duplicate declarations, malformed declarations, and unknown targets.
- Make preview and stable authorities independent: a declaration for
  `0.9.0-preview.1` cannot authorize `0.9.0`.
- Document when `version_override` is appropriate for the first preview of a
  new minor line whose latest tag is still on the previous stable line.
- Add the reviewed `0.9.0-preview.1` scope without closing stable v0.9
  performance/documentation/release authorities.

## Capabilities

### New Capabilities

- `release-publication-authorization`: exact reviewed publication authority
  for stable and preview package targets with stable/preview separation.

### Modified Capabilities

## Impact

Only release authorization tooling, release-process documentation, and release
scope evidence change. Analyzer runtime, policy/schema behavior, package
contents unrelated to release metadata, and the stable v0.9 performance gate
remain unchanged.
