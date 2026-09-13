# badge-lifecycle-operations Specification

## Purpose
Define the authenticated, fail-closed lifecycle contract for adopter-owned
Architecture Health badge Relay registrations, including identity changes,
revocation, recovery, compatibility transitions, bounded diagnostics, and the
operator CLI/runbook boundary.
## Requirements
### Requirement: Authenticated lifecycle administration preserves immutable identity
The Relay SHALL expose a bounded private admin surface for status,
reconcile-identity, revoke, recover/open, and the authenticated
recover/finalize proof-required guard,
upgrade/{stage|activate|rollback}, and uninstall. Admin authentication SHALL be
separate from publisher OIDC, and every mutating request SHALL carry an
operation ID plus expected registry revision/barrier epoch and use an atomic
generation/revocation-epoch transition where state is affected. Identity
reconciliation SHALL preserve immutable repository and owner IDs and the alias;
transfer and removal SHALL revoke and tombstone the old alias before any new
binding can be created.

#### Scenario: Rename updates only private display metadata
- **WHEN** an authenticated admin renames a registered repository while immutable repository and owner IDs remain equal
- **THEN** the alias, generation, epoch, public bytes, and lease remain unchanged
- **AND** the updated display metadata is available only through private status

#### Scenario: Stale callers cannot mutate newer Relay state
- **WHEN** a forwarded read or mutation carries a registry revision or barrier epoch older than the Durable Object
- **THEN** the request is rejected with no state mutation and an already-ready payload remains ready
- **AND WHEN** the registry revision advances without a barrier advance
- **THEN** the Durable Object synchronizes display metadata without clearing ready data
- **AND WHEN** the barrier advances
- **THEN** the Durable Object clears ready data and enters `needs-recovery` (or `revoked` for a tombstone)

#### Scenario: Transfer cannot carry authorization
- **WHEN** an admin requests an owner or immutable repository identity transfer
- **THEN** the old alias becomes revoked and tombstoned
- **AND** the response requires an explicit new registration instead of creating or authorizing a destination for the new owner

#### Scenario: Revoke beats an in-flight writer
- **WHEN** revoke or remove commits while a publish, renewal, or challenge uses the prior generation/epoch
- **THEN** the delayed operation receives a conflict and changes no state
- **AND** public reads expose no prior ready payload

#### Scenario: Destructive admin CAS is checked before the Registry barrier
- **WHEN** an authenticated revoke, remove, or transfer carries an operation ID and the current generation, revocation epoch, registry revision, and barrier epoch
- **THEN** all caller preconditions are validated before the Registry tombstone is committed
- **AND** a stale generation, epoch, revision, or barrier receives `409` with the Registry and Relay state unchanged
- **AND** a successful request returns success after the Registry barrier and Relay revocation transition, without comparing caller expectations against the post-barrier counters

#### Scenario: Admin mutations preserve caller expectations and operation identity
- **WHEN** an authenticated invalidate, recovery-open, rename, rotate, upgrade, activate, or rollback supplies an expected registry revision or barrier epoch
- **THEN** the supplied values are compared with authoritative state and are never replaced by a fresh lookup
- **AND** every mutating admin request without an explicit operation ID is rejected before state mutation

#### Scenario: Recovery finalization requires a fresh publisher proof
- **WHEN** an authenticated admin opens recovery
- **THEN** the alias enters `needs-recovery` and remains unavailable
- **WHEN** an admin calls `recover/finalize` without a publisher proof
- **THEN** the Relay returns `fresh_publisher_proof_required` with no state mutation
- **AND** a fresh publisher OIDC identity must complete a new `prepare`/`recover` challenge and commit before the alias becomes ready

### Requirement: Pin rotation and bundle compatibility fail closed
The lifecycle service SHALL rotate publisher workflow/audience pins by first
invalidating the current state and then atomically updating the private
registry. Unknown workflow revisions, bundle identifiers, contract versions,
compatibility plans, or manifest digests SHALL be rejected without mutation.
Compatible upgrades SHALL be idempotent. Bundle transitions SHALL maintain
`active_digest`, `staged_digest`, and `previous_verified_digest` and accept only
digests in an operator-controlled shipped manifest. A rollback SHALL target the
previous verified digest and SHALL refuse an incompatible target without serving
the older generation.

#### Scenario: Old pin cannot publish after rotation
- **WHEN** an admin rotates the registered workflow pin
- **THEN** the current destination is unavailable with an advanced epoch before the new pin is stored
- **AND** a publisher using the old pin cannot publish or renew

#### Scenario: Incompatible rollback is refused
- **WHEN** an operator requests rollback to an unknown or incompatible bundle/schema
- **THEN** the Relay returns a fixed compatibility conflict
- **AND** generation, epoch, registry binding, and public state remain unchanged

### Requirement: Private operational status is redacted and bounded
The Relay SHALL provide private status containing only state, generation,
revocation epoch, validity boundaries, compatibility identifiers, tombstone,
private display owner/repository names, and bounded redacted reason codes. It SHALL retain operation diagnostics for at
most 30 days and 256 records per alias. A tombstone and its monotonic
revocation barrier SHALL be retained permanently; diagnostic retention SHALL
never delete the `relay_state` security barrier. Status and diagnostics SHALL not expose
canonical payload bytes, tokens, JWT claims, source identity, repository URL,
commit/tree/PR/run identifiers, receipts, or provider response bodies.

#### Scenario: Status explains an outage without provenance
- **WHEN** storage, quota, authorization, expiry, or compatibility failure is recorded
- **THEN** private status returns its fixed reason code and current unavailable state
- **AND** the response contains none of the private payload or provider identity fields

#### Scenario: Retention remains bounded
- **WHEN** more than 256 operation records exist or records are older than 30 days
- **THEN** the Relay prunes the excess records transactionally
- **AND** it never creates an unbounded public or private history endpoint

### Requirement: Lifecycle CLI and runbook are complete and dry-run safe
The shipped CLI SHALL validate a generated v1 setup configuration and expose
status, invalidate, revoke, rename, transfer, rotate, remove, recover, upgrade,
activate, and rollback operations through the approved admin routes. It SHALL obtain the
admin token and an operator-controlled Relay origin from process environment
variables, exact-match that origin against the checked-in destination, and
never take credential destination from repository input alone. Dry-run SHALL be
read-only. The operator runbook SHALL map
every lifecycle matrix event to a positive and negative action, state the
60-minute lease/30-minute renewal and retention bounds (with permanent
tombstones), distinguish origin
truth from cached copies, and include synthetic upgrade, rollback, uninstall,
and recovery verification.

#### Scenario: Dry-run produces no external mutation
- **WHEN** an operator requests a lifecycle operation with `--dry-run`
- **THEN** the CLI prints the validated operation plan and performs no HTTP request, file write, or token lookup

#### Scenario: Real lifecycle command uses the admin boundary
- **WHEN** an operator runs a non-dry-run operation with a valid generated Relay config, `ARCHLINTERNET_BADGE_ADMIN_TOKEN`, and `ARCHLINTERNET_BADGE_ADMIN_ORIGIN`
- **THEN** the CLI sends only the approved bounded request to the configured Relay admin route
- **AND** the operator origin exactly matches the checked-in destination origin before the bearer token is used
- **AND** it returns the fixed status/reason result without printing the bearer token or private provenance

#### Scenario: Runbook recovery starts unavailable
- **WHEN** a restore, provider outage, or revoked pin is detected
- **THEN** the documented action keeps the origin unavailable until an owner-authorized current proof succeeds
- **AND** it does not rescan `main`, trust a backup, or promise deletion of cached internet copies
