## Why

Multi-review of PR #858 found that the initial turnkey badge setup surface can
generate synthetic Relay identity, publish an invalid producer workflow pin,
claim unproven capabilities, and emit incomplete consumer wiring. These are
trust-contract defects in #832, not downstream acceptance work.

## What Changes

- Replace fixture-only Relay deployment values with adopter-bound immutable IDs,
  audience, registry, and OIDC workflow configuration.
- Generate producer, trusted publisher, and optional metadata-only renewal
  workflows; calculate the producer workflow blob SHA from the exact generated
  consumer file and use Shields-compatible README URLs.
- Add fail-closed provider/GitHub capability inspection and a doctor that can
  distinguish a fresh unavailable destination from a healthy observed install.
- Validate generated and parsed configuration against the shipped schema,
  including HTTPS, pins, IDs, bounds, and managed paths.
- Expand packed Relay/conformance tests to execute generated artifacts and
  verify identity, wiring, digest, doctor, rollback, and redaction contracts.

## Impact

This is a hardening change to the archived `badge-turnkey-setup` capability.
It does not publish a package, deploy an adopter resource, or move live
provider acceptance to #834/#806.
