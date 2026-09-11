## 1. Relay bundle foundation

- [x] 1.1 Create the isolated `badge-relay/v1` Worker package, SQLite Durable Object binding, pinned JOSE/test dependencies, and reproducible scripts; verify installation and type checking succeed.
- [x] 1.2 Implement private registry configuration and bounded public routing so unregistered aliases never create objects; verify registry and unknown-alias integration tests pass.

## 2. Publisher identity boundary

- [x] 2.1 Implement fixed-chain GitHub OIDC validation with protected-header, issuer, audience, time, immutable identity, event/ref, workflow-pin, and subject checks; verify positive and negative OIDC vectors pass.
- [x] 2.2 Implement redacted bounded publisher request parsing and generic failures; verify oversize, invalid URL, and no-token-disclosure tests pass.

## 3. Atomic publication state

- [x] 3.1 Implement SQLite-backed state schema and atomic challenge/idempotency/generation/revocation-epoch transitions; verify local Durable Object race, replay, CAS, and deadline tests pass.
- [x] 3.2 Implement private invalidate, revoke, and recovery barriers that remove ready eligibility and preserve tombstones; verify delayed-writer and cross-tenant isolation tests pass.

## 4. Verification and synchronization

- [x] 4.1 Run the full Relay test/typecheck/lint commands and fix issue-scoped failures; verify all package checks pass.
- [x] 4.2 Compare implementation with the Relay contract/fixtures, update the change artifacts to reflect delivered behavior, and verify `openspec validate implement-badge-relay-oidc-storage --strict` passes.
