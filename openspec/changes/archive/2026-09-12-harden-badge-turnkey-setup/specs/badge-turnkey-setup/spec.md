## ADDED Requirements

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

## MODIFIED Requirements

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
from a fresh identity-bound observation. A fresh destination with no qualifying
first evidence SHALL be unavailable. A healthy installation with a valid
bounded observation SHALL be reportable as available.

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
