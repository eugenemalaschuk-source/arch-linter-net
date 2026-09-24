# Experimental Relay setup and doctor

This page is for the **ArchLinterNet Relay**, not for ordinary CI or an
independently hosted badge. Start with [badge adoption](badge-adoption.md) to
choose a path. For a consumer-owned Worker/publisher, use
[direct hosting](badge-direct-hosting.md); the setup below does not configure it.

> **Experimental / opt-in Private Relay.** Full hosted/lifecycle acceptance is
> still pending. Included Relay code is not an adoption-stable or turnkey support
> claim; verify the exact release or candidate and matching distribution.
> Private repositories default to `none`: no automatic cloud setup or badge egress.
> Stable core governance, private reports, and public `github-raw` snapshots do not
> require Relay.

The packed CLI, configuration schema, Relay bundle, and pinned workflow/action
must form a [verified compatible distribution](../reference/badge-distribution.md).
A source checkout or successful unit test is not proof that a hosted installation
works. Setup plans disclosure and cost before writing managed files; it does
not run the architecture evaluator.

## Choose a transport

The setup command supports these built-in modes:

| Mode | Use | Hosting requirement |
| --- | --- | --- |
| `none` | Private checks and artifacts without public disclosure | None; no external badge call |
| `github-raw` | Public static snapshot compatibility | Public repository only |
| `relay` | Experimental bounded-freshness publication | Your Cloudflare Worker and SQLite Durable Object account |

Private repositories default to `none`; private `github-raw` is rejected.
These are the setup command's modes, not a ban on independent direct hosting.
Custom URLs and broad deployment credentials are not substitutes for Relay's
reviewed configuration and OIDC trust. An opaque alias does not hide the hosting
hostname or guarantee that the source repository cannot be identified.

## Preview and apply

Read the installed command's options first:

```text
arch-linter-net badge architecture-health setup --help
arch-linter-net badge architecture-health doctor --help
```

Use a reviewed dry-run first. Select the approved destination, alias, numeric
repository/owner identities, exact base ref, producer workflow/check, publisher
pins, and disclosure profile. `headline-only/v1` is JSON/Shields compatibility;
`headline-plus-freshness/v1` adds approved timestamps and the stamped SVG view.
Do not silently upgrade the disclosure profile.

For enabled renewal, review cadence, Actions usage, provider quotas and billing.
Relay caps a lease at 60 minutes and renewal at no more than once per 30 minutes
(48 attempts per day). These are limits, not scheduling or uptime guarantees.
Disabling renewal emits no scheduled renewal workflow and excludes `schedule`
from the registry entry. Non-hour-aligned cadences may use multiple cron entries;
the preview uses `floor(1440 / cadence_minutes)` slots without shortening the
configured cyclic interval. These Relay-specific limits do not govern direct
hosting.

`--provider-plan` is optional cost metadata, not evidence that required checks,
Rules API visibility, OIDC, permissions or quota work. A null plan label is valid
when live inspection proves the required capabilities. Missing or contradictory
capabilities leave the plan unavailable; setup must not weaken verification.

### Bootstrap in the trusted publisher

The first non-dry-run Relay write needs live inspection in the exact pinned
reusable publisher context. A local shell cannot mint its publisher-bound OIDC
claim. Configure the required check/ruleset and approve disclosure first, then
use the compatible reusable workflow's `operation: bootstrap`.

Bootstrap checks out the configured consumer base, obtains short-lived OIDC,
runs setup, and produces a private `architecture-health-bootstrap-handoff`
artifact. It does **not** push a branch, create a PR, or write a consumer Git
reference. No GitHub App key or PAT writer is required. A caller-authored
`--capability-evidence` JSON file is rejected in v1; shape and freshness do not
authenticate its origin.

Download the handoff from that trusted bootstrap run. Keep it private and retain
all manifest-bound files, including hidden `.github` paths. On a local review
branch at the recorded exact base, verify and apply it:

