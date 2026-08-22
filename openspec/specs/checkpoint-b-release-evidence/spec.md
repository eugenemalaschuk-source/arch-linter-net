# checkpoint-b-release-evidence Specification

## Purpose
Define the release-blocking Checkpoint B contract: validate one immutable NuGet
candidate set on every required platform, preserve verifiable synthetic-adopter
evidence, and authorize publication only for the digest-verified files that
were tested.
## Requirements
### Requirement: Checkpoint B consumes packed candidate artifacts
The repository SHALL provide a deterministic NUnit Checkpoint B entrypoint that
consumes a supplied immutable candidate manifest, validates package metadata, dependency graph,
embedded resources, content files, versions, and digests, and consumes the CLI
and applicable Core, CEL, and Testing packages from an isolated local feed. The
entrypoint SHALL reuse the synthetic adoption corpus and SHALL NOT use a
`ProjectReference` as evidence for an external consumer scenario.

#### Scenario: Candidate is installed from the isolated feed
- **WHEN** the Checkpoint B entrypoint runs for a candidate version
- **THEN** every external-consumer scenario loads the manifest-verified candidate
  packages from the isolated feed and records their identities and digests

### Requirement: Final adopter matrix is executable and release-blocking
Checkpoint B SHALL execute the synthetic greenfield, conventional multi-project,
same-named multi-host, legacy-import migration, clean-checkout, direct CLI,
generic CI-neutral, and `ArchLinterNet.Testing` acceptance scenarios. It SHALL
also execute non-TTY, offline packaged-schema, sequential/default-parallel,
cache disabled/population/hit/corruption, and cancellation/publication
interruption scenarios where their owning capability is available. Any failed
scenario SHALL produce a failed Checkpoint B result and SHALL block release
authorization.

#### Scenario: Matrix detects a failed invariant
- **WHEN** any scenario reports different canonical results, unsafe publication,
  missing packaged schema, or non-zero external-consumer failure
- **THEN** the evidence marks Checkpoint B failed and does not authorize 0.5.1

### Requirement: Release evidence is deterministic, synthetic, and explicit
The repository SHALL produce a deterministic immutable workflow-artifact
Checkpoint B evidence summary containing the tested commit, candidate package identities and digests,
scenario inventory and results, observed platform/runtime/shell matrix, support
exclusions and rationale, performance-evidence reference, OpenSpec,
self-architecture, package, and documentation gate results, and an explicit
pass-or-fail authorization statement. The summary SHALL state that all
identities are synthetic and SHALL NOT contain private adopter identities.

#### Scenario: Evidence authorizes the candidate
- **WHEN** every required scenario and gate succeeds
- **THEN** the summary explicitly records that Checkpoint B passed and that the
  tested candidate is authorized for 0.5.1 publication

### Requirement: Checkpoint B records a complete executable scenario oracle
Every required Checkpoint B fixture SHALL declare its expected exit category,
canonical findings or identities, completion status, and permitted diagnostics.
The matrix SHALL include clean checkout, direct CLI, CI-neutral wrapper, Testing
API, POSIX and PowerShell entrypoints, non-TTY output, documented command
examples, sequential/default parallelism, cache miss/population/verified
hit/corruption, and in-flight cancellation/publication interruption.

#### Scenario: A mode fails identically
- **WHEN** every execution mode returns the same incorrect result
- **THEN** Checkpoint B fails because the result does not match the fixture oracle

### Requirement: Evidence is schema-backed and cannot self-authorize
The aggregation job SHALL validate exactly one evidence record for every required
platform, its observed architecture and shell, required scenario inventory,
consumer policy-shape counters, candidate package manifest, and independently produced
repository-gate results. It SHALL reject a platform record whose declared result contradicts its
own scenario results. The required scenario inventory SHALL include the public-API surface-selector
consumer-exit matrix alongside the existing consumer-cleanup matrix. It SHALL emit an immutable
GitHub Actions workflow artifact containing the candidate-manifest digest and workflow-run
reference, an explicit PASS or FAIL publication statement naming the candidate version, and the
inventory of failed scenarios and policy-shape defects; it SHALL NOT hard-code successful gates or
authorization. It SHALL terminate unsuccessfully when the verdict is FAIL. This artifact is the
authoritative release record and is retained according to the repository artifact-retention policy;
generated evidence is not checked into the source tree.

#### Scenario: Evidence is incomplete
- **WHEN** a platform record, required scenario, policy-shape counter, gate result, or manifest
  digest is absent, duplicated, mismatched, or invalid
