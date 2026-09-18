# badge-turnkey-setup Specification

## Purpose
Provides a versioned, reviewable setup and doctor workflow that lets consumers configure the supported Architecture Health badge transports, generate the adopter-owned Relay assets, and recover from unsupported or incomplete prerequisites without exposing private source metadata.

## Requirements

### Requirement: Setup exposes explicit supported modes and bounded choices
The setup command SHALL inspect the target repository visibility and capabilities before any write and SHALL require an explicit mode from `none`, public `github-raw`, or adopter-owned `relay`. It SHALL require an explicit disclosure profile, target account/owner where applicable, opaque alias for Relay, and renewal cadence/cost choice before registration or file generation. Private repositories SHALL default to `none`; public `github-raw` behavior SHALL remain compatible; private `github-raw` SHALL be rejected with an actionable diagnostic. Custom or arbitrary authenticated transports SHALL not be presented as turnkey options.

#### Scenario: Private repository defaults to no disclosure
- **WHEN** setup inspects a private repository without an explicit transport choice
- **THEN** the preview selects `none`
- **AND** it performs no external hosting call and requests no hosting credential

#### Scenario: Private raw publication is rejected
- **WHEN** a private repository selects `github-raw`
- **THEN** setup returns a machine-readable visibility conflict with a concise migration fix
- **AND** it writes no registry, workflow, README, or destination state

#### Scenario: Relay cost and cadence are explicit
- **WHEN** a user selects `relay` with renewal enabled
- **THEN** setup reports the resulting jobs-per-day/month, GitHub private-minute implications, and provider quota assumptions before registration
- **AND** it never silently selects a fifteen-minute scheduler or increases the lease to hide failed jobs

### Requirement: Setup generates deterministic versioned configuration and deployable assets

The setup command SHALL generate a versioned configuration containing the
selected mode/profile, approved repository and owner identities, workflow/action
pins, destination binding, validity limits, bundle/compatibility identifiers,
and manifest digests. Relay output SHALL include the Worker, SQLite Durable
Object bindings/migrations, and deterministic templates needed for deployment
without user-authored server code. Producer, publisher, optional-renewal
workflows, and README changes SHALL be reviewable diffs and SHALL use the
existing trusted promotion and stamped-SVG contracts.

For contract version v1, `workflow_ref`, `workflow_sha`, and `action_ref` SHALL
be absent or exactly equal to the shipped `BadgeSetupContract.Default*` values.
Input configuration SHALL never select a different write-capable reusable
publisher or action. Rotation SHALL occur through a reviewed bundle/contract
version update. Generated Relay assets SHALL bind immutable repository and owner
IDs, repository display identity, opaque alias, exact audience, disclosure
profile, and the configured reusable-workflow OIDC claim. Setup SHALL never
copy synthetic fixture identity or a fixture `wrangler.jsonc` into an adopter
project.

#### Scenario: Untrusted publisher pins are rejected

- **WHEN** an input configuration names a syntactically valid workflow or
  action outside the shipped v1 defaults
- **THEN** setup returns an invalid-pin diagnostic before any managed write
- **AND** it never renders that reference into a write-capable workflow or
  Relay OIDC registry entry

#### Scenario: Clean setup produces a candidate bundle

- **WHEN** a supported clean packed candidate runs setup for a valid Relay account and profile
- **THEN** it emits a schema-valid configuration, manifest, deployable bundle, pinned workflows, and final endpoint URLs
- **AND** none of the generated files reference the ArchLinterNet source checkout or embed a token

#### Scenario: `none` produces no public destination

- **WHEN** setup selects `none`
- **THEN** it emits the private workflow/report configuration needed for local evidence
- **AND** it emits no public endpoint, hosting account requirement, or external request

#### Scenario: Generated README uses the selected representation

- **WHEN** setup selects `headline-plus-freshness/v1`
- **THEN** the generated default README snippet points to the stamped SVG view
- **AND** an optional bare Shields snapshot is labelled separately and no token appears in Markdown or URLs

#### Scenario: Relay output is adopter-bound

