## Why

The shipped badge setup and Relay can publish and expire a payload, but an adopter cannot yet complete the operational lifecycle safely. Ownership changes, consent withdrawal, pin rotation, uninstall, recovery, and compatibility checks need one authenticated, auditable path that cannot resurrect revoked state or leak private provenance.

## What Changes

- Add a versioned Relay lifecycle/admin contract for status, invalidate, revoke, recovery, identity-preserving rename, ownership transfer, pin rotation, removal, and bundle upgrade/rollback.
- Enforce immutable repository/owner identity and alias tombstones across rename, transfer, uninstall, and retry races.
- Add bounded private operational diagnostics and redacted reason codes for expiry, authorization, quota, storage, and compatibility failures.
- Add CLI lifecycle commands that validate the generated setup configuration and invoke only the approved authenticated Relay admin routes.
- Add an executable operator runbook covering every lifecycle event, recovery/rollback ordering, cost and retention bounds, cache limits, and synthetic verification scenarios.
- Add adversarial and integration coverage for stale writers, replay, restore, transfer/name reuse, pin rotation, compatible upgrades, refused incompatible rollbacks, and safe cleanup.

## Capabilities

### New Capabilities

- `badge-lifecycle-operations`: Authenticated lifecycle/admin operations, bounded private status, CLI integration, and the adopter runbook for safe badge publication operations.

### Modified Capabilities

- None.

## Impact

- Extends the isolated TypeScript Relay registry and per-alias Durable Object with lifecycle state, compatibility metadata, bounded operation history, and admin routes; it does not import Core or recalculate Architecture Health.
- Extends the .NET badge command surface with a lifecycle subcommand while preserving the existing setup/doctor and canonical projection contracts.
- Adds internal/publicly linked operator documentation and OpenSpec coverage; no package publication, provider deployment, or release-authority changes are included.
