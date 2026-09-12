# Turnkey Architecture Health badge setup

The packed `ArchLinterNet.Cli` tool includes the setup contract, Relay bundle,
configuration schema, and workflow templates. Setup is intentionally a plan-
first command: it shows the disclosure and cost decision before writing a
consumer repository, and it never runs the architecture evaluator a second
time.

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

Applying the reviewed plan generates, without custom server code:

- versioned configuration and a SHA-256-bound manifest;
- the pinned Worker/Durable Object Relay source, Wrangler configuration, and
  SQLite migration declaration;
- producer, trusted publisher, and optional metadata-only renewal workflows;
- a managed README badge block and final endpoint URLs.

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
