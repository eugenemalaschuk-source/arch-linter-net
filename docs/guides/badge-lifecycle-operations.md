# Badge Relay lifecycle operations

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
| Disable/expire | `--operation invalidate` | Attempt a publisher write with the old generation; expect a stale/CAS rejection. |
| Repository deletion/visibility change | `--operation invalidate` after the provider check no longer permits publication | Keep the alias unavailable until a fresh identity reconciliation and proof; never infer deletion from a caller-authored flag. |
| Disclosure withdrawal | `--operation invalidate` | A public read must become unavailable without returning the previous payload. |
| Remove/uninstall | `--operation remove` | Omit confirmation; expect `explicit_confirmation_required`; the alias remains tombstoned and cannot be reused. |
| Pin rotation | `--operation rotate --workflow-ref <ref> --workflow-sha <40-hex>` | Publish with the old workflow pin; expect authorization failure. |
| Upgrade | `--operation upgrade --to <shipped-digest>` followed by `--operation activate --to <shipped-digest>` | Unknown bundle, contract, compatibility plan, or manifest digest; expect `compatibility_conflict`. |
| Rollback | `--operation rollback --to <previous-verified-digest>` | Any unshipped or incompatible digest; expect a refused rollback. |
| Recovery | `--operation recover` | Omit confirmation or try to publish before fresh proof; expect `explicit_confirmation_required` or `fresh_publisher_proof_required`. |
| Status/outage | `--operation status` | Storage outage is reported as `storage_unavailable`; no private payload or token is returned. |
| Abandoned alias | `--operation revoke` (or `remove`) | Re-registering the tombstoned alias is refused; allocate a new opaque alias instead. |

Every mutation advances the generation/revocation epoch and is guarded by the
registry revision and barrier epoch. A delayed writer therefore loses to a
revocation, rotation, restore, or recovery transition. Registry and Relay
state are reconciled before a subsequent publish is accepted.

## Recovery and rollback

1. Capture the redacted status and operation identifier.
1. If a restore, registry mismatch, or storage incident occurred, run
   `--operation recover --dry-run`, then repeat with the administrative token
   and explicit confirmation.
1. Re-run the publisher workflow to obtain fresh OIDC identity and proof; a
   backup payload, token, or old challenge is not a recovery proof.
1. For upgrades, stage and activate the exact shipped bundle digest. Rollback
   is permitted only to the previous verified digest under the same v1
   compatibility plan; incompatible rollback is refused closed.

Keep the redacted status response and operation ID with the deployment record.
The Relay retains at most 256 operation records for 30 days and keeps an alias
tombstone for at least 90 days. Entries contain reason codes and generation
metadata only; payload bytes, tokens, URLs, and provider evidence are never
written to the operator journal.

## Synthetic acceptance checks

For a disposable alias, execute each positive row above in order, then run its
negative check with the previous generation/epoch or an altered immutable ID.
Verify that public JSON/SVG reads return only the fixed representation, that a
revoked alias returns an unavailable response, that the old pin cannot publish,
and that status remains redacted after Durable Object eviction. Record the
command output, HTTP reason code, generation, and barrier epoch; do not record
the administrative token or payload.
