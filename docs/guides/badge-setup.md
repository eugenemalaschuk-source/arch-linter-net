# Turnkey Architecture Health badge setup

> Prepublication candidate guidance: use only an explicitly verified candidate.
> This page does not establish stable-release availability. Start with
> [badge adoption](badge-adoption.md) for transport, disclosure, and evidence
> prerequisites.

The candidate contract requires the packed `ArchLinterNet.Cli` tool to include
the setup contract, Relay bundle, configuration schema, and workflow templates.
Setup is intentionally a plan-first command: it shows the disclosure and cost
decision before writing a consumer repository, and it never runs the
architecture evaluator a second time.

## Choose a transport

Run setup from the consumer repository (or pass its explicit repository
identity) and choose one supported mode:

| Mode | Use | Hosting requirement |
| --- | --- | --- |
| `none` | Private checks and artifacts without public disclosure | None; no external call |
| `github-raw` | Existing public static snapshot compatibility | Public repository only |
| `relay` | Bounded-freshness private/public badge | Your Cloudflare Worker and SQLite Durable Object account |

Private repositories default to `none`. Selecting `github-raw` for a private
repository is rejected. An opaque Relay alias does not hide the Relay hostname
or imply that the source identity is undiscoverable; setup shows the approved
destination before registration. Custom URLs, PATs, GitHub App keys, and
unscoped deployment credentials are not turnkey inputs.

## Preview and apply

The exact options and schema identifiers are owned by the installed tool. Start
with:

```text
arch-linter-net badge architecture-health setup --help
arch-linter-net badge architecture-health doctor --help
```

Use setup's dry-run/preview mode first. It must be read-only and should be
reviewed as a normal change. A Relay setup selects either
`headline-only/v1` (JSON/Shields snapshot compatibility) or
`headline-plus-freshness/v1` (the default stamped SVG representation). If
renewal is enabled, the preview reports the selected cadence, jobs per day and
month, private GitHub billed-minute implications, and hosting quotas. The
contract caps a lease at 60 minutes and renewal at no more than once per 30
minutes (48 attempts per day); these are limits, not execution guarantees.
When renewal is disabled, setup emits no scheduled renewal workflow and does
not allow `schedule` in the Relay registry entry. For enabled cadences that do
not align to an hour, the generated workflow uses multiple explicit POSIX cron
entries when necessary; their UTC slots match the previewed jobs-per-day bound
instead of rounding to a more frequent hourly schedule. The preview uses
`floor(1440 / cadence_minutes)` slots; for a non-divisible cadence the final
slot-to-next-day gap is longer so the cyclic interval never falls below the
configured cadence.

Relay setup is fail-closed. `--provider-plan` is optional cost metadata only;
when supplied, it cannot prove a required check, Rules API, OIDC, provider
quota, or account capability. A null plan label is valid when live inspection
proves the required capabilities.
Before a non-dry-run Relay write, the bounded live inspector must run in the
pinned reusable publisher context. This is deliberate: a local shell cannot
mint the publisher-bound OIDC claim. Use the trusted reusable workflow's
`operation: bootstrap` for the first write (it checks out the consumer's
configured base ref, obtains the short-lived OIDC token, runs the same setup
command, and commits only the generated managed files). The caller must first
configure the exact required check/ruleset and review the disclosure inputs.
The bootstrap workflow is pinned by the same immutable publisher SHA used for
normal publication; it does not accept caller-authored capability evidence.

For an already bootstrapped destination, the equivalent local preview remains
useful, but it cannot replace that trusted first write. With adopter-specific
values, the command shape is:

```text
arch-linter-net badge architecture-health setup \
  --repository owner/name --visibility private --mode relay \
  --repository-id 123456 --repository-owner-id 654321 \
  --account 0123456789abcdef0123456789abcdef --alias a7f4k2m9 \
  --endpoint https://relay.example --audience architecture-health-badge-relay/a7f4k2m9 \
  --provider-plan pro \
  --approve-disclosure --output . --dry-run
```

Missing, stale, contradictory, or unproven live capability evidence leaves the
plan unavailable and produces no managed files. A caller-authored JSON file
passed through `--capability-evidence` is intentionally rejected in v1: shape
and freshness do not authenticate its origin, so it cannot declare required
checks, Rules API, OIDC, Relay capability, or quota. A future signed evidence
protocol must define its issuer and key rotation before file-based evidence is
re-enabled.

Applying the reviewed plan generates, without custom server code:

- versioned configuration and a SHA-256-bound manifest;
- the pinned Worker/Durable Object Relay source, Wrangler configuration, and
  SQLite migration declaration;
- a producer workflow for the selected project/base ref/check, a trusted
  publisher, and optional metadata-only renewal workflow;
- a managed README badge block and final endpoint URLs.

`producer.workflow_sha` is calculated from the exact generated consumer
workflow's Git blob bytes. It is intentionally different from the SHA pin of
the trusted reusable publisher. Use `--base-ref`, `--policy`, `--solution`,
`--producer-workflow`, `--check-name`, and `--audience` when the consumer does
not use the defaults. The generated Relay registry and `wrangler.jsonc` bind
the actual repository/owner IDs, alias, audience, ref, and workflow pins; no
fixture identity is copied into an adopter bundle.

The generated blocks are reviewable. Existing workflow names, branch
protection/rulesets, secrets, and README content outside those blocks are
preserved. Setup writes atomically; repeated setup reuses the approved alias
and deployment identity. A conflict or partial failure leaves the destination
unavailable and can be retried deterministically.

`none` emits no public URL and does not require a hosting account or credential.
Required PR reports, checks, and private artifacts remain available. Setup never
seeds a healthy value before the first qualifying merged-PR evidence exists.

## Doctor and recovery

Doctor produces stable machine-readable codes plus a concise fix. It checks
CLI/action/Relay/schema compatibility, repository and owner identity, pins,
profile, required-check/rules API visibility, OIDC audience/ref/workflow
claims, destination reachability, artifact and evidence availability, expiry,
revocation, storage/quota state, and origin-versus-Shields/Camo cache delay.

When no observation file is supplied, doctor performs a bounded read-only
inspection of the local generated producer pin, configured capabilities, and
the selected raw/Relay endpoint. A newly generated `github-raw` or Relay
configuration reports `unavailable` until the endpoint serves real first
evidence. In environments where the remote-only checks are performed by an
approved inspector, pass its fresh identity-bound observation instead:

```text
arch-linter-net badge architecture-health doctor \
  --input badge-relay-config.json \
  --observation ./badge-relay-doctor-observation.json
```

The observation protocol covers identity, pins, OIDC, required checks/rules,
artifact/evidence, expiry, revocation, quota, and cache freshness. A plan or
provider-plan flag cannot manufacture a healthy doctor result.

Public output is deliberately redacted: it contains no token, repository
name/URL, commit or tree SHA, PR/run identifier, private receipt, JWT, or raw
provider response. Detailed operator context is a separate private result.
Unsupported plans, permissions, schemas, or claims are unavailable results;
verification is never silently weakened.

After all publishers and renewal jobs stop, the Relay origin expires the stored
lease on read. A stale cached browser, Shields, GitHub Camo, or offline image
may remain visible and cannot be universally recalled; it is not proof that the
origin is still ready. Recovery requires a new approved PR-authoritative proof.
It never re-analyzes `main`, revives a revoked alias, or extends an expired
semantic horizon.

The first real deployment and packed candidate proof are maintained by the
release/acceptance workflow. This guide does not publish packages, deploy a
project-owned Relay, or authorize a public release.