- **THEN** aggregation fails and no authorization statement is emitted

#### Scenario: A required scenario failed
- **WHEN** any required scenario is recorded as failed on any platform
- **THEN** the emitted evidence states FAIL for the candidate version, lists the failed scenario
  and its reason, and the aggregation job terminates unsuccessfully

### Requirement: Checkpoint B evidence has executable, duplicate-free scenario outcomes
Every Checkpoint B scenario record SHALL be returned by the oracle that executed
the scenario. The aggregator SHALL reject a platform record with a duplicate,
missing, or unexpected scenario ID before authorization.

#### Scenario: A scenario is duplicated
- **WHEN** a platform evidence record contains two entries with the same scenario ID
- **THEN** aggregation fails and no release authorization is emitted

### Requirement: The packed gate executes a release-blocking consumer-cleanup matrix
The packed-artifact gate SHALL execute, against the candidate tool and packages installed from
the isolated local feed, one required scenario for every adoption finding the release claims to
fix and for the reusable source-set authoring model it introduces: composed assembly-free policy
validation, non-destructive build preparation, the reviewed public-API snapshot workflow,
strict-cycle baseline scope, dependency contract id parity, actionable schema diagnostics, shared
-framework analysis, declared layer-overlap allowance, namespace allowance glob semantics,
JSON-formatted configuration-error termination, candidate release identity, source-set assembly
authoring, discovered-project-set authoring, source-set enrolment, and fail-closed stale source
selectors. Each scenario SHALL be proven from the installed candidate and SHALL NOT accept a
source-tree `ProjectReference` as evidence.

#### Scenario: A fixed finding regresses in the packed candidate
- **WHEN** a consumer-cleanup scenario cannot be satisfied by the installed candidate
- **THEN** the platform evidence records that scenario as failed and the aggregated evidence does
  not authorize publication

#### Scenario: A required scenario is unreachable on a platform
- **WHEN** a platform cannot execute a required scenario
- **THEN** its evidence records the scenario as not applicable with a reason and at least one
  other platform records it as passed

### Requirement: The canonical consumer policy shape is typed release evidence
Every platform evidence record SHALL carry typed counters describing the synthetic consumer
policy the matrix validated: composed policy documents and imported fragments, governed module
assemblies, authored directional assembly contracts and their expanded instances, governed
projects and the project-metadata contracts reusing them, copied project inventories, and inline
public-API signatures. The aggregation job SHALL reject a candidate whose counters show a
workaround shape this release exists to remove — a forced policy monolith, directional assembly
contracts authored once per module, a copied project inventory where solution discovery can be
authoritative, or an inline public-API inventory instead of a reviewed snapshot.

#### Scenario: Directional contracts are still copied per module
- **WHEN** the consumer policy authors at least one directional assembly contract per governed
  module assembly
- **THEN** aggregation records a policy-shape defect and does not authorize publication

#### Scenario: Deduplicated authoring is recorded
- **WHEN** one authored directional assembly contract expands across every governed module
  assembly and project-metadata contracts reuse one discovered project set
- **THEN** the evidence records the authored/expanded counts as the release's policy-shape proof

### Requirement: A tracked defect blocks release without hiding itself
The executable gate SHALL fail when a scenario fails without a registry entry, and SHALL also fail
when a registered scenario starts satisfying its contract, so the entry is removed and the scenario
gates the release again. A consumer-cleanup scenario whose failure is a separately tracked product
defect MAY be recorded in a registry that names its tracking issue, but the registry SHALL NOT
change the recorded scenario result, the platform result, or the authorization outcome.

#### Scenario: A tracked defect is silently fixed
- **WHEN** a registered scenario satisfies its contract
- **THEN** the gate fails and names the registry entry that must be removed

### Requirement: Publication authorization proves the release scope is closed
The aggregation job SHALL consume an authoritative release-scope inventory
selected from a fixed tracked declaration collection by the immutable candidate
manifest's exact stable release version. Declaration filenames SHALL NOT be
semantic authority. Every declaration SHALL expose a versioned schema, explicit
declaration identity, release target, release-authority story, required items,
explicitly excluded items with reasons, and delivered-context items with
reasons. The generator SHALL accept no caller-supplied declaration path.

The selected inventory SHALL bind the declaration identity and SHA-256,
candidate version, candidate manifest digest, and source commit, list every
required item with its live issue-tracker state, and include excluded and
delivered-context inventories in the emitted JSON and Markdown evidence. The
aggregator SHALL verify all of those bindings before authorization. Authorization
SHALL be refused while any required item is not closed.

