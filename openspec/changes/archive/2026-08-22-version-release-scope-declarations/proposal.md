## Why

The release scope is currently one mutable declaration hard-wired to v0.6.4.
That prevents the v0.7.0 release authority from coexisting safely with a still
publishable v0.6.4 maintenance authority and can bind a candidate to the wrong
set of release blockers.

## What Changes

- Replace the single fixed release-scope declaration with a tracked collection
  of reviewed declarations selected from the immutable candidate manifest's
  exact release version.
- Preserve the existing v0.6.4/#527 declaration and add the v0.7.0/#613
  declaration, including its required, excluded, and delivered-context items.
- Fail closed for missing, duplicate, malformed, incompatible, preview, or
  otherwise unmapped target declarations; the release-authorizing CLI continues
  to accept no caller-supplied declaration path.
- Bind the selected declaration's explicit identity and SHA-256, candidate
  version, manifest digest, and source commit into release-scope evidence, and
  verify that binding during final Checkpoint B aggregation.
- Update active release documentation, tests, and comments to describe the
  target-selected authority model rather than historical global release scope
  assumptions.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `checkpoint-b-release-evidence`: select, attest, and verify the declaration
  that exactly matches the immutable candidate's release identity.
- `ci-release-gate`: require the release workflow's candidate-specific
  release-scope evidence rather than a mutable global declaration.

## Impact

Affected areas are the release-scope generator and declarations, Checkpoint B
evidence aggregation, release workflow documentation, Python release-tool
regressions, and the two release specifications. No product runtime behavior or
public .NET API changes are introduced.
