## Why

The trusted Badge bootstrap generates the correct managed Relay bundle but then
directly pushes it to the consumer base branch. A protected branch correctly
rejects that update, leaving the documented turnkey path unusable for the
normal protected-branch configuration it claims to support.

## What Changes

- Replace the direct bootstrap base-branch update with a constrained,
  reviewable bootstrap pull-request path.
- Keep setup bound to the trusted base ref/tree and preserve the existing
  exact OIDC, publisher pin, and fail-closed validation contract.
- Add regression coverage that prohibits direct base-branch bootstrap pushes
  and verifies the protected-branch handoff.
- Update the setup guidance to describe normal protected-branch review and
  merge accurately.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `architecture-health-badge-promotion`: Bootstrap must hand managed output to
  normal protected-branch review rather than directly updating the base ref.
- `badge-turnkey-setup`: Turnkey setup guidance must describe the reviewable
  protected-branch handoff and its fail-closed boundaries.

## Impact

- Pinned reusable bootstrap workflow and its static/workflow regression tests.
- Badge setup documentation and managed-output provenance flow.
- No public API, consumer ruleset, secret, Cloudflare authority, or release
  publication change.