Missing, malformed, duplicate, incompatible, prerelease, emergency-override,
or otherwise unmapped target declarations SHALL fail closed. The current tracked
authorities SHALL preserve v0.6.4/#527 and define v0.7.0/#613 with required
#234, #116, #269, #267, and #614; #287 SHALL remain explicitly non-blocking and
#222 SHALL remain delivered context for v0.7.0.

#### Scenario: Coexisting release targets select their own declarations
- **WHEN** otherwise valid v0.6.4 and v0.7.0 candidate manifests are evaluated
- **THEN** each evidence record identifies only its exact target's declaration,
  authority story, and reviewed inventory
- **AND** neither declaration can authorize the other candidate

#### Scenario: Candidate target has no unique supported declaration
- **WHEN** a candidate uses a prerelease, unknown patch/minor, malformed,
  inconsistent, or duplicate-mapped release target
- **THEN** declaration selection fails before issue resolution or evidence
  output

#### Scenario: A required release-scope item is open
- **WHEN** any required item of the selected release scope is open at
  aggregation time
- **THEN** the evidence states FAIL, lists that item, and the aggregation job
  terminates unsuccessfully

#### Scenario: An excluded issue remains open
- **WHEN** an explicitly excluded non-blocking item remains open
- **THEN** it remains listed with its reviewed reason
- **AND** it does not fail release-scope authorization

#### Scenario: The inventory cannot be trusted
- **WHEN** the release-scope inventory is missing, lacks a valid declaration
  identity or hash, does not match the candidate version, manifest digest, or
  source commit, is empty, or contains an item with no resolved state
- **THEN** aggregation fails and no authorization statement is emitted

#### Scenario: Scope evidence is reused for another candidate
- **WHEN** a release-scope artifact from another candidate source, manifest, or
  release target is supplied to aggregation
- **THEN** aggregation rejects the binding before it can authorize publication

### Requirement: The public-API scenario observes every delta class
The packed gate SHALL drive the reviewed public-API snapshot lifecycle from the installed
candidate across an ordinary API evolution that adds one exported signature, removes one, and
changes one. It SHALL assert that each delta class is reported, and that `update` restores the
reviewed snapshot to a clean comparison.

#### Scenario: A delta class stops being reported
- **WHEN** the exact snapshot comparison fails to report an added, removed, or changed signature
- **THEN** the scenario fails and the candidate is not authorized

### Requirement: Non-destructive build preparation is proven on both entrypoints
The packed gate SHALL prove non-destructive build preparation through the installed CLI and
through a packaged `ArchLinterNet.Testing` consumer performing two consecutive `WithEnsureBuilt()`
validations in one process without an intervening rebuild. The preservation oracle SHALL cover
every selected primary build output, not assemblies alone.

#### Scenario: Repeated packaged validation disturbs an output
- **WHEN** a second consecutive packaged `WithEnsureBuilt()` validation fails, or any selected
  primary output is removed or rewritten
- **THEN** the scenario fails and the candidate is not authorized

### Requirement: The packed gate executes a release-blocking public-API surface-selector consumer-exit matrix
The packed-artifact gate SHALL execute, against the candidate tool and packages installed from the
isolated local feed, a required scenario group proving that `surface_selector` on
`strict_public_api_surface`/`audit_public_api_surface` contracts lets a modular consumer replace a
whole-assembly reviewed API snapshot with a materially smaller intentional snapshot. The group
SHALL prove: the selected snapshot omits incidental exported types that an assembly-wide sibling
contract with no selector still governs; selection through a user-owned `has_attribute` marker and
through at least one other bounded selector source (`namespace`, `base_type`, `implements_interface`,
`layer`, or `role`) both produce a materially reduced snapshot; a selected type whose existing
semantic role is not `ApiContract` retains that role and remains governed by an ordinary role-based
contract unchanged; the exact snapshot comparison reports an added, removed, and changed selected
signature and `update` restores a clean comparison; adding or removing selector-matching evidence on
a type is observed as a review-visible snapshot delta; a selected member's signature referencing an
unselected first-party exported type fails closed instead of silently escaping; a full-policy strict
run over the fixture's permanent selector contracts is green; and the CLI and packaged
`ArchLinterNet.Testing` resolve the same effective selected surface and normalized findings for the
same contract.

