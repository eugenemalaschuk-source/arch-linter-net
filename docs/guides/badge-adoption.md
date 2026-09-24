# Adopt an Architecture Health badge

Get the [required CI check](ci-integration.md) working first. A public badge is
optional: it displays an ArchLinterNet result, not a GitHub workflow status,
and it does not replace the private report or merge gate.

## Choose the disclosure boundary

Choose a publication path, not a hosting product before you have a result:

| Your situation | Path | Start here |
| --- | --- | --- |
| You need private checks and reports, not a public badge. | No publication; `none` is the built-in private default. | Keep your CI artifacts private. No badge setup command or hosting account is needed. |
| Your repository and badge data are public. | A static public snapshot, including `github-raw`. | [Public snapshots](#public-snapshots). |
| You have, or will maintain, your own trusted publisher and hosting. | Consumer-owned direct publication, for private or public repositories. | [Publish to your hosting](badge-direct-hosting.md). |
| You explicitly want to evaluate ArchLinterNet's Worker/Durable Object/OIDC integration. | Experimental, opt-in `relay`. | [Experimental support boundary](#experimental-support-boundary), then [setup](badge-setup.md). |

These are integration choices. The CLI's built-in transport modes remain
`none`, `github-raw`, and `relay`; consumer-owned hosting is **not** a fourth
mode, a new CLI flag, or a project-operated service. The built-in `github-raw`
adapter rejects private repositories. That does not prohibit independently
verified direct publication from a private repository.

## Generate the payload

Produce `architecture-health.json` through the
[complete governance workflow](single-tool-workflow.md), using the actual
policy, baseline, contexts, and required evidence for your repository. Ordinary
strict validation JSON is not a Health document. Then render the badge:

```bash
dotnet arch-linter-net badge architecture-health \
  --input artifacts/architecture-health.json \
  --output artifacts/architecture-health-badge.json
```

The badge command reads canonical Health and policy-inventory evidence without
rerunning analysis. Its headline reports Gate, Health, explicit ignore debt,
and effective policy controls. Counts are not a score or coverage percentage.
`UNASSESSABLE` and `?` are unknown results, not zero debt.

The default headline projection contains only `schemaVersion`, `label`,
`message`, and `color`. Publish the complete generated payload, not a
handwritten substitute based on whether the workflow was green.

## Choose the producing workflow

For a normal PR-gated repository, the path is:

```text
required PR analysis -> canonical Health + badge + private provenance
                    -> merge -> verify analyzed tree against accepted main
                             -> publish the accepted projection
```

A squash commit has a different commit SHA. Bind the actual analyzed commit
and Git tree separately from the PR head and merged commit; require exact
accepted-tree agreement. Do not assume GitHub's PR checkout is the head commit:
it can be a synthetic merge candidate.

A repository may instead publish from an existing nightly/product lifecycle:

```text
select exact main revision -> existing build/analysis -> canonical projection
                            -> isolated publisher -> public readback
```

That badge describes the selected nightly revision. A skipped nightly is not a
new evaluation and must not refresh old evidence. Keep required PR checks even
when the public badge follows nightly. In either design, publication does not
start another build or analysis merely to update the image.

## Public snapshots

For a public repository, an existing trusted static publisher can place the
accepted JSON on an automation-owned branch or other public static hosting.
Use a stable URL. Do not commit generated badge updates to your source branch
on every PR. The built-in `github-raw` adapter is another option with its own
reviewed registry/provenance prerequisites; it is not a generic workflow you
can adopt just by copying an upstream configuration ID.

Shields can render the generated endpoint JSON. In the following README
example, replace `OWNER`, `REPOSITORY`, `BADGE_BRANCH`, and the file path with
your **public** snapshot location:

```markdown
[![Architecture Health](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FOWNER%2FREPOSITORY%2FBADGE_BRANCH%2Farchitecture-health-badge.json)](https://raw.githubusercontent.com/OWNER/REPOSITORY/BADGE_BRANCH/architecture-health-badge.json)
```

Never add an access token to a raw/Shields/README URL. A static snapshot has no
request-time expiry: stopped publication can leave it visible indefinitely.
Label it as a snapshot and provide publication evidence where appropriate.
Use an expiry-enforcing origin when bounded freshness is required.

## Private repositories and public output

For consumer-owned hosting, use the [direct publication guide](badge-direct-hosting.md).
A pre-provisioned Worker serving generated JSON/SVG does not require
ArchLinterNet Relay, a Durable Object registry, OIDC bootstrap, or Relay renewal.
You own the publisher, provider credentials, availability, and expiry behavior.
There is no installed one-command direct publisher.

Approve the public fields and destination before enabling publication. Keep
full Health, reports, findings, source paths, repository/PR/run identities,
SHAs, and provenance manifests private. A minimal public headline is still a
disclosure decision. A neutral hostname or opaque alias does not make its
association with the repository undiscoverable.

## Experimental support boundary

**Private Relay is experimental / opt-in. Full hosted/lifecycle acceptance is
still pending.** Included code and release assets are not proof of turnkey or
adoption-stable operation. Core governance, private reports, public snapshots,
and independently maintained direct hosting do not require Relay.

Relay uses an adopter-owned Cloudflare Worker and SQLite Durable Objects with
pinned publisher trust and GitHub OIDC. Evaluate it only with a matching
[verified distribution](../reference/badge-distribution.md). Start with
[setup and doctor](badge-setup.md); use the
[lifecycle runbook](badge-lifecycle-operations.md) for renewal, rotation,
revocation, migration, and removal. No project-hosted signup endpoint or free
hosting/SLA is promised. Experimental status does not excuse security,
privacy, integrity, or false-success defects.

Relay's `headline-only/v1` is the four-field JSON projection.
`headline-plus-freshness/v1` additionally discloses `verified_at` and
`valid_until` and supports the direct stamped SVG. That extra disclosure needs
explicit approval. The Relay lease/renewal limits and lifecycle guarantees
belong to **Relay**, not to every consumer-owned Worker or raw snapshot.

## Verify the result

First read the origin JSON and, when used, its SVG without authentication.
Compare them with the accepted projection and inspect the private publication
record for the exact source, producer/run attempt, digest, and validity bound.
An upload API success alone is insufficient. A new verified publication can
have the same headline; text equality does not establish freshness or staleness.

Then compare the origin with Shields and the image in GitHub's README proxy
(Camo). A cached copy is not proof that the origin remains ready. Expiry and
revocation at the origin cannot recall browser, proxy, or offline copies, and
there is no fixed cache-refresh delay to promise. Do not change canonical
counts or create cache-busting source commits to make a refresh visible.

A valid failing Health result, unassessable architecture evidence, and an
unavailable publication are different states. Keep the architecture decision
in the canonical report; make transport failures visibly unavailable rather
than presenting the previous healthy result as current.

## Operate, migrate, and remove

For direct hosting, follow [publisher acceptance and operations](badge-direct-hosting.md#acceptance-and-operations).
For Relay, use its [authenticated lifecycle procedures](badge-lifecycle-operations.md),
including consent withdrawal and tombstones. Do not apply Relay commands to an
unrelated Worker. Removing a README image or stopping a schedule alone does
not revoke an already published origin or its cached copies.

Existing public snapshots and the narrower `badge architecture-policy` command
remain valid choices. Adopting another transport is an explicit decision, not a
mandatory upgrade to keep the architecture gate working.