- **WHEN** setup renders a Relay candidate for an adopter repository
- **THEN** `wrangler.jsonc` contains the adopter's IDs, alias, audience, and workflow binding
- **AND** it contains no synthetic fixture owner, repository, alias, IDs, or workflow values

#### Scenario: Producer pin is exact

- **WHEN** setup generates the producer workflow and registry
- **THEN** `producer.workflow_sha` equals the Git blob SHA of the generated producer file byte-for-byte
- **AND** the reusable publisher commit pin is not substituted for that hash

### Requirement: Writes are dry-run safe, idempotent, and recoverable

Dry-run and doctor SHALL be read-only. A real setup SHALL write through
temporary files and an atomic commit boundary, preserve existing manual
settings and protected branch/ruleset/secrets, and record enough manifest state
to retry or roll back. Before any managed read or write, setup SHALL reject an
output root or existing path component that is a symlink, junction, or other
reparse point; lexical containment alone SHALL not authorize filesystem access.

#### Scenario: Linked output subtree is rejected

- **WHEN** `.github`, `relay`, or another existing managed path component is a
  symlink, junction, or reparse point outside the output root
- **THEN** setup fails before reading or writing managed content
- **AND** it does not create files through the linked subtree

#### Scenario: Dry-run does not mutate

- **WHEN** a user runs setup in dry-run mode or runs doctor
- **THEN** no local file, registry, workflow, README, destination, or hosting resource is written
- **AND** the command reports the planned changes and prerequisites

#### Scenario: Repeated setup preserves identity

- **WHEN** setup is rerun against an existing deployment with the same approved configuration
- **THEN** it reuses the existing alias and deployment identity
- **AND** it preserves manual workflow, ruleset, secret, and README edits outside its managed regions

#### Scenario: Partial failure fails closed

- **WHEN** deployment or registration fails after a temporary or remote step has started
- **THEN** setup reports a bounded failure and leaves the destination unavailable or rolls back managed temporary state
- **AND** a retry can resume deterministically without creating a second authorized destination

### Requirement: Doctor provides bounded redacted diagnostics

Doctor SHALL produce machine-readable diagnostics plus concise human remediation
for incompatible CLI/action/Relay/schema versions; identity/profile/pin
mismatch; missing required checks, rules APIs, permissions, or unsupported plans;
invalid OIDC audience/ref/workflow claims; inaccessible endpoints; missing,
expired, or malformed artifacts; absent first evidence; expired validity;
revoked destinations; storage/quota failures; and origin-versus-proxy cache
delay. Public-facing diagnostics SHALL redact credentials, private source
identity, full provenance, raw JWTs, and confidential IDs, while private detail
shall remain clearly separated.

Doctor SHALL evaluate configuration identity, pins, OIDC binding, required
checks/rules, artifact/evidence, validity, revocation, quota, and cache state
from a fresh identity-bound observation or from its bounded read-only live
inspector. A fresh destination with no qualifying first evidence SHALL be
unavailable. A healthy installation with a valid bounded observation SHALL be
reportable as available.

#### Scenario: Doctor performs a bounded live inspection

- **WHEN** doctor runs without an observation file for a generated public raw or Relay configuration
- **THEN** it verifies the local producer blob pin, configured capabilities, and the selected endpoint using bounded read-only requests
- **AND** it reports a valid current artifact as available without fabricating first evidence

#### Scenario: Unsupported prerequisites are actionable

- **WHEN** doctor cannot prove the required check, rules API capability, account plan, bundle version, or OIDC claim
- **THEN** it returns a stable diagnostic code and the exact owner action needed
- **AND** it does not downgrade verification or continue setup

#### Scenario: Missing first evidence is unavailable

- **WHEN** setup has completed but no qualifying merged PR evidence exists
- **THEN** doctor reports the destination as unavailable with a first-evidence fix
- **AND** it does not seed a fabricated healthy result

#### Scenario: Public diagnostics are redacted

- **WHEN** a user requests a public report or a setup failure is rendered for public output
- **THEN** the result contains only approved reason codes and safe remediation text
- **AND** it contains no token, repository URL/name, SHA, PR/run identifier, private receipt, or raw provider response

#### Scenario: Healthy observation is available

