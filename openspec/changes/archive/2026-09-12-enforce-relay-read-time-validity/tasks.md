## 1. Public read authority

- [x] 1.1 Implement the Relay Durable Object read/representation seam so it validates persisted closed payload state, checks `now < valid_until` before ETag handling, and returns exact ready JSON or fixed unavailable output; verify virtual-clock expiry, corrupt state, GET, HEAD, and conditional-request tests pass.
- [x] 1.2 Add fixed-template freshness SVG rendering and representation-aware cache/ETag/security headers without transport semantic recomputation; verify injection, Unicode, identical-headline/new-lease, and cache-bound tests pass.
- [x] 1.3 Route registered public aliases to only supported read representations after private registry lookup and preserve unknown-alias non-allocation; verify Relay integration tests pass.

## 2. Contract evidence and user boundary

- [x] 2.1 Extend synthetic Relay vectors/fixtures for expiry without schedulers, 304/HEAD, cache limits, availability fallback, and SVG safe rendering; verify fixture parsing and Relay contract tests pass.
- [x] 2.2 Update the Relay ADR and root README to describe the strict stamped-SVG default, JSON snapshot compatibility, and the non-recallability of cached copies; verify `make lint-docs` passes.

## 3. Integration and release-ready proof

- [x] 3.1 Compare implementation to the approved proposal/specs and synchronize any necessary behavior details; verify `openspec validate --strict --change enforce-relay-read-time-validity` passes.
- [x] 3.2 Run risk-tier validation for the Relay change (focused Relay tests, formatting, relevant lint, and OpenSpec validation), then inspect the final diff for scope and disclosure regressions.
