## Context

The existing `architecture-health/v1` and the current CLI badge projector own
semantic Gate, Health, ignore-debt, and rule-count facts.  The static public
publisher owns a reviewed `architecture-health-badge-promotion/v1` manifest and
`architecture-health-badge-publication/v2` receipt.  Those facts remain the
canonical input; this design adds no evaluator and no private GitHub artifact
reader.

## Goals / Non-Goals

**Goals:**

- Freeze a small, versioned protocol for a prebuilt adopter-owned Relay.
- Keep public disclosure and semantic validity boundaries independent and
  mechanically testable.
- Make delayed writers, failed schedules, cache layers, recovery, and lifecycle
  events fail closed.

**Non-Goals:**

- A project-operated SaaS, a general provider catalogue, a GitHub PAT/App,
  arbitrary user endpoints, production Relay code, or a new Health evaluator.
- A claim that Camo/browser/offline cache copies can be revoked immediately.

## Decisions

### Select a bounded adopter-owned Relay

`relay` is a reference Worker plus SQLite-backed Durable Object that the adopter
deploys in its own account. The per-alias object is the sole ordering authority;
an eventually consistent KV store is not authoritative for revocation or
generation state. SQLite transactions atomically update payload bytes/digest,
lease, generation, revocation epoch, and used challenge/idempotency records.

`none` is the private default. `github-raw` remains a public static snapshot:
private users receive a visibility diagnostic, not a raw credential workaround.
Pages, generic object storage, mirrors, and authenticated endpoints are
extensions, not turnkey modes. This avoids a mandatory service and preserves
the current public-path contract.

### Separate canonical bytes, disclosure profile, private envelope, and receipt

The canonical CLI emits deterministic `headline-only/v1` badge bytes. The Relay
first matches those bytes to a closed dictionary; it does not reconstruct them.
`headline-plus-freshness/v1` adds only canonical UTC `verified_at` and
`valid_until` values to a separate render envelope. The exact `promotion/v1`
manifest and `publication/v2` receipt remain private provenance: they contain
source identities and are never a public JSON, SVG, header, error, log, or
telemetry input. The no-extra-field/duplicate-key/canonical-byte rule prevents
the exact digest from becoming a covert disclosure authorization.

### Use a 60-minute absolute lease with optional 30-minute renewal

Each ready value is valid only until `min(verified_at + 60m, semantic_horizon)`.
The publisher is permitted to request renewal at most every 30 minutes; a
healthy repository consequently needs at most 48 scheduled renewal attempts
per day, never an implicit 96. GitHub schedules are best effort, so an idle
repository can legitimately expire. An adopter can choose a lower cadence or
disable renewal; it cannot choose a larger lease without a future versioned
contract.

Expiry is evaluated on every read from a trusted Relay clock, with a five-minute
OIDC clock-skew allowance only for token validation. It does not alter Gate or
Health. If the canonical semantic horizon is absent, unknown, or passed, ready
publication and renewal fail closed. Reusing an unchanged tree cannot create a
new horizon; recovery uses a current PR-authoritative proof, never main
reanalysis or a prior receipt.

### Require immutable OIDC identity plus two-phase CAS

The registry pins `repository_id`, `repository_owner_id`, owner, permitted
event/ref, `job_workflow_ref`, and `job_workflow_sha`; it validates the GitHub
issuer/audience/algorithm/key/time claims against a fixed discovery/JWKS
configuration. A run with a familiar display name is insufficient. `sub`
format is treated as a configured compatibility input, not a loose wildcard;
legacy, immutable, and custom forms get separate vectors. OIDC establishes the
trusted publisher identity only; the publisher separately proves the artifact
and exact-tree context.

`prepare` records a one-time challenge with an immutable deadline, expected
digest/state/generation/epoch, and `jti`/idempotency binding. After it receives
the challenge, the publisher rechecks its GitHub context and `publish` performs
one conditional transaction. Older SHA, higher run ID, delayed delivery, ABA,
or force-push cannot win because none establishes ordering.

### Treat cache and public response as untrusted transport

Origin read is authoritative and never returns expired ready. JSON and
Shields-compatible output are compatibility snapshots. The default SVG includes
an inseparable readable absolute expiry timestamp. Responses use a bounded
`max-age` no greater than the remaining lease, conservative stale controls, and
ETag only for the complete public representation; 304 and HEAD must perform the
same expiry check. Caches can retain prior bytes beyond that boundary, which is
not revocable and not a freshness guarantee.

### Lifecycle has monotonic invalidation precedence

Each destination has a monotonic generation and revocation epoch. Invalidate,
revoke, transfer, consent/visibility loss, deletion, and pin rotation advance
the relevant epoch inside the same transaction that removes ready eligibility.
Aliases tombstone on removal and are not reused by a different tenant by
default. Backup recovery restores at most storage; a current publisher proof
with the current registry/pin must make a later-generation commit before ready
is observable.

## Risks / Trade-offs

- [Best-effort scheduled workflows can stop or be delayed] → Origin read-time
  expiry and the visible SVG expiry state prevent last-known-green truth claims.
- [Private GitHub Actions consumes owner-billed time] → 48 maximum renewal
  attempts/day, setup budget controls, and documented measurement; public
  repositories and self-hosted runners have different billing treatment.
- [Cloudflare limits/outages or a JWKS outage prevent a write] → bounded input,
  fixed provider endpoints, redacted diagnostics, and reject-before-write.
- [PITR can restore obsolete state] → keep a registry-authorized revocation
  barrier external to a restorable ready row and require fresh proof.
- [Unknown exact platform behavior] → the reference requires synthetic and
  subsequently live private-consumer acceptance before it becomes shipped.

## Migration Plan

1. Publish the ADR, schemas, samples, and vectors from this issue; review them
   before unblocking #827 and #828.
2. #827 implements the profile/horizon authority and consumes the shared
   conformance vectors; #828 implements OIDC registry/storage and consumes its
   private protocol vectors.
3. Later children implement promotion, reads, bundle, lifecycle, and candidate
   acceptance. #835 owns distribution integration; #806 remains the only
   released-artifact authority.
4. Until an adopter deploys the reviewed Relay bundle, use `none` for private
   repositories and preserve the existing public `github-raw` snapshot path.
