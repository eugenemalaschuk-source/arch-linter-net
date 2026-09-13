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

### Requirement: Setup proves capabilities fail-closed

Setup SHALL treat provider-plan declarations as cost metadata only. Required
checks, Rules API access, OIDC availability/claims, provider quota, account
identity, and deployment capability SHALL come from the bounded live inspector
for v1. An unsigned or caller-authored local capability-evidence JSON file SHALL
never satisfy a prerequisite; `--capability-evidence` SHALL fail closed with a
stable diagnostic until an authenticated evidence protocol is shipped. Missing
or contradictory live evidence SHALL prevent non-dry-run writes.

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

## ADDED Requirements

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