#### Scenario: A selector regresses to whole-assembly behavior in the packed candidate
- **WHEN** the selected snapshot produced by the installed candidate contains an incidental exported
  type the selector should have excluded
- **THEN** the platform evidence records the snapshot-reduction scenario as failed and the
  aggregated evidence does not authorize publication

#### Scenario: A selected first-party escape is not rejected
- **WHEN** capturing or validating a selected contract whose member signature references an
  unselected first-party exported type does not fail
- **THEN** the platform evidence records the fail-closed-escape scenario as failed and the aggregated
  evidence does not authorize publication

### Requirement: Checkpoint B scenario shards merge into the canonical platform evidence contract

Checkpoint B MAY execute its required scenario inventory as isolated deterministic scenario shards to reduce wall-clock latency. Shard artifacts are intermediate execution evidence and SHALL NOT independently authorize release.

Before final release aggregation, the repository SHALL merge exactly the required shard set into one canonical `checkpoint-b-platform-evidence/v1` record per platform. The merge SHALL verify that all shards are bound to the same candidate version, source commit, manifest digest, package inventory, observed platform/runtime/architecture, and shell adapter. It SHALL reject a missing, duplicate, unexpected, or overlapping shard/scenario inventory and SHALL require the union of scenario IDs to equal the authoritative required scenario inventory exactly.

#### Scenario: A scenario is lost between shards

- **WHEN** the union of Checkpoint B shard scenario IDs omits any authoritative required scenario
- **THEN** platform evidence merge fails
- **AND** no canonical platform record is emitted for release authorization

#### Scenario: Two shards execute the same scenario ID

- **WHEN** two shard records contain the same scenario ID
- **THEN** platform evidence merge fails as an overlap
- **AND** final release aggregation cannot authorize the candidate

#### Scenario: Shards disagree on candidate provenance

- **WHEN** any shard reports a different source commit, candidate version, manifest digest, or package inventory
- **THEN** platform evidence merge fails before canonical evidence is produced

#### Scenario: Consumer policy shape remains canonical platform evidence

- **WHEN** the consumer-cleanup layer-overlap-and-policy-shape shard completes
- **THEN** it reports the typed consumer policy-shape counters
- **AND** the platform merge requires exactly that shard to supply the counters copied into the canonical platform record

### Requirement: Checkpoint B subprocess cancellation bounds the process tree

Checkpoint B subprocess execution SHALL observe the NUnit cancellation token while waiting for child processes. When cancellation or the test timeout fires, the gate SHALL terminate the complete descendant process tree before propagating cancellation so timed-out `dotnet`, shell, MSBuild, or synthetic-consumer processes cannot continue mutating temporary state after the test has ended.

#### Scenario: A child process owns a long-running descendant

- **WHEN** Checkpoint B cancellation fires while a subprocess tree is still running
- **THEN** the direct subprocess and its descendants terminate
- **AND** the test returns cancellation rather than waiting for the original child duration

### Requirement: Checkpoint B preserves the complete candidate subject inventory
Checkpoint B platform records and final release evidence SHALL retain and
compare the complete canonical candidate package-subject inventory, including
the explicit primary-package and symbol-package pair for each package ID. They
SHALL reject a record or artifact whose manifest schema, source commit, version,
paired inventory, file identity, size, or digest differs from the candidate
manifest used by the release workflow.

#### Scenario: Platform evidence omits a symbol package
- **WHEN** a platform record reports primary packages but omits a manifest
  symbol subject
- **THEN** Checkpoint B evidence aggregation fails and publication is not
  authorized

#### Scenario: Candidate bytes are modified after packing
- **WHEN** a package or paired symbol file changes after the canonical manifest
  is created
- **THEN** downstream candidate verification fails before release evidence can
  authorize publication

### Requirement: Checkpoint B precedes pre-publication provenance authority
Checkpoint B acceptance SHALL remain a required prerequisite for GitHub build
provenance generation. Its successful candidate authorization SHALL hand the
same immutable package manifest and derived checksum evidence to the separate
provenance authority gate; it SHALL NOT permit NuGet publication or GitHub
Release attachment until that gate independently verifies every attestation.

#### Scenario: Checkpoint B fails
- **WHEN** Checkpoint B does not authorize the immutable candidate
- **THEN** the provenance-producing job and every publication handoff do not
  run

#### Scenario: Checkpoint B passes
- **WHEN** Checkpoint B authorizes the immutable candidate
- **THEN** the workflow re-verifies that same frozen candidate and its outer
  evidence before provenance authority can pass

