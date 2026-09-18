## Why

The protected-bootstrap pull-request path requires every private Relay adopter to
provision and safeguard a GitHub App private key even though the credential is
not a Relay or OIDC publisher credential. Live consumer acceptance established
that this one-time operational prerequisite makes the documented turnkey setup
unusable before the supported OIDC publication path can begin.

## What Changes

- Replace the GitHub App branch/PR writer in trusted bootstrap with a private,
  immutable handoff artifact containing the generated managed setup and its
  trusted base-tree and producer bindings.
- Provide a normal consumer-owned PR apply path that verifies the handoff
  before materializing managed output, without any bootstrap remote write.
- Remove App-ID and private-key bootstrap inputs, App-token minting, Git pushes,
  and pull-request API calls from the reusable publisher workflow.
- Document the no-App one-time review handoff separately from ongoing pinned
  GitHub OIDC publication.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `architecture-health-badge-promotion`: Trusted bootstrap must emit a
  content-bound handoff and must not require GitHub App writer credentials or
  write to consumer Git references.
- `badge-turnkey-setup`: Relay setup must provide a verified normal-PR handoff
  path without an App/PAT prerequisite.

## Impact

The pinned reusable bootstrap workflow, CLI setup surface and packaged
templates, focused workflow/CLI tests, generated consumer workflow templates,
and setup/adoption documentation change. Relay runtime, canonical Architecture
Health evaluation, required-check verification, exact producer provenance, and
ongoing OIDC publication do not change.
