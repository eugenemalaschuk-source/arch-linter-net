# Adopt an Architecture Health badge

> **Experimental / opt-in Private Relay.** Full hosted/lifecycle acceptance is
> still pending. Included Relay code is not an adoption-stable or turnkey support
> claim; verify the exact release or candidate and matching distribution.
> Private repositories default to `none`: no automatic cloud setup or badge egress.
> Stable core governance, private reports, and public `github-raw` snapshots do not
> require Relay.

Relay/OIDC is optional transport, not a prerequisite for core governance or
private reporting. A consumer that already owns a trusted badge transport may
independently verify exact producer provenance and publish only the canonical
badge projection through its own trusted post-merge workflow and adopter-owned
hosting/storage. That transport's credentials, fail-closed receipt checks,
availability, and lifecycle remain consumer responsibilities. This is not a
fourth ArchLinterNet transport mode, protocol, or hosted service.
An Architecture Health badge is a deliberately limited public projection of
PR-authoritative evidence, not a second architecture evaluator. Choose the
transport and disclosure first, then use [setup](badge-setup.md) for
executable commands and [lifecycle operations](badge-lifecycle-operations.md)
for administration. Those guides own the command examples; this page explains
which path to choose and what its result does, and does not, prove. The
[distribution and compatibility reference](../reference/badge-distribution.md)
identifies the components and immutable identities that must accompany the CLI.

## Experimental support boundary

Relay infrastructure belongs to the adopter. Included code and successful unit
or package tests do not prove the complete hosted/lifecycle path on a particular
provider account. Full adoption acceptance remains pending; no free hosting or
SLA is promised. Existing core governance, required PR checks, private reports,
and public static badges remain usable without adopting Relay.

Experimental status is not a waiver for known security, privacy, integrity,
data-corruption, or false-PASS defects. Unsafe or false-success paths must be
blocked or fixed, not relabeled as acceptable experimental behavior.

Bootstrap is read-only and produces a private, content-verified handoff. The
owner applies it through a normal protected setup PR, without a GitHub App or
PAT writer. Subsequent OIDC publication updates only Relay state: it does not
commit a badge update to the consumer repository on every PR or renewal.

## Choose the disclosure boundary

| Mode | Appropriate use | Publication and prerequisites |
| --- | --- | --- |
| `none` | Private checks, reports, and artifacts without a public badge | No public URL, external badge egress, hosting account, or publication credential. This is the private default. |
| `github-raw` | Existing public-repository static snapshots | Public repository only. Private raw URLs are rejected; do not put a token in a README URL to bypass that restriction. |
| `relay` | Experimental, opt-in bounded-freshness publication from a private or public repository | Adopter-owned Cloudflare Worker and SQLite Durable Objects, approved disclosure, verified capabilities, and the shipped compatible bundle. No custom server code. |

Custom or authenticated URL transports are not turnkey private-publication
paths without their own independent acceptance. Selecting a transport does not
change the architecture policy, required PR check, or private reporting.
`badge architecture-policy` is a separate existing badge and is unchanged.

For Relay, explicitly approve the destination hostname, opaque alias, and one
of the two profiles before setup writes anything:

| Profile | Approved public representation | Important limitation |
| --- | --- | --- |
| `headline-only/v1` | Minimal headline JSON for snapshot/Shields compatibility | No fresh-origin or current-main guarantee can be inferred from a cached image. |
| `headline-plus-freshness/v1` | Headline plus bounded freshness, with direct stamped SVG as the default README path | The visible absolute expiry remains meaningful in a cached copy; it does not make caches revocable. |

The exact `headline-only/v1` JSON fields are `schemaVersion`, `label`, `message`,
and `color`. Gate, Health, ignores and rules counts appear in the fixed message;
unknown assessment/counts remain `UNASSESSABLE` and `?`, not zero.
`headline-plus-freshness/v1` adds only `verified_at` and `valid_until`. These
additional timestamps require their own disclosure approval; do not silently
upgrade a headline-only registration or add free-form fields.

Only the approved canonical projection may leave the private workflow. Do not
publish full Health JSON, findings, reports, source paths, repository identity,
commit/tree SHAs, PR/run identifiers, private receipts, or raw provider
responses. No secrets belong in URLs, README blocks, generated public output,
or committed configuration. An opaque alias is not a promise that an observer
cannot associate a hostname or README with the source repository.

These are anonymous-reader limits, not a promise that infrastructure providers
see nothing. GitHub handles repository and workflow metadata; the Relay host
processes authentication claims and the private ownership registry. Keep the
registry, configuration and full acceptance traces private. The runtime Relay
is not given a source-reading PAT or GitHub App credential.

## Verify the candidate and manual prerequisites

Use the same verified CLI/package, configuration schema, Relay bundle,
workflow/action pins, and manifests throughout setup and acceptance. Record the
exact version, source commit, package and distribution checksums, publisher
pins, and generated-output manifest in the private deployment record. A source
checkout, a moving branch, or a successful local unit test is not evidence that
the matching installable release assets exist or work. Do not invent a download
URL, copy missing bundle files by hand, or mix independently chosen versions.

The adopter must supply the hosting account, authorize scoped provider access,
and review the real provider plan, permissions, rules/check visibility, quotas,
and billing implications. The setup guide owns the live-inspection and
credential procedure. A plan label is descriptive, not evidence that a private
repository supports the required checks, Rules API visibility, OIDC trust, or
hosting capabilities. Missing or unprovable capabilities leave publication
unavailable; the installer must not weaken verification to make it green.

