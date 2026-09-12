# ADR: Architecture Health Badge Relay contract

Status: accepted design baseline for issue [#826](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/826)

Date: 2026-09-09\
Lifecycle: v0.8.x completeness-stabilization; implementation children consume this reviewed contract. This ADR does not authorize publication or deployment.

This is an internal decision record. It defines the product and security
contract for a bounded-freshness badge transport. It does not implement or
deploy a service.

## Decision

The supported private-repository path is an adopter-owned Badge Relay: a
Cloudflare Worker with one SQLite-backed Durable Object per alias. The adopter
deploys and pays for it in its own Cloudflare account. The Durable Object is the
per-alias serial authority for generation, lease, revocation, challenges, and
idempotency. Cloudflare KV, a project-operated SaaS, and a provider catalogue
are not part of this decision.

The Relay transports an already-produced canonical payload. It does not run
ArchLinterNet, interpret policy, calculate Gate or Health, count rules, or
turn a private receipt into public data.

### The ten workshop decisions

| Question | Decision |
| --- | --- |
| Mode and hosting | Support `none`, public `github-raw`, adopter-owned `relay`, and an explicit `custom` extension point. The reference `relay` is the Cloudflare Worker + SQLite Durable Object bundle above; there is no project SaaS. |
| Semantic authority | ArchLinterNet owns canonical Gate, Health, explicit-ignore inventory, effective-rule inventory, validity horizon, and the `architecture-health/v1` evidence. The Relay owns only transport authorization, publication validity, and lifecycle ordering. |
| Public representation | Use closed `headline-only/v1` and `headline-plus-freshness/v1` profiles. The first has the four headline values and fixed rendering fields; the second adds only `verified_at` and `valid_until`. |
| Freshness | A ready lease is at most 60 minutes and is `min(verified_at + 60m, canonical semantic horizon)`. Renewal is optional and may be attempted no more often than every 30 minutes: at most 48 scheduled jobs per day. |
| Credentials | GitHub OIDC is the only runtime publisher credential. No GitHub PAT, GitHub App private key, secret raw URL, or public token is supported. Cloudflare deployment credentials are adopter setup concerns, never badge credentials. |
| Identity and privacy | Authorization uses immutable repository and owner IDs plus exact workflow pins. Full provenance, identity, SHAs, PR/run data, JWTs, request headers, detailed errors, and SVG metadata remain private. |
| Ordering and recovery | One-time challenges, atomic generation/epoch CAS, tombstones, and a recovery barrier make revocation and invalidation win over delayed writers. Restore never revives a ready row; a current PR-authoritative proof is required. |
| Cache and rendering | The origin checks expiry for GET, HEAD, ETag, and 304. The default SVG includes an inseparable readable absolute expiry. Cached browser, Camo, and offline copies cannot be universally recalled. |
| Operations and failure | GitHub schedules are best effort; missed jobs expire at origin. The adopter owns GitHub Actions usage, Cloudflare quotas, plan limits, and billing. Storage or provider failure fails closed rather than serving old ready bytes. |
| Compatibility and release | Versioned profiles, configuration, and bundle are accepted only when explicitly known. Distribution follows the existing release authority; this ADR does not publish packages, deploy Pages, or change the existing public snapshot. |

## Invariants and authority boundaries

The following remain unchanged and independent of publication state:

- canonical Gate, Health, ignores, and rules are evaluated by the existing
  ArchLinterNet CLI/Core pipeline;
- `architecture-health/v1`, its inventory, exit semantics, and canonical
  projector remain the source of those facts;
- `ready`, `expired`, `unavailable`, and `revoked` mean only whether a current
  confirmation is available. They never represent a recomputed Gate or Health;
- CI may produce and transport evidence, but the Relay is not a second
  evaluator.

| Fact or object | Canonical authority | Relay responsibility |
| --- | --- | --- |
| Gate, Health, ignores, effective rules | ArchLinterNet evaluator and canonical Health artifact | Store no interpretation; reject malformed or non-canonical bytes |
| Semantic validity horizon | Canonical producer evidence | Enforce the horizon; never invent an unknown horizon |
| Ready public bytes | Canonical CLI projector and exact digest | Match closed profile and store/serve exact bytes; never rewrite |
| Promotion manifest and publication receipt | Private publisher evidence | Validate the private binding and retain only the minimum private envelope |
| Current time and lease status | Relay trusted UTC clock | Check on every origin read and suppress expired ready |
| Generation, revocation epoch, challenge use, tombstone | SQLite Durable Object transaction | Serialize and compare-and-set every write |

## Publication modes

| Mode | Meaning | Private-repository behavior | Credential/hosting rule |
| --- | --- | --- | --- |
| `none` | No external badge transport | Default; private artifacts and checks remain available, with no Relay contact | No service and no credential |
| `github-raw` | Existing public static `github-raw` snapshot | Reject with an actionable visibility diagnostic; do not add a secret raw URL | Public repository only; GitHub is the host |
| `relay` | Versioned bounded-freshness Relay | Supported private mode when the adopter deploys the reference bundle | Adopter-owned Cloudflare Worker + SQLite Durable Object; adopter owns account, domain, quotas, and billing |
| `custom` | Adopter implementation of this same versioned contract | Not a turnkey provider or arbitrary URL; registration must name an approved contract/bundle | Explicitly configured and reviewed by the adopter; no project-operated endpoint |

Native private GitHub image/branch alternatives are not supported by this
contract: they have not demonstrated authenticated multi-viewer behavior and
bounded expiry. A renderer cannot receive a raw private token through a URL.

## Disclosure profiles

Profiles accept exact canonical UTF-8 bytes with fixed key order, no duplicate
keys, no extra keys, deterministic escaping/whitespace, a closed state/message/
color dictionary, and bounded size (16 KiB for a public payload). For
`headline-only/v1`, canonical bytes are the existing producer's default
`System.Text.Json` wire representation, including `\\u00B7` escapes for the
headline separator; literal UTF-8 separator bytes are a different byte string.
`headline-plus-freshness/v1` uses that same encoding and order, appending only
`verified_at` and `valid_until`; each profile has a schema-constrained golden
byte string and digest. Invalid bytes are rejected; they are never sanitized or
rewritten after digest verification. Color is Health-owned and Gate-independent: `healthy` is
`brightgreen`, `debt` is `yellow`, `degrading` is `orange`, `failing` is `red`,
and `unassessable` is `lightgrey`.

| Surface | `headline-only/v1` | `headline-plus-freshness/v1` | Visibility decision |
| --- | --- | --- | --- |
| Headline facts | Gate, Health, explicit-ignore count, effective-rule count | Same four | Public, and only from canonical bytes |
| Fixed rendering fields | Schema/version, fixed architecture label, closed message/color representation | Same | Public; no arbitrary text |
| Freshness | Not present | Canonical UTC `verified_at` and `valid_until` only | Public only in the explicitly selected second profile |
| Alias/hostname | Opaque alias may occur in the route; no repository name or source identity in payload. Relay hostname is transport metadata | Same | Route/transport visibility is not a provenance disclosure |
| Full provenance | `architecture-health/v1`, promotion manifest, publication receipt, source identity, repository/owner IDs, commit/tree SHA, PR/run IDs | Same | Private; never payload, SVG, header, error, or telemetry input |
| Authentication and diagnostics | JWT claims/token, JTI, challenge, request headers, detailed errors, registry pins | Same | Private; logs contain only redacted reason codes |
| SVG metadata and markup | Fixed local SVG only; readable expiry is handled by the rendering rule below | Same | No embedded provenance, links, scripts, HTML, or external resources |

The four headline values are disclosed as a closed projection, not a free-form
message. The public response cannot carry source URLs, header values, arbitrary
text, SHAs, PR/run information, JWT material, errors, or hidden SVG metadata.
The Relay does not create a second public receipt or expose the existing
`promotion/v1` or `publication/v2` provenance models.

## Freshness, lease, and cost

For a verified publication at trusted Relay time `verified_at`:

```text
valid_until = min(verified_at + 60 minutes, canonical semantic horizon)
```

The lease is never longer than 60 minutes. A missing, unknown, mismatched, or
already-passed semantic horizon forbids ready publication and renewal. The
Relay does not calculate waiver, evaluation-date, or external-evidence
semantics. An unchanged tree, a read, a retry, or a repeated identical payload
does not extend a lease; a renewal needs a fresh PR-authoritative proof whose
horizon is still current.

Renewal is optional and is permitted at most once per 30 minutes. A schedule
with one attempt per cadence therefore has a maximum of 48 renewal jobs per
day per repository. This is a cap, not a promise that GitHub cron runs. Private
GitHub Actions usage is charged according to the repository owner’s plan and
runner; public repositories and self-hosted runners have different billing
treatment. Cloudflare Worker/Durable Object requests are likewise subject to
the adopter’s plan and quotas. The adopter may lower the cadence or disable
renewal, but cannot enlarge the lease in this contract.

When no publisher or schedule runs, origin expiry still occurs. After
`valid_until`, origin reads return the selected expired/unavailable rendering,
never the prior ready payload. Recovery does not extend a passed semantic
horizon and never analyzes `main` to manufacture current evidence.

## OIDC trust contract

The reference registry is pinned to this repository and workflow. An adopter
entry must replace the IDs and pins with its own exact values; names and
display strings are never substitutes for IDs.

| JWT input | Required value/check |
| --- | --- |
| `iss` | Exact `https://token.actions.githubusercontent.com`; no alternate issuer |
| `aud` | Exact configured audience for the registry entry; each adopter configures one exact owner audience |
| Protected JOSE header `alg` | `RS256` only; reject `none`, other algorithms, and algorithm confusion before trusting claims |
| Protected JOSE header `kid` | Select only a key identified by `kid` from the fixed GitHub issuer JWKS endpoint; an unknown key can trigger one bounded refresh of that fixed endpoint and is then rejected. Configuration stores neither an allowed key-ID set nor current/next key rotation state. No JWT header or claim can select a network URL. Provider key rotation is controlled through the fixed issuer trust chain, not a permanent snapshot of one key |
| Time claims | Relay UTC clock; require `nbf`, `iat`, and `exp`, reject missing claims, `nbf` or `iat` more than 5 minutes in the future, `exp` more than 5 minutes in the past, or `exp - iat > 10 minutes`. The ±5-minute skew is only token validation allowance and does not extend a lease |
| `repository_id` | Exact immutable integer in the registry and token context; synthetic vectors use a non-production ID. Display `repository` is diagnostic only |
| `repository_owner_id` | Exact immutable integer in the registry and token context; synthetic vectors use a non-production ID. Display owner/login is diagnostic only |
| Event and ref | Exact `push` event and `refs/heads/main` for this ArchLinterNet publisher; no wildcard branches or `workflow_run` substitution |
| `job_workflow_ref` | Exact configured reusable-workflow path and ref; each registry entry pins its own approved path |
| `job_workflow_sha` | Exact configured 40-hex workflow-file commit SHA; changing the workflow requires a reviewed registry pin update |
| `sub` | Exact configured compatibility form; it is secondary to immutable IDs and pins. Legacy, immutable, and custom subject forms are separate explicit configurations, never loose wildcards |

The token is size-bounded (16 KiB), verified against the pinned issuer/JWKS,
and discarded after validation. OIDC proves publisher identity only. The
publisher must separately prove the exact canonical bytes, promotion manifest,
PR-authoritative context, base/head/tree relationship, and selected profile.
A successful job with a familiar name, a larger run ID, or a matching SHA is
not by itself authorization or ordering.

## Lifecycle state machine

The state set is `unregistered`, `unavailable`, `ready`, `expired`, `revoked`,
and `needs-recovery`; removal additionally leaves a `tombstoned` alias. Every
alias has monotonic `generation` and `revocation_epoch` integers. The SQLite
transaction updates payload bytes/digest, lease, generation, epoch,
challenge-use, and idempotency together.

| Operation | Preconditions and ordering | Atomic result |
| --- | --- | --- |
| `register` | Authenticated adopter; exact immutable IDs, bundle/profile, destination, and consent; alias is absent, never silently reused | Create alias as `unavailable`, generation 1, epoch 1, and registry pin. |
| `prepare` | Valid OIDC and registry pin; canonical digest/profile; current generation/epoch; no revoke; one idempotency key | Create one-use challenge bound to digest, state, generation, epoch, JTI hash, and **deadline = issue time + 5 minutes**. The deadline is fixed before the context recheck; no public state changes. |
| `publish` | Recheck GitHub PR-authoritative context after challenge issuance and before commit; challenge unexpired and unused; exact digest/manifest/profile | One transaction CASes generation and epoch, consumes the challenge, stores exact bytes and private envelope, and enters `ready` with the formula above. |
| `renew` | Same two-phase flow as `publish`; fresh current proof and unpassed semantic horizon; same tree alone, retry, or read is insufficient | CAS to a new generation and lease, or reject without mutation. A failed or passed horizon cannot be revived by transport. |
| `invalidate` | Publisher or owner action, or failed current evidence; expected generation/epoch | Atomically remove ready eligibility, advance generation/epoch as applicable, and enter `unavailable`. Delayed ready delivery then fails CAS. |
| `read` | Public route lookup | Trusted-clock check happens before GET, HEAD, ETag validation, or 304. Only `ready` with `now < valid_until` is served; otherwise return expired/unavailable without recomputation or lease extension. |
| `revoke` | Exact owner/registry authorization for delete, consent/visibility loss, transfer, uninstall, or pin rotation | Atomically remove ready data, advance revocation epoch and generation, enter `revoked`, and tombstone the alias. All old challenges and delayed writes fail. |
| `recover` | Restore detected or `needs-recovery`; current registry/pin and a new PR-authoritative proof; no main reanalysis | Keep public state unavailable until a post-restore, greater-generation CAS succeeds. A backup never makes an old ready row current. |

`publish` and `renew` use compare-and-set on both generation and epoch. A
challenge cannot be replayed with another digest, state, generation, or JTI.
The one-use deadline is not moved by delivery retry, queue delay, or a `429`.
Generation/epoch and the tombstone defeat these cases:

- ABA or force-push back to a previously seen commit/tree: equal bytes do not
  establish recency; a fresh challenge and current proof are required;
- delayed `ready` after `invalidate` or `revoke`: the old epoch/generation
  cannot satisfy the transaction and changes nothing;
- a backup restored before revocation: the Worker enters `needs-recovery`, an
  external registry/recovery barrier remains authoritative, and only a fresh
  proof under the current pin can create a later generation;
- alias transfer or deletion: the tombstone prevents silent reassignment to a
  different tenant. Any future reuse needs an explicit registration procedure
  with a new identity and epoch.

## Origin, cache, SVG, and response contract

### Fixed public routes and layout

After the outer Worker has looked up a registered alias (and before it calls
`idFromName`), the public route family is fixed:

| Route | Allowed methods | Representation selection |
| --- | --- | --- |
| `/badge-relay/v1/{alias}` | `GET`, `HEAD` | Profile-selected default: strict local SVG for `headline-plus-freshness/v1`; exact JSON snapshot compatibility for `headline-only/v1` |
| `/badge-relay/v1/{alias}.json` | `GET`, `HEAD` | Exact canonical JSON for the registered profile; no implicit profile upgrade or added freshness fields |
| `/badge-relay/v1/{alias}.svg` | `GET`, `HEAD` | Fixed local SVG only for `headline-plus-freshness/v1`; a headline-only registration cannot claim a bounded-current SVG |
| `/badge-relay/v1/{alias}/json` or `/badge-relay/v1/{alias}/svg` | `GET`, `HEAD` | Segment aliases for the same explicit JSON/SVG selections above; they do not add representations |

The alias is the only route-derived public identifier and is opaque. Unknown,
malformed, unsupported, or tombstoned aliases and unsupported methods return a
generic `404` without allocating a Durable Object. Public `GET` and `HEAD`
share one read seam: it validates the persisted closed payload and compares
the trusted UTC clock with `valid_until` before choosing a body, creating an
ETag, or evaluating `If-None-Match`. `HEAD` has the same status and headers as
`GET` and omits the body. A matching conditional request can return `304` only
while the representation is still ready (`now < valid_until`).

Ready JSON is the stored canonical UTF-8 byte string verbatim. The fixed
unavailable projection is the exact `UNASSESSABLE · ? ignores · ? rules`
representation and never includes a private reason. The strict SVG is a
small local template: it displays the canonical label, message, Health-owned
color, and absolute UTC `verified at` / `valid until` text together as one
readable badge. Only already validated closed values enter text or attributes;
XML escaping is defense in depth. No alias, provenance, URL, SHA, PR/run,
JWT, arbitrary metadata, `script`, event handler, `foreignObject`, link, or
external asset can enter the markup. The headline-only profile therefore
remains a JSON/Shields snapshot view and cannot be presented as bounded-current
through a default SVG route.

Ready responses use a bounded `Cache-Control: public, max-age=N, must-revalidate`, where `N` is no greater than the remaining lease. Expired,
unavailable, revoked, and storage-uncertain responses use `Cache-Control: no-store` and do not carry a ready representation. ETag identifies the complete
public representation: profile, exact payload bytes, generation, state, and
`valid_until`; it is never the headline payload digest alone. A conditional
request and `HEAD` perform the same trusted-clock expiry check. A 304 never
extends validity. Browser, GitHub Camo, proxy, and offline copies may remain
visible beyond expiry and cannot be universally recalled; documentation must
describe them as cached copies, not current origin truth.

The default SVG uses `headline-plus-freshness/v1` and is a fixed local rendering
with a readable absolute UTC `valid_until` timestamp inseparable from the badge
state. `headline-only/v1` is reserved for snapshot compatibility output. Neither
representation contains HTML, script, link, redirect, external resource, hidden
metadata, or payload-derived attribute. Bare JSON and Shields-compatible output
remain snapshot compatibility views, not a strict freshness guarantee.

| Condition | Response | Disclosure/side effect |
| --- | --- | --- |
| Missing/invalid/expired publisher JWT | `401` | Generic reason only; no registry or tenant detail; no mutation |
| Authenticated identity not registered/consented/pinned | `403` | Generic reason only; no existence oracle beyond the authenticated owner |
| Unknown or tombstoned public alias | `404` | Indistinguishable lookup result; no history |
| Stale challenge, replay, generation/epoch mismatch, force-push/context mismatch | `409` | No mutation; retry cannot move its deadline |
| Oversized JWT, payload, manifest, or unsupported closed-profile field | `413` | Reject before write; no sanitization or truncation |
| Rate/quota limit | `429` | Bounded retry information; challenge deadline remains unchanged |
| Storage/provider outage or uncertain clock/state | `503` | Fail closed; never serve expired ready or private diagnostics |

## Identity, retention, and recovery

Registration binds an opaque alias to immutable `repository_id`,
`repository_owner_id`, exact owner/repository display values for private
diagnostics, selected profile, destination, event/ref, workflow ref/SHA, and
consent. Display names, branch names, run IDs, and commit SHAs are not
authorization. The Relay stores only a hash of JTI/idempotency values needed
for replay prevention; raw JWTs and authentication headers are never stored or
logged.

Public retention is one current representation only: no endpoint for history,
old payloads, receipts, or provenance exists. The private Durable Object may
retain the current envelope, registry, active challenge, and monotonic state;
redacted operational reason codes are retained for at most 30 days. Spent
challenge/idempotency records are retained for at least 48 hours. A tombstone
and its revocation barrier are retained for at least 90 days and are not
silently purged as part of ordinary payload deletion. Deletion removes current
payload bytes and private provenance; the tombstone survives the retention
period before any explicit, reviewed purge.

After backup restore, the Worker must enter `needs-recovery` even if a restored
row says `ready`. A current registry pin, current consent, and current
PR-authoritative proof must pass a new challenge and commit a generation above
the restored value. Recovery never analyzes `main`, extends a semantic
horizon, or publishes a prior receipt. If the external recovery barrier or
current registry cannot be established, reads stay unavailable.

## Compatibility and release graph

| Component | Contract | Compatibility rule |
| --- | --- | --- |
| Existing CLI/Core evaluator | `architecture-health/v1` and canonical Health/inventory | Unchanged; publication state cannot alter results or exit semantics |
| CLI projection | `headline-only/v1`, `headline-plus-freshness/v1` | Exact profile and canonical bytes; no implicit profile upgrade |
| Publisher evidence | `architecture-health-badge-promotion/v1`, private `architecture-health-badge-publication/v2` | Private binding only; no parallel public provenance model |
| Relay configuration | `relay-config/v1` | Unknown schema, mode, profile, destination, or bundle is rejected before registration |
| Reference deployment | `badge-relay/v1` | Worker/SQLite Durable Object bundle and protocol version must be explicitly pinned |
| Existing public path | `github-raw` snapshot | Remains available for public repositories; no migration or ordinary Pages deploy is implied |

The ownership and dependency graph is:

```text
#825 product story
  -> #826 this ADR, profiles, trust and lifecycle contract
       -> (#827 canonical projection) || (#828 OIDC registry/storage)
            -> promotion/read/bundle/lifecycle implementation children
                 -> #835 distribution integration
                      -> #806 released-artifact verification and v0.8.x closure
```

`#827` and `#828` may implement only after this ADR is reviewed and merged.
The distributable bundle, CLI, publisher, configuration, and profile versions
must be released through the existing ArchLinterNet release authority. This
design does not publish a package, create a tag, deploy documentation, or make
the capability shipped.

## Non-goals and risks

This ADR does not add a Relay runtime, cloud account settings, a user-written
publisher/server, a GitHub PAT/App integration, arbitrary provider adapters,
secret tokenized URLs, a new Gate/Health evaluator, universal cache recall,
instant revocation of already cached images, free infinite hosting, HTML or
script rendering, or a public private-repository receipt.

The principal residual operational risks are best-effort scheduling, owner
billing and provider quotas, Cloudflare/JWKS outages, and stale copies held by
third-party caches. Each has an explicit fail-closed origin rule and an owner;
none is hidden behind a freshness claim.

## Primary sources

- [GitHub OIDC claims reference](https://docs.github.com/en/actions/reference/security/oidc)
- [GitHub OIDC security hardening](https://docs.github.com/en/actions/security-for-github-actions/security-hardening-your-deployments/about-security-hardening-with-openid-connect)
- [GitHub OIDC reusable-workflow trust](https://docs.github.com/en/actions/how-tos/secure-your-work/security-harden-deployments/oidc-with-reusable-workflows)
- [GitHub repository API (immutable repository IDs)](https://docs.github.com/en/rest/repos/repos#get-a-repository)
- [Pinned GitHub OIDC JWKS](https://token.actions.githubusercontent.com/.well-known/jwks)
- [Cloudflare Durable Objects](https://developers.cloudflare.com/durable-objects/concepts/what-are-durable-objects/)
- [Cloudflare Durable Object SQLite storage](https://developers.cloudflare.com/durable-objects/api/storage-api/#sqlite-api)
- [Shields endpoint badge contract](https://shields.io/badges/endpoint-badge)
- [HTTP caching, RFC 9111](https://www.rfc-editor.org/rfc/rfc9111)
- [Repository architecture-health reference](../reference/architecture-health.md)
