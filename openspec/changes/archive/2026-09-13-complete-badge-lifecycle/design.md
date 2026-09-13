## Context

The v0.8 badge graph already has canonical projection, trusted promotion,
OIDC-backed per-alias storage, read-time expiry, and setup/doctor. The remaining
gap is operational ownership: an adopter needs a safe way to inspect and change
the destination after installation without bypassing the Relay's generation,
epoch, or tombstone rules. The implementation must stay transport-only and must
not duplicate #828 storage or #831 read semantics.

## Goals / Non-Goals

**Goals:**

- Provide authenticated, redacted admin status and lifecycle transitions.
- Preserve immutable repository/owner identity on rename and require explicit
  re-registration after transfer or removal.
- Make pin rotation and bundle compatibility changes staged and fail closed.
- Bound private operational history and expose actionable reason codes without
  public provenance.
- Ship CLI invocation and an executable operator runbook for the complete path.

**Non-Goals:**

- No new Architecture Health evaluator, provider adapter, hosted SaaS, or
  automatic Cloudflare deployment.
- No automatic ownership transfer, alias reuse, backup trust, or semantic
  horizon extension.
- No rewrite of the existing read-time expiry, publisher protocol, or setup
  output contract.

## Decisions

1. **One private control plane, two authorities.** The outer Worker authenticates
   the adopter's short-lived admin bearer token and exposes only bounded private
   routes: `status`, `reconcile-identity`, `revoke`, `recover/open`,
   `recover/finalize`, `upgrade/{stage|activate|rollback}`, and `uninstall`.
   It forwards an operation ID and expected registry revision/epoch to the
   Registry and per-alias Durable Object. Publisher OIDC remains the only
   source-publication credential. Admin calls never accept canonical payloads or
   trusted-context proof.
2. **Rename is metadata-only.** An admin may change the private display owner or
   repository only when both immutable IDs match the registered entry. The alias,
   state, generation, epoch, and public bytes do not change.
3. **The Registry is the lifecycle barrier.** Every forwarded read/mutation
   carries the registry revision and barrier epoch. An older caller is rejected
   without mutation; a revision-only rename synchronizes metadata while
   preserving ready data; a newer barrier clears ready data and enters
   `needs-recovery`, so a stale Durable Object backup or delayed publisher cannot
   win. Registry revocation first advances the barrier and is
   idempotent; Durable Object cleanup is retried afterward. Transfer,
   removal, and revocation tombstone the old alias. Transfer returns an explicit
   `registration_required` result; it never creates a new binding or carries
   consent to another owner. A tombstone is retained and cannot be silently
   reused.
4. **Pin rotation is invalidate-then-update.** The current state is made
   unavailable with a monotonic epoch before the registry stores the new exact
   workflow pin/audience. A stale writer using the old pin therefore loses even
   if it retries after the registry update.
5. **Compatibility is an allowlist.** The lifecycle service recognizes only the
   shipped `badge-relay/v1`, `v1` contract, and
   `architecture-health-badge-relay/v1` plan. An operator-controlled manifest
   supplies shipped digests; transitions maintain active, staged, and previous
   verified digests. Upgrade stage/activate and rollback are idempotent and
   otherwise return a bounded conflict without state mutation.
6. **Status is private and bounded.** The Durable Object retains the current
   state plus a bounded, redacted operation journal (30-day age and 256-entry
   cap). Status includes state, generation, epoch, lease boundary, last reason,
   and compatibility identifiers, never payload, token, source identity, commit/tree SHA,
   PR/run data, or receipts.
7. **CLI tokens stay out of arguments.** The lifecycle CLI reads the admin
   bearer token from `ARCHLINTERNET_BADGE_ADMIN_TOKEN` and the operator origin
   from `ARCHLINTERNET_BADGE_ADMIN_ORIGIN`, exact-matches it to the generated
   config endpoint, and sends only approved JSON fields. `--dry-run` emits
   the operation plan without network access or writes. Destructive operations
   require an explicit approval switch; recovery is an open/finalize pair.

## Risks / Trade-offs

- [Admin token compromise] → Require a dedicated adopter token, keep it out of
  command-line options/logs, and return generic errors.
- [Registry update races with relay mutation] → Relay mutation is performed
  first; registry update uses an atomic conditional transaction and failure
  leaves the destination unavailable, never falsely ready.
- [Cloudflare schema drift] → Additive SQLite columns and bounded migration
  checks are idempotent; unknown bundle/contract values are rejected.
- [Third-party cached images remain visible] → The runbook explicitly calls
  them uncontrollable cached copies and treats origin status as authoritative.
- [Best-effort scheduler or provider outage] → Status records redacted reason
  codes; read-time expiry remains the correctness boundary and no stale-ready
  fallback is introduced.

## Migration Plan

Existing v1 Relay rows are upgraded by additive columns/tables on first access.
No payload rewrite or generation reset occurs. Operators deploy the compatible
bundle, run `doctor`, then use `lifecycle status`; rotation/recovery is explicit
and starts unavailable when a restore barrier is detected.
