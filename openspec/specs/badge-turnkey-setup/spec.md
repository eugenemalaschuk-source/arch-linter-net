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
The setup command SHALL generate a versioned configuration containing the selected mode/profile, approved repository and owner identities, workflow/action pins, destination binding, validity limits, bundle/compatibility identifiers, and manifest digests. Relay output SHALL include the Worker, SQLite Durable Object bindings/migrations, and deterministic templates needed for deployment without user-authored server code. Producer, publisher, optional-renewal workflows, and README changes SHALL be reviewable diffs and SHALL use the existing trusted promotion and stamped-SVG contracts.

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
Doctor SHALL produce machine-readable diagnostics plus concise human remediation for incompatible CLI/action/Relay/schema versions; identity/profile/pin mismatch; missing required checks, rules APIs, permissions, or unsupported plans; invalid OIDC audience/ref/workflow claims; inaccessible endpoints; missing, expired, or malformed artifacts; absent first evidence; expired validity; revoked destinations; storage/quota failures; and origin-versus-proxy cache delay. Public-facing diagnostics SHALL redact credentials, private source identity, full provenance, raw JWTs, and confidential IDs, while private detail SHALL remain clearly separated.

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

### Requirement: Configuration and distribution compatibility is fail closed
Setup SHALL reject unknown schema, disclosure profile, bundle, compatibility plan, malformed configuration, conflicting identity, or unsupported migration before registration or public writes. The distributable manifest SHALL bind every generated component to an explicit version and digest, and packed consumers SHALL be able to run setup/doctor using those assets without a source checkout or custom scripts.

#### Scenario: Unknown component is refused
- **WHEN** configuration names an unknown schema, profile, bundle, or compatibility plan
- **THEN** setup returns an unsupported-version diagnostic before any write
- **AND** it does not silently select a fallback component

#### Scenario: Packed install is source-independent
- **WHEN** a consumer invokes the setup/doctor command from the packed product
- **THEN** all required templates, schemas, and compatibility metadata resolve from shipped assets
- **AND** the output contains no project-source path requirement