- **WHEN** doctor receives a valid current observation for a generated install
- **THEN** it reports available with no fabricated failure diagnostics
- **AND** identity, tokens, raw provider responses, and provenance remain private

### Requirement: Configuration and distribution compatibility is fail closed

Setup SHALL reject unknown schema, disclosure profile, bundle, compatibility
plan, malformed configuration, conflicting identity, or unsupported migration
before registration or public writes. The distributable manifest SHALL bind
every generated component to an explicit version and digest, and packed
consumers SHALL be able to run setup/doctor using those assets without a source
checkout or custom scripts.

Setup SHALL validate both parsed input and generated output against the shipped
schema before writing. The schema and validator SHALL reject out-of-bound
cadence/lease values, non-HTTPS endpoints, invalid SHA pins, non-positive
immutable IDs, and escaping or duplicate managed paths.

#### Scenario: Unknown component is refused

- **WHEN** configuration names an unknown schema, profile, bundle, or compatibility plan
- **THEN** setup returns an unsupported-version diagnostic before any write
- **AND** it does not silently select a fallback component

#### Scenario: Packed install is source-independent

- **WHEN** a consumer invokes the setup/doctor command from the packed product
- **THEN** all required templates, schemas, and compatibility metadata resolve from shipped assets
- **AND** the output contains no project-source path requirement

#### Scenario: Invalid configuration is rejected before writes

- **WHEN** configuration contains an invalid endpoint, pin, ID, bound, or managed path
- **THEN** setup returns a stable configuration diagnostic
- **AND** it writes no workflow, README, registry, Relay, or manifest file

### Requirement: Setup proves capabilities fail-closed

Setup SHALL treat provider-plan declarations as cost metadata only. Required
checks, Rules API access, OIDC availability/claims, provider quota, account
identity, and deployment capability SHALL come from the bounded live inspector
for v1. The live provider inspector SHALL use only fields and successful
capability responses documented by the provider contract; it SHALL not require
an undocumented account plan catalogue field to establish account capability.
An unsigned or caller-authored local capability-evidence JSON file SHALL never
satisfy a prerequisite; `--capability-evidence` SHALL fail closed with a stable
diagnostic until an authenticated evidence protocol is shipped. Missing or
contradictory live evidence SHALL prevent non-dry-run writes.

#### Scenario: Unsigned capability evidence is not proof

- **WHEN** a user supplies a fresh JSON file that claims `required_check`,
  `rules_api`, `oidc`, `relay`, and quota are true
- **THEN** setup and doctor do not treat those claims as capabilities
- **AND** the command reports that authenticated live inspection is required

#### Scenario: Declared plan is insufficient proof

- **WHEN** a user supplies `--provider-plan pro` without capability evidence
- **THEN** Relay setup remains unavailable
- **AND** it does not mark required check, Rules API, OIDC, or quota prerequisites
  as satisfied

#### Scenario: Documented account response proves account identity without a plan slug

- **WHEN** the provider Account Details response contains the documented account
  `id`, name, and type fields but no `plan.slug`
- **THEN** the inspector accepts the account identity and evaluates Worker and
  Durable Object capability responses separately
- **AND** a supported configured provider plan remains cost metadata rather than
  an undocumented API assertion

### Requirement: Optional renewal output follows the enabled state

The generated Relay consumer bundle SHALL include a scheduled renewal workflow
only when renewal is enabled in the approved configuration. When renewal is
disabled, the registry event allowlist and managed-file list SHALL not claim or
contain a scheduled renewal workflow.

#### Scenario: Disabled renewal emits no scheduled workflow

- **WHEN** setup generates a valid Relay bundle with `renewal.enabled` set to
  `false`
- **THEN** the bundle contains no scheduled renewal workflow
- **AND** the registry allowlist contains `push` but not `schedule`
- **AND** the managed-file list contains no renewal workflow path

#### Scenario: Enabled renewal emits the scheduled workflow

- **WHEN** setup generates a valid Relay bundle with `renewal.enabled` set to
  `true`
- **THEN** the bundle contains exactly one scheduled renewal workflow
- **AND** the registry allowlist contains both `push` and `schedule`
- **AND** the managed-file list contains the renewal workflow path

