# Architecture Health badge adoption

> **Upcoming candidate, not a released capability.** The Relay setup and
> lifecycle described here belong to the upcoming v0.8.x completeness delivery.
> Source availability is not packed or hosted acceptance. Use only an explicitly
> reviewed, compatible candidate for evaluation; do not assume the currently
> published CLI includes this path. Existing public raw badges remain supported.

Start with the transport and disclosure decision below, then follow
[Turnkey badge setup](badge-setup.md) for the executable command sequence.
[Distribution and compatibility](../reference/badge-distribution.md) identifies
what must accompany the tool. [Lifecycle operations](badge-lifecycle-operations.md)
is the single operator runbook; this page does not replace it with another
installer or a collection of publisher scripts.

## Choose what may leave the repository

| Mode | Audience and disclosure | Operational requirement |
| --- | --- | --- |
| `none` | Checks, reports and evidence stay private; no public badge publication | No Relay account or hosting credential; private default before consent |
| `github-raw` | Public repository snapshot; no request-time freshness guarantee | The existing public raw publication path; no new hosting account |
| `relay` | Approved headline is anonymously readable even when the source and README are private | Adopter-owned Cloudflare Worker and SQLite Durable Object, explicit consent and compatible pinned components |

Private `github-raw` is incompatible with an anonymously rendered README
badge. A failed visibility check is a reason to select `none` or review Relay
disclosure, not to put a GitHub token in a URL. Authenticated endpoints,
mirrors, Pages, object storage and custom publishers are extension space, not
additional turnkey-supported transports.

There is no mandatory ArchLinterNet-operated service. Owning the Relay does not
make its public endpoints private. An opaque alias is a locator, not a password;
the hostname, custom domain, DNS and association with a hosting account can
still disclose identity. Review the final endpoint and README diff before
approving it, not only the JSON content.

### Exact disclosure profiles

`headline-only/v1` permits the canonical Gate, Health, ignores count and rules
count in the fixed message. Its exact JSON fields are `schemaVersion`, `label`,
`message` and `color`. It is the JSON/Shields snapshot view; it does not
authorize extra free-form messages, source details or timestamps. Unknown
assessment/counts remain `UNASSESSABLE` and `?`, not a fabricated zero.

`headline-plus-freshness/v1` additionally permits the bounded `verified_at` and
`valid_until` timestamps. With that consent, the default README view is the
direct stamped SVG. Do not enable this profile silently for an owner who has
approved only the headline. These timestamps are the only additional JSON
fields in the freshness profile. Publication keeps the exact approved
canonical bytes, rather than accepting arbitrary extra fields.

Full Health JSON, findings, policy, paths, repository identity, source/tree
SHA, PR/run identifiers, JWTs and full receipts stay outside the anonymous
publication surface. Rejecting an invalid payload is different from sanitizing
it and publishing different bytes under its old digest. Private evidence links
belong in the private repository, not in public Relay redirects.

These are **anonymous-reader** limits, not a claim that infrastructure providers
see nothing. GitHub handles repository and workflow metadata. The Relay host
processes authentication claims and the private ownership registry. Deployment
and administrative credentials remain in the operator context; the source
publisher uses the approved GitHub OIDC identity. The Relay is not given a
source-reading PAT or GitHub App credential. Keep config, registry, detailed
observations and receipts private even when they contain no bearer secret.

## Approvals and prerequisites

Review the installed tool's setup preview before applying managed changes.
The unavoidable manual decisions are ownership of the GitHub repository and
hosting account, disclosure including hostname, permissions, the protected
branch and required check, and budget. Setup does not silently modify
rulesets, secrets or the default branch. Generated workflows and README blocks
are a reviewable diff, not permission to merge or deploy them.

The supported reference provider is Cloudflare Worker plus SQLite Durable
Object. Account access alone is not sufficient: live inspection must prove
the configured account, Worker/Durable Object capability and quotas. On GitHub,
the configured required PR check and supported branch-rules APIs must be
visible, and the approved reusable-workflow OIDC identity must be verifiable.
A plan name or a caller-authored observation of success is not proof of these
capabilities. Unsupported plan/API/permission combinations stop with a bounded
diagnostic; there is no name-only or weaker-check fallback.

Use the credential channels and commands in the [setup guide](badge-setup.md).
Neither credentials in Markdown/URLs nor a manually patched bundle are an
installation workaround. A missing or incompatible distribution component is
a delivery blocker. Provider/account approvals and a real hosted acceptance
run are not implied by downloading the candidate or reading this guide.

## First PR to a verified snapshot

The intended sequence uses the components generated by setup:

1. Review the pinned tool, configuration, producer and publisher workflows.
   Run the authoritative architecture check in a PR and retain its canonical
   Health and bound promotion evidence. Before first qualifying evidence, the
   publication is unavailable; setup does not seed a healthy badge.
