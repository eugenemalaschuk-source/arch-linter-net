## Context

The accepted contract already fixes the protocol and public disclosure rules in
the Relay ADR and `architecture-health-badge-relay-contract` spec. This change
adds the first deployable runtime. The repository currently has no Worker or
JavaScript package, so the runtime must remain an isolated bundle and must not
introduce a provider dependency into the .NET projects.

The ADR file still labels itself "proposed", while its owning issue #826 is
closed and #828 explicitly identifies the ADR/contracts as accepted. This
change treats the closed issue plus current canonical spec/fixtures as the
accepted source of truth; it does not alter the contract.

## Goals / Non-Goals

**Goals:**

- Provide a testable `badge-relay/v1` Worker with one SQLite-backed Durable
  Object per registered opaque alias.
- Make immutable registry pins, OIDC verification, replay prevention, and
  generation/epoch CAS independently inspectable and locally executable.
- Keep storage failures and untrusted inputs fail-closed and diagnostics
  public-safe.

**Non-Goals:**

- Canonical projection, payload validation, semantic-horizon calculation,
  public rendering/read endpoints, deployment/bootstrap tooling, recovery
  operator UX, source-repository access, and GitHub workflow changes.
- GitHub PAT/App support, arbitrary outbound URLs, KV as authority, or a
  project-operated Relay service.

## Decisions

### Isolated TypeScript Worker package

Create `relay/` with a pinned Worker toolchain, `wrangler.jsonc`, TypeScript
sources, and local Miniflare/Vitest integration tests. The Node dependency lock
is committed with the bundle. An isolated package avoids adding JavaScript
dependencies to Core/CLI and maps directly to the adopter-owned bundle
boundary. A C# host was rejected because Cloudflare Durable Objects execute
Worker modules and the contract names a Worker/SQLite reference runtime.

### Fixed private registry plus named alias-state objects

A fixed-name SQLite-backed Registry Durable Object persists immutable entries
and tombstones. It may seed missing entries from adopter-private configuration,
but uses insert-only seed semantics so a cold start cannot overwrite a
registration or resurrect a tombstone. The outer Worker performs the registry
lookup before calling `idFromName(alias)`; public unknown aliases therefore
reach only this fixed object and cannot allocate an unbounded tenant object.
Each registered alias then has a separate named SQLite-backed object with one
`state` row and bounded `challenges` and `replay_keys` tables. The state object
performs every mutation within its storage transaction. Eventual KV and a
shared in-memory map were rejected because they cannot prove ordering,
persistence, or revocation precedence.

### Fixed JOSE verification chain

Use maintained `jose`, pinned in the lockfile, with a constant HTTPS GitHub
JWKS URL. Decode only enough protected-header information to reject bad size,
format, `alg`, and missing `kid`; verify signature and claims through the fixed
remote JWK set with a single bounded refresh path. Validate exact issuer,
audience, time bounds, immutable IDs, event/ref, workflow ref/SHA, and an
explicit subject matcher after signature verification. Neither JWT headers nor
claims select a URL. Handwritten cryptography and config-pinned `kid` values
are rejected alternatives.

### Two-phase private publication contract

`prepare` authenticates the publisher and inserts one fixed-deadline challenge
bound to digest, request state, JTI hash, idempotency hash, and expected
generation/epoch. The public HTTP `publish` path deliberately fails closed:
it does not accept a caller-supplied `trusted_context` as proof. An internal
commit seam accepts only the later trusted exact-tree verifier's typed
assertion after its exact digest and expected state match the challenge, then
performs a transaction that consumes the challenge and CASes state. This task
defines that internal seam but deliberately does not implement the canonical
projector or GitHub artifact/tree verifier owned by later work.

### Fail-closed bounded operations

All JSON parsing is size-limited; manifests are opaque bounded bytes/digest
bindings rather than archives; per-alias limits cover active challenges,
replay records, and payload/envelope length. Error responses use a small reason
code enum and never echo token, identity, URL, payload, or receipt data.

## Risks / Trade-offs

- [Local emulation differs from production Durable Objects] → integration tests
  use the official Workers test pool and real local SQLite-backed object
  semantics; production deployment remains a later adopter acceptance task.
- [JWKS availability/key rotation] → fixed endpoint, short timeout, one
  bounded refresh, no caller-controlled fallback, and fail-closed rejection.
- [An alias can be used as an object-name allocation vector] → registry lookup
  precedes `idFromName` on public paths; only private registration creates a
  registered object route.
- [Later publisher proof is not yet implemented] → prepare/publish accepts only
  a typed internal trusted-context assertion and cannot derive it from caller
  URL/bytes; the seam is explicitly owned by follow-up #830.

## Migration Plan

1. An adopter deploys the versioned Worker with no registered aliases and a
   configured private registry/admin credential.
2. The later setup tooling provisions registry entries from reviewed
   configuration and pins the published bundle version.
3. Rollback disables routing or revokes aliases; it never substitutes a static
   ready payload. Schema changes require an explicit Durable Object migration
   and preserve revocation epochs/tombstones.