### Requirement: Generated renewal schedule matches the bounded cadence

For every valid renewal cadence in minutes, the generated workflow SHALL
declare UTC cron trigger slots anchored at midnight. Adjacent slots, including
the last slot of one UTC day and the first slot of the next UTC day, SHALL be
separated by at least the configured cadence. The declared slots SHALL equal
the preview's `floor(1440 / cadence_minutes)` jobs per day; multiple POSIX cron
entries MAY be used when one expression cannot represent a non-hour-aligned
interval. The workflow SHALL not substitute a broader hourly or otherwise more
frequent schedule.

#### Scenario: Ninety-minute renewal preserves the previewed bound

- **WHEN** setup generates an enabled renewal workflow for a 90-minute cadence
- **THEN** its schedules represent `00:00, 01:30, 03:00, ... , 22:30` UTC
- **AND** the workflow declares 16 trigger slots per UTC day
- **AND** it does not declare the hourly `0 * * * *` schedule

#### Scenario: Hour-aligned renewal remains compact and exact

- **WHEN** setup generates an enabled renewal workflow for a 120-minute
  cadence
- **THEN** its schedules represent every even UTC hour
- **AND** the workflow declares 12 trigger slots per UTC day
- **AND** it declares no additional minute or hour slots

#### Scenario: Non-divisible cadence remains safe across midnight

- **WHEN** setup generates an enabled renewal workflow for a cadence such as
  31 or 59 minutes
- **THEN** the final slot of a UTC day and the first slot of the next UTC day
  are separated by at least the configured cadence
- **AND** the generated slot count equals the preview's floor-based daily count

### Requirement: Live OIDC capability inspection authenticates the token

The live inspector SHALL validate a compact JWT signature before evaluating
OIDC claims. It SHALL accept only an issuer-bound RS256 token whose `kid`
resolves in the fixed GitHub Actions issuer JWKS and whose signature verifies
over the exact encoded header and payload. Unsupported algorithms, missing
keys, malformed JWKS entries, and dummy or invalid signatures SHALL make OIDC
capability unavailable.

#### Scenario: JWT-shaped payload without a valid signature is rejected

- **WHEN** the OIDC endpoint returns a token with valid-looking claims but a
  literal, missing, or non-verifying signature
- **THEN** the OIDC capability is false
- **AND** Relay setup remains unavailable

### Requirement: Generated producer installation is independent of PR package configuration

The generated producer workflow SHALL install the pinned ArchLinterNet CLI from
an explicit trusted NuGet configuration containing only approved feeds, before
checking out PR-controlled contents, and SHALL pass that configuration through
`--configfile`. A `NuGet.Config` supplied by the PR SHALL not affect evaluator
resolution.

#### Scenario: PR NuGet.Config cannot replace the evaluator

- **WHEN** the checked-out PR contains a `NuGet.Config` with cleared feeds and
  an attacker-controlled source
- **THEN** the producer's CLI installation has already used the fixed trusted
  configuration before checkout
- **AND** the workflow does not invoke `dotnet tool install` without the
  explicit `--configfile`

### Requirement: Provider plan metadata is optional

The Relay setup configuration SHALL permit `provider_plan` to be `null`. When present,
the value SHALL be limited to the shipped closed metadata set, but its presence
or value SHALL not be required to satisfy any capability or deployment
prerequisite.

#### Scenario: Proven capabilities do not require a plan label

- **WHEN** live account, quota, required-check, Rules API, and OIDC inspection
  all succeed and `provider_plan` is `null`
- **THEN** Relay setup remains eligible for a valid plan
- **AND** no provider capability is inferred from a plan label

### Requirement: Generated producer context and artifact transport are executable

The generated producer workflow SHALL derive pull-request number, base ref,
and base commit from supported GitHub pull-request event context. It SHALL
upload the generated badge and semantic evidence from paths accepted by the
artifact action, without using a workspace-only file-matching predicate to
guard a runner-temporary path. Missing required files SHALL fail the producer
or upload step rather than silently skipping evidence publication.

#### Scenario: Producer binds a real pull-request context