2. Squash the qualifying PR through the required gate. The trusted publisher
   verifies the exact merged tree, approved producer, run/attempt/check and
   artifact identities, disclosure and current authorization before promotion.
   It does not execute consumer-controlled source in the privileged job.
3. Inspect the anonymous origin and the README representation separately.
   Keep the full verification receipt private. Authenticated access to a
   private README is not a test that its image endpoint is private.

The CLI owns Gate, Health and counts. A successful Actions run, an HTTP upload
or an available Relay is not a second architecture evaluator. Conversely,
**publication unavailable is not an architecture FAIL**: inspect the private
check/report to determine the canonical result and doctor for delivery errors.

A newly verified tree can have unchanged headline bytes and a new receipt,
generation and validity window. Lack of a visual headline change is not lack
of verification. A direct push, mismatched tree, unsupported merge shape,
missing/expired artifact or unprovable context cannot be promoted as a fresh
healthy snapshot.

## Freshness is bounded, not instantaneous

The Relay checks the stored deadline on every origin read. Ready requires
both an unexpired head-observation lease and the canonical semantic validity
horizon. The effective bound is the earlier of those deadlines. Copying,
retrying or reading evidence does not move it; the transport does not
recompute waiver or external-evidence semantics.

Optional renewal revalidates metadata, required checks, evidence and ownership;
it does not compile or re-analyze `main`. The same tree is insufficient after
a waiver, evaluation-date or external-evidence horizon expires. Unknown
validity is not infinite validity. After long inactivity or lost evidence,
obtain approved PR-authoritative evidence for the current tree; a manual
healthy value, backup payload or successful admin command is not a substitute.

A missed publisher or renewal run cannot keep the origin ready beyond its
bound, even when every scheduler is stopped. However, the origin is not an
instantaneous oracle for a newly pushed head, transfer or deletion that it has
not observed. Detection is followed by invalidation; absent detection, finite
expiry bounds the old observation. During a complete runtime/network outage,
an unavailable image is possible rather than a guaranteed grey SVG.

### Cached images and diagnosis

The stamped SVG includes a visible absolute UTC verification/validity window
in the same image as the headline. A saved image retains that original bound;
it must not be described as always-current `main`. Bare Shields and raw JSON
are compatibility snapshots, not a substitute for the stamped view.

Diagnose **origin first, renderer second, proxy last**. Use doctor and a fresh
origin read to distinguish expired/revoked evidence or storage/authentication
failure from rendering. Then compare the direct SVG with optional Shields and
the README/Camo/browser copy. If origin is unavailable while an old image is
still visible, do not renew expired evidence or weaken verification to make
those displays agree. Origin cache headers, ETag/HEAD behavior and visible
stamps cannot recall every Camo, browser or offline copy. Record observed
cache delays as observations, not a universal delivery SLA.

## Budget and ongoing operations

The setup contract caps leases at 60 minutes and enabled renewal at no more
than once per 30 minutes: at most 48 scheduled attempts per day, or 1,440 in a
30-day planning month, before PR-triggered work and retries. These are job
counts, not billed minutes or a hosting-price quote. Review the current account
allowances, measured duration, request/storage usage and provider limits.
Setup previews the selected cadence and quota assumptions; `--provider-plan`
is optional cost metadata, not a capability or zero-cost guarantee.

Renewal is optional. Disabled renewal produces no scheduled renewal workflow;
read-time expiry remains active. A longer cadence can intentionally leave
unavailable intervals. Do not increase the lease to conceal missing jobs or
cost. Quota, JWKS, storage and runtime failures must not become a
last-known-green fallback.

Use [lifecycle operations](badge-lifecycle-operations.md) for rename, transfer,
revoke/remove, pin rotation, restore, recovery, upgrade and compatible rollback.
Distinguish temporary invalidation from permanent consent withdrawal: stop
future publication with the authenticated revoke/tombstone procedure before
removing workflows or resources. A backup cannot recreate lost permission or
revive a revoked alias. Restore requires owner authorization and fresh proof;
incompatible rollback must be refused. Review workflow-pin changes separately
from operator credential rotation. During a suspected credential or publisher
compromise, contain future serving/writes and rotate the affected authority;
do not promise erasure of already published copies. Uninstall includes both
reviewable repository changes and provider-resource cleanup.

## Migrate without breaking existing users

An existing public `github-raw` badge can remain a static snapshot. Relay is an
explicit migration for different privacy/freshness needs, not a requirement to
open a hosting account. Preserve the existing required check, raw endpoint and
private evidence navigation while reviewing any generated changes.

The legacy `badge architecture-policy` command remains a compatibility path;
this delivery does not redefine it or change canonical Architecture Health
semantics. For a private repository, choose `none` until disclosure is approved,
then follow setup with one compatible distribution. Never expose private Git
history through a public raw mirror, add token-bearing image URLs, or call an
unpublished candidate a shipped upgrade.