```text
arch-linter-net badge architecture-health apply-handoff \
  --input ./architecture-health-bootstrap-handoff/bootstrap-handoff.json \
  --payload ./architecture-health-bootstrap-handoff/payload \
  --output . \
  --expected-base-sha <handoff base.sha> \
  --expected-base-tree-sha <handoff base.tree_sha> \
  --repository <handoff repository.owner/name> \
  --repository-id <handoff repository.repository_id> \
  --repository-owner-id <handoff repository.repository_owner_id>
```

The angle-bracket fields are values from the verified handoff, not literal shell
arguments. The command independently checks local `HEAD` and `HEAD^{tree}`,
repository identity, permitted paths, UTF-8 contents, byte counts and digests.
Stale branches, extra paths, reparse points, or mismatched bytes are rejected
before writing managed files. Do not commit first: the exact base is replay
protection. Inspect the applied diff and submit an ordinary protected setup PR.

This is a one-time owner review. Later publication uses the pinned GitHub OIDC
publisher to update Relay state, not to commit badge refreshes to the consumer.

### Local preview and generated files

A local preview remains useful but does not replace trusted bootstrap.
Substitute your approved values in this example:

```text
arch-linter-net badge architecture-health setup \
  --repository owner/name --visibility private --mode relay \
  --repository-id 123456 --repository-owner-id 654321 \
  --account 0123456789abcdef0123456789abcdef --alias a7f4k2m9 \
  --endpoint https://relay.example --audience architecture-health-badge-relay/a7f4k2m9 \
  --approve-disclosure --output . --dry-run
```

The reviewed plan generates versioned configuration and a SHA-256-bound
manifest, the compatible Worker/Durable Object source and Wrangler/migration
configuration, producer/publisher workflows, optional renewal, and a managed
README block. No custom server implementation is required by this Relay path.

`producer.workflow_sha` is the **Git blob SHA** of the generated producer bytes,
not the trusted publisher's **commit SHA**. Use the installed options
`--base-ref`, `--policy`, `--solution`, `--producer-workflow`, `--check-name`,
and `--audience` when defaults do not match your repository. Registry and
Wrangler configuration must contain the actual approved identities and pins,
not fixture values.

Review the generated diff. Setup preserves unrelated README/workflow content,
secrets, and protection rules; it writes atomically and reuses the approved
alias/deployment on retries. A conflict or partial failure leaves publication
unavailable. `none` emits no public URL. No mode seeds a healthy badge before
qualifying evidence exists.

## First publication

Run the required producer in an actual PR and merge through the approved path.
The publisher must prove the exact merged-tree, workflow/check, run/attempt,
artifact and disclosure relationship before the origin becomes ready. Do not
replace that proof with a copied Health file, a green unrelated workflow, or a
new scan of main.

Verify the origin's JSON and selected SVG against the accepted projection;
inspect cached Shields/Camo images separately. Health/Gate remain architecture
facts, while readiness describes whether the public claim can be verified.
Follow [badge verification](badge-adoption.md#verify-the-result).

## Doctor and recovery

Doctor checks component compatibility, local producer pins, identities, selected
endpoint and capabilities. Without an observation file it performs bounded
read-only inspection. A newly generated raw/Relay configuration remains
unavailable until it serves real first evidence.

Where an approved inspector performs remote-only checks, supply its fresh
identity-bound observation:

```text
arch-linter-net badge architecture-health doctor \
  --input badge-relay-config.json \
  --observation ./badge-relay-doctor-observation.json
```

The observation covers identity/pins/OIDC, required checks/rules, evidence,
expiry, revocation, quota, and cache freshness. A plan flag cannot manufacture
a healthy result. Keep detailed operator observations and provider responses
private; public diagnostics must not leak repository/source/run identities or
authentication material.

Relay enforces lease expiry on reads even after publishers and renewal stop.
Renewal revalidates metadata; it does not rerun analysis or extend the canonical
semantic horizon. Expired evidence requires new approved PR-authoritative proof.
Revoked aliases cannot be revived by replay or backup restoration. Cached copies
cannot be universally recalled.

Use the [lifecycle runbook](badge-lifecycle-operations.md) for authenticated
status, renewal, invalidate, revoke/remove, transfers, pin/key rotation,
upgrades, rollback and recovery. Its procedures apply to Relay only. Successful
local commands or a docs build do not establish hosted/lifecycle acceptance.