Review the read-only setup preview before applying it. It identifies the
repository and owner by immutable numeric IDs, the selected base ref and
required producer check, approved endpoint/profile, workflow pins, generated
files, and optional renewal cadence. Inspect the generated workflow, Relay
configuration, registry binding, and README block as an ordinary repository
change. Preserve unrelated README content, workflows, rules, and secrets.

The generated producer's Git-blob SHA is not the trusted reusable publisher's
commit pin. Both identities must match the reviewed setup. Provider credentials
are administrative inputs; the runtime publisher uses the approved OIDC trust
boundary rather than a token embedded in a public URL. A fresh install must
remain unavailable until real qualifying evidence exists.

## First authoritative PR to README

Run the selected producer check in a PR, review its canonical architecture
result, and squash-merge through the approved path. The publisher must prove
that the qualifying PR evidence matches the current target tree and the
required identity, workflow, and check constraints before the Relay can serve
it as ready. A manually copied Health file, an untrusted workflow success, or a
rescan of `main` does not substitute for that proof.

A new valid receipt can refresh an unchanged headline; text equality alone is
not proof that no new authoritative evaluation occurred. Conversely, an old
healthy headline does not authorize publication for a changed tree. The setup
guide's doctor procedure distinguishes missing first evidence, incompatible
pins, inaccessible checks/rules, expired evidence, and endpoint problems.

Health, Gate, and violation counts come from the canonical CLI result. Relay
publication availability is a different state: an unavailable badge means the
public claim cannot currently be verified, not that the architecture passed or
failed. Keep the private required PR check and report as the decision surface.
The badge is not an instantaneous current-main oracle.

## Renewal, expiry, and cost

Renewal is optional and metadata-only. It revalidates the approved evidence,
identity, and current-tree relationship; it never re-analyzes `main` or creates
a replacement architecture result. It cannot extend the canonical semantic
horizon, revive a revoked alias, or repair unknown/missing evidence. Once proof
has expired or no longer qualifies, obtain a new approved PR-authoritative
proof rather than continually renewing the old headline.

The lease is bounded by both the publication lease and canonical semantic
validity. At the origin, expiry is enforced on read even after all publishers
and renewal jobs stop. The setup preview exposes the selected cadence and
cost: the v1 ceiling is a 60-minute lease and no more than one renewal attempt
per 30 minutes (48 per day). These are limits, not GitHub scheduling or uptime
guarantees. Review private Actions billed-minute implications, Worker/Durable
Object quotas, and the chosen provider plan; no universally free service is
promised. Disabling renewal emits no scheduled renewal workflow.

## Diagnose origin and cached representations separately

Start with doctor and the Relay origin's JSON/direct SVG, then compare any
Shields renderer, GitHub Camo proxy, browser, or offline copy. A ready origin
with a stale proxy image and an expired/unavailable origin with an old healthy
image are different incidents. Retain redacted reason codes and observed expiry
in the private deployment record; do not paste provider responses or tokens
into public issues.

Use the direct stamped SVG only after approving `headline-plus-freshness/v1`.
Its absolute expiry is still visible when the image is cached. Neither that
stamp nor a cache-control header recalls a previously served image. Revocation
and read-time expiry control the origin; GitHub Camo, Shields, browsers, and
offline copies cannot be universally recalled. A cached badge is not proof of
current origin readiness.

## Operate, migrate, and remove

Use the [lifecycle runbook](badge-lifecycle-operations.md) for status, rename,
transfer, invalidate, revoke/tombstone, remove/uninstall, pin rotation,
upgrade/activate, rollback, and recovery. Administrative credentials stay in
the environment and are bound to the approved origin. Transfers require a
fresh setup identity; restoration requires fresh publisher proof rather than
replaying a backup. Only verified shipped bundle digests may be activated or
rolled back. Do not silently substitute an unshipped digest or reuse a
permanently tombstoned alias. Review publisher workflow-pin rotation separately
from operator credential rotation. During a suspected compromise, contain
publication and rotate the affected authority; do not promise erasure of copies
that have already been published.

Use `invalidate` for a temporary suspension, `revoke` to permanently withdraw
disclosure consent, and `remove` to uninstall. Invalidation is not a substitute
for consent withdrawal: it makes the current payload unavailable without
permanently tombstoning the alias. Follow the authenticated lifecycle procedure
and verify that the origin is unavailable. For revoke/remove, also verify that
delayed publishers and renewal attempts cannot restore readiness.

A revoked or removed alias stays tombstoned. Publishing again requires a new
alias, explicit consent, a fresh setup, and new qualifying publisher proof;
recovery or replaying an old backup cannot undo the tombstone. When uninstalling,
review removal of the managed README/workflow blocks and adopter-owned hosting
resources. Keep private PR checks/reports and unrelated repository content
intact. Stopping scheduled jobs or deleting a README block alone is not immediate
revocation, and removing a deployment does not recall cached images.

Public `github-raw` users may retain their existing static-snapshot workflow.
Migration to Relay is an explicit new disclosure/setup decision, not a silent
upgrade to stronger freshness guarantees. Private users may remain on `none`;
there is no requirement to publish a badge to keep architecture governance.
