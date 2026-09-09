## Why

The existing public Architecture Health badge is a static public snapshot.  It
does not provide a safe, bounded-freshness path for a private repository, and
its public receipt is too broad to become a private-repository disclosure
contract.  Before implementation children can build a Relay, product-owned
contracts must make the trust, privacy, expiry, and recovery boundaries
executable and reviewable.

## What Changes

- Select an adopter-owned Badge Relay, implemented by a Cloudflare Worker with
  a SQLite-backed Durable Object, as the only turnkey private-repository mode;
  retain `none` and public `github-raw` as supported modes.
- Define versioned canonical payload, promotion, disclosure-profile, validity,
  publication-envelope, OIDC-trust, and lifecycle contracts without changing
  the existing `architecture-health/v1` or canonical Gate/Health semantics.
- Add synthetic configuration samples and positive/adversarial contract vectors
  for disclosure, stale writers, expiry, recovery, and authentication.
- Record a reviewed internal decision record covering public disclosure,
  operational bounds, lifecycle state machine, and release ownership.

## Capabilities

### New Capabilities

- `architecture-health-badge-relay-contract`: Defines the bounded-freshness,
  disclosure, identity, lifecycle, and compatibility contract that a turnkey
  private-repository Badge Relay must implement.

### Modified Capabilities

- None.

## Impact

This is design and contract work only.  It adds internal ADR and machine-readable
synthetic fixtures, and changes OpenSpec requirements.  It does not add a relay
runtime, cloud account configuration, Core/CLI evaluator behavior, a project
SaaS, GitHub PAT/App access, or a public documentation deployment.
