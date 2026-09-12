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

Generated Relay assets SHALL bind immutable repository and owner IDs, repository
display identity, opaque alias, exact audience, disclosure profile, and the
configured reusable-workflow OIDC claim. Setup SHALL never copy synthetic
fixture identity or a fixture `wrangler.jsonc` into an adopter project.
The producer configuration SHALL record the Git blob SHA of the exact generated
consumer producer workflow. The upstream reusable-workflow commit pin SHALL be
stored separately and SHALL not be used as `producer.workflow_sha`.
Generated README wiring SHALL use a Shields endpoint for JSON snapshot output
and the Relay stamped SVG route for freshness output.

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
Dry-run and doctor SHALL be read-only. A real setup SHALL write through temporary files and an atomic commit boundary, preserve existing manual settings and protected branch/ruleset/secrets, and record enough manifest state to retry or roll back. Repeating setup with the same approved identity SHALL not create another alias or overwrite manual changes. Conflicts, partial failures, unsupported account/plan limits, and retries SHALL leave the public destination unavailable or unregistered rather than half-authorized.

#### Scenario: Dry-run does not mutate
- **WHEN** a user runs setup in dry-run mode or runs doctor
- **THEN** no local file, registry entry, hosting resource, workflow, or README is written
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
identity, and deployment capability SHALL come from approved live or
shape-validated capability evidence. Missing or contradictory evidence SHALL
prevent non-dry-run writes and SHALL produce stable diagnostics.

#### Scenario: Declared plan is insufficient proof

- **WHEN** a user supplies `--provider-plan pro` without capability evidence
- **THEN** Relay setup remains unavailable
- **AND** it does not mark required check, Rules API, OIDC, or quota prerequisites
  as satisfied

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
declare UTC cron trigger slots anchored at midnight and separated by that
cadence until the end of the UTC day. The declared slots SHALL equal the
preview's `ceil(1440 / cadence_minutes)` jobs per day; multiple POSIX cron
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
