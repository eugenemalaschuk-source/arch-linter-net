# Badge Relay lifecycle operations

> Prepublication candidate guidance: use only an explicitly verified candidate.
> This page does not establish stable-release availability. Start with
> [badge adoption](badge-adoption.md) for transport, disclosure, and evidence
> prerequisites.

This runbook is the operator procedure for a registered `badge-relay/v1`
destination. Lifecycle changes are authenticated administrative actions; public
badge reads never expose the registry, operation journal, payload, or provider
provenance.

## Plan first

Use the checked-in Relay setup file and inspect the exact operation before
making a change:

```text
arch-linter-net badge architecture-health lifecycle \
  --operation status --input badge-relay-config.json --dry-run
```

For a real request, provide the short-lived administrative credential through
the environment only:

```text
export ARCHLINTERNET_BADGE_ADMIN_TOKEN='short-lived-admin-token'
export ARCHLINTERNET_BADGE_ADMIN_ORIGIN='https://relay.example'
arch-linter-net badge architecture-health lifecycle \
  --operation status --input badge-relay-config.json
```

The Relay deployment must also configure `RELAY_SHIPPED_BUNDLE_DIGESTS` as an
operator-controlled comma-separated manifest of verified bundle digests. Stage,
activate, and rollback refuse any digest outside that manifest.

The command validates relay mode, an HTTPS origin, and an opaque alias. The
operator-controlled origin is required and must exact-match the checked-in
destination origin; the repository file cannot redirect the credential to a
different host. It
never accepts a token as a command-line argument and prints only the bounded,
redacted status contract.

## Lifecycle matrix

| Event | Positive operation | Required negative check |
| --- | --- | --- |
| Rename | `--operation rename --new-owner OWNER --new-repository REPO` | Change either immutable numeric repository ID; expect `identity_mismatch`. |
| Transfer | `--operation transfer --new-owner OWNER --new-repository REPO` | Omit explicit confirmation; expect `explicit_confirmation_required`. Transfer revokes the old alias and requires a fresh setup plan. |
| Revoke/tombstone | `--operation revoke` | Retry with stale generation/epoch; expect a conflict and no payload resurrection. |
| Temporary suspension / expiry | `--operation invalidate` | Attempt a publisher write with the old generation; expect a stale/CAS rejection. |
| Repository deletion/visibility change | `--operation invalidate` after the provider check no longer permits publication | Keep the alias unavailable until a fresh identity reconciliation and proof; never infer deletion from a caller-authored flag. |
| Disclosure withdrawal | `--operation revoke` | Public reads become unavailable; delayed publish/renew/recover attempts must not restore readiness or reuse the tombstoned alias. |
| Remove/uninstall | `--operation remove` | Omit confirmation; expect `explicit_confirmation_required`; the alias remains tombstoned and cannot be reused. |
| Pin rotation | `--operation rotate --workflow-ref <ref> --workflow-sha <40-hex>` | Publish with the old workflow pin; expect authorization failure. |
| Upgrade | `--operation upgrade --to <shipped-digest>` followed by `--operation activate --to <shipped-digest>` | Unknown bundle, contract, compatibility plan, or manifest digest; expect `compatibility_conflict`. |
| Rollback | `--operation rollback --to <previous-verified-digest>` | Any unshipped or incompatible digest; expect a refused rollback. |
| Recovery | `--operation recover` (open), then rerun the publisher workflow | Omit confirmation or try to publish before fresh proof; expect `explicit_confirmation_required` or `fresh_publisher_proof_required`. The admin `/recover/finalize` route is a proof-required guard, not a state transition. |
| Status/outage | `--operation status` | Storage outage is reported as `storage_unavailable`; no private payload or token is returned. |
| Abandoned alias | `--operation revoke` (or `remove`) | Re-registering the tombstoned alias is refused; allocate a new opaque alias instead. |

Every completed mutation advances the generation/revocation epoch and is
guarded by the registry revision and barrier epoch. Revoke/remove/transfer use
a two-phase fence: Relay first atomically clears public bytes and blocks
publisher/challenge writes, then the Registry CAS tombstones the alias, and a
final Relay step commits the revoked generation. A delayed writer therefore
loses even while the Registry request is in flight. If the Registry CAS loses a
race, the alias remains unavailable and the same operation ID can be retried
with a fresh status snapshot; no stale caller is reported as a successful
tombstone. When using optimistic concurrency, supply all four expected
counters from the same private status snapshot; stale values fail with `409`
before a Registry tombstone or other mutation is committed. Every mutation
also requires its own explicit operation ID for retry tracing and idempotency.

## Recovery and rollback

Recovery applies only to a non-tombstoned registration. A tombstoned alias
cannot be recovered; use a new alias and explicit consent, followed by fresh
setup and qualifying publisher proof. Invalidation suspends publication but
does not permanently withdraw consent; use revoke or remove for that boundary.

1. Capture the redacted status and operation identifier.
1. If a restore, registry mismatch, or storage incident occurred, run
   `--operation recover --dry-run`, then repeat with the administrative token
   and explicit confirmation.
1. Re-run the publisher workflow to obtain fresh OIDC identity and proof; its
   new `prepare`/`recover` challenge and commit finalize recovery. A backup
   payload, token, or old challenge is not a recovery proof. The admin
   `/recover/finalize` endpoint intentionally refuses without that proof.
1. For upgrades, stage and activate the exact shipped bundle digest. Rollback
   is permitted only to the previous verified digest under the same v1
   compatibility plan; incompatible rollback is refused closed.

Keep the redacted status response and operation ID with the deployment record.
The Relay retains at most 256 operation records for 30 days. An alias tombstone
and its monotonic revocation barrier are permanent; the 90-day bound is only a
minimum operational-retention target and never authorizes deleting the security
state. Entries contain reason codes and generation metadata only; payload
bytes, tokens, URLs, and provider evidence are never written to the operator
journal.

## Synthetic acceptance checks

Use a fresh disposable alias for each independent scenario. Do not execute
every matrix row sequentially against one alias: transfer, revoke, and remove
permanently tombstone it. Exercise recovery and the compatible upgrade/rollback
sequence on a separate non-tombstoned alias with the required verified bundles.
Allocate another fresh alias for each terminal operation and assert that it
cannot be re-registered or recovered afterward.

For each scenario, establish its required registration, consent, and qualifying
publisher proof first. Capture the pre-operation counters, execute the intended
mutation, then test stale requests with those old counters or an altered
immutable ID. Test refusal cases before a terminal operation or on a separate
alias with the same starting conditions, so a tombstone does not mask the
specific refusal being tested.
Verify that public JSON/SVG reads return only the fixed representation, that a
revoked alias returns an unavailable response, that the old pin cannot publish,
and that status remains redacted after Durable Object eviction. Record the
command output, HTTP reason code, generation, and barrier epoch; do not record
the administrative token or payload.