- **WHEN** the generated producer runs for a pull request
- **THEN** its manifest contains the event's pull-request number, base ref, base
  SHA, head SHA, and head tree SHA
- **AND** pull-request number, run ID, and run attempt are JSON integers
- **AND** it does not read nonexistent `GITHUB_EVENT_NUMBER` or
  `GITHUB_BASE_SHA` environment variables

#### Scenario: Producer uploads generated temporary artifacts

- **WHEN** the producer creates its badge and health artifacts under the runner
  temporary directory
- **THEN** both upload steps execute when the files exist
- **AND** an absent required artifact produces an explicit failure instead of a
  skipped upload

### Requirement: Github-raw doctor probes the publisher's artifact branch

For `github-raw` configurations, doctor SHALL construct its artifact URL from
the fixed publication branch used by the generated publisher and README, not
from the producer's pull-request base ref. The probe SHALL preserve the
configured repository identity and artifact path.

#### Scenario: Doctor checks the published raw branch

- **WHEN** a generated github-raw configuration targets a non-default producer
  base ref
- **THEN** doctor probes
  `/<owner>/<repository>/architecture-health-badge/architecture-health.json`
- **AND** it does not probe the producer base branch as the artifact branch

### Requirement: Packaged Relay assets are integrity-checked before setup

The packed CLI SHALL resolve the Relay deployment assets and configuration
schema from its installed package or the repository's reviewed development
assets, validate them against a shipped bundle manifest containing the closed
bundle and compatibility identities, and reject missing, altered, unexpected,
or incompatible files before generating adopter configuration or deployment
output. The validation SHALL not trust an arbitrary remote checksum URL and
SHALL preserve the existing generated bundle digest and approved publisher pin
contract.

#### Scenario: A valid packed Relay bundle is resolved

- **WHEN** setup selects Relay from an installed CLI containing the reviewed
  bundle manifest, source files, lockfile, schema, and license notice
- **THEN** setup verifies every declared file and compatibility identity before
  writing adopter output
- **AND** the generated configuration retains the verified bundle digest and
  approved immutable publisher/action pins

#### Scenario: A packaged Relay asset is tampered with

- **WHEN** a Relay source file, lockfile, template, schema, notice, or bundle
  manifest differs from its declared digest or declares an unexpected path
- **THEN** setup fails closed with a local integrity error
- **AND** it writes no Relay, workflow, registry, README, or configuration
  output

### Requirement: Setup handoff applies only to its exact reviewed base

The packed CLI SHALL provide a normal-PR setup-handoff apply operation. Before
writing, it SHALL validate the versioned handoff schema, repository identity,
shipped publisher pins, exact expected base commit and tree, closed managed
path allowlist, regular-file/UTF-8 constraints, byte lengths, and SHA-256
digests. It SHALL apply verified output through the existing atomic managed-file
boundary.

#### Scenario: Owner applies a valid handoff on its exact base branch

- **WHEN** the owner supplies a complete valid handoff and the expected base
  commit and tree of its local review branch match the handoff
- **THEN** the command writes the declared managed output atomically
- **AND** the owner can review it through the repository's ordinary PR flow

#### Scenario: Handoff is stale, malformed, or tampered

- **WHEN** the handoff has an unknown schema, wrong identity/pin/base tree,
  unsafe or extra path, symlink/reparse point, invalid UTF-8, missing file, or
  digest/length mismatch
- **THEN** the command rejects it before writing any managed file
- **AND** it does not downgrade to local setup, a PAT, or a GitHub App writer

### Requirement: Turnkey guidance separates bootstrap and publication authority

Turnkey Relay guidance SHALL describe the private trusted handoff and the
owner-operated normal PR required for first setup. It SHALL state that ongoing
publication uses the exact pinned GitHub OIDC publisher and requires neither a
GitHub App key nor any direct write to the base branch.

#### Scenario: Owner follows the no-App first setup guide

- **WHEN** an owner follows the Relay setup guide
- **THEN** the guide directs it to verify/apply the private handoff on a normal
  review branch and merge through existing branch protection
- **AND** it does not instruct the owner to create a GitHub App, use a PAT, or
  allow automated direct base-branch writes
