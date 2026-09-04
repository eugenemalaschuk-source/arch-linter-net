## MODIFIED Requirements

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
aggregator SHALL reselect the unique declaration from the fixed
`tools/release/scopes/` collection using the candidate manifest's version,
recompute its SHA-256, and compare the declaration identity, authority story,
and exact required/excluded/delivered inventories against the supplied evidence
before evaluating required-item states. Evidence-supplied identity, hash, or
inventory fields SHALL NOT act as release authority. Authorization
SHALL be refused while any required item is not closed.

Missing, malformed, duplicate, incompatible, prerelease, emergency-override,
or otherwise unmapped target declarations SHALL fail closed. The current tracked
authorities SHALL preserve v0.6.4/#527 and define v0.7.0/#613 with required
#234, #116, #269, #267, and #614; #287 SHALL remain explicitly non-blocking and
#222 SHALL remain delivered context for v0.7.0. The tracked authorities SHALL
further define v0.8.0/#90 with required items #504, #91, #92, #93, #95, #633,
#672, #684, and #706; #510 and #673 SHALL remain explicitly non-blocking; and
#742 SHALL remain delivered context for v0.8.0. v0.8.0 SHALL NOT list itself as
a required item of its own declaration.

#### Scenario: Coexisting release targets select their own declarations
- **WHEN** otherwise valid v0.6.4, v0.7.0, and v0.8.0 candidate manifests are evaluated
- **THEN** each evidence record identifies only its exact target's declaration,
  authority story, and reviewed inventory
- **AND** no declaration can authorize a candidate targeting a different release

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

## ADDED Requirements

### Requirement: Checkpoint B proves the composed v0.8 governance workflow end to end
Checkpoint B SHALL execute one required v0.8 full-cycle scenario family, folded
into `_REQUIRED_SCENARIOS` and its owning shard(s) folded into `_REQUIRED_SHARDS`
exactly like every existing scenario family, proving the documented single-tool
command chain — policy check, analysis with applicability/completeness, declared
topology capture/diff/verify, visible contract-surface governance, policy
weakening/gate, measurement and at least one enforced budget, required
current-context external SARIF evidence binding, base/current architecture
change, Architecture Health, PR Markdown, and the Architecture Health badge —
against one coherent primary synthetic fixture, through the installed packed
candidate CLI only. A source-tree `ProjectReference`, `dotnet run` invocation,
or independently repacked bytes SHALL NOT stand in as product authority for
this family.

The scenario family SHALL also exercise the canonical Health matrix states
HEALTHY, DEBT, DEGRADING, FAILING, and UNASSESSABLE against bounded mutations
of the primary fixture's evidence, using the existing Health gate/health
resolution unchanged. HEALTHY, FAILING, and UNASSESSABLE SHALL report their
correct `gate`/`health` pair. DEGRADING is a warning-level signal only: per
the existing `ArchitectureHealthProjector.ResolveGate` implementation, a lone
Degrading dimension does not by itself fail the gate, so DEGRADING SHALL
report `gate: pass` unless another dimension independently fails or the
debt-gate itself does not pass. Where the DEBT or DEGRADING mutation exposes
a genuine, independently-reproducible defect in `health`'s own debt-gate or
validation-pass computation (confirmed outside the packed candidate, not a
fixture or test-authoring artifact), the scenario MAY assert the currently
observed outcome instead of the state the mutation was designed to produce,
provided the assertion is accompanied by a comment identifying the defect and
a tracked follow-up — this proves the composed pipeline is exercised
end-to-end without silently masking a real product gap. The scenario family
SHALL also prove: at least one recursive first-party contract-surface
exposure violation reached through a nested visible signature path, asserted
against its exposure-path evidence; and, on overlapping canonical facts,
agreement between JSON/SARIF/Testing finding projections and between
Health/report/badge outputs.

Missing, duplicate, unexpected, or failed scenarios in this family SHALL fail
platform and aggregate release evidence exactly like any existing Checkpoint B
scenario family.

#### Scenario: The full v0.8 command chain runs against one packed candidate
- **WHEN** the v0.8 full-cycle scenario executes against the installed
  Checkpoint B candidate CLI and its primary synthetic fixture
- **THEN** every documented stage from policy check through the Architecture
  Health badge completes using the same candidate CLI, feed, and consumer
  fixture state throughout
- **AND** no stage substitutes a source-tree build, `ProjectReference`, or a
  package from outside the immutable candidate manifest

#### Scenario: The canonical Health matrix is exercised on the primary fixture
- **WHEN** the primary fixture's policy, baseline, waiver, budget, or required
  external evidence is mutated to each of the HEALTHY, DEBT, DEGRADING,
  FAILING, and UNASSESSABLE shapes
- **THEN** the resulting canonical Health artifact reports the matching
  `gate`/`health` pair for HEALTHY, FAILING, and UNASSESSABLE
- **AND** DEGRADING reports `gate: pass` unless an independent dimension or
  the debt-gate itself also fails, matching the existing `ResolveGate`
  implementation rather than treating Degrading as inherently gate-blocking
- **AND** where DEBT or DEGRADING instead surfaces a confirmed,
  independently-reproduced defect in Health's own computation, the scenario
  asserts the observed outcome with a comment naming the defect and its
  tracked follow-up, rather than silently masking it

#### Scenario: A missing or wrong-context required external evidence artifact is unassessable
- **WHEN** the required external SARIF evidence binding is missing or bound to
  a revision or scope other than the current assessment context
- **THEN** the Health external-evidence dimension, and therefore overall
  Health, reports unassessable rather than a false healthy or failing result

#### Scenario: A newly unmapped required topology subject cannot false-green
- **WHEN** the current fixture state introduces a required first-party subject
  that the declared topology does not map, or maps ambiguously
- **THEN** the current-context Health/gate evidence reports unassessable for
  that dimension rather than passing

#### Scenario: Recursive first-party exposure is proven with real path evidence
- **WHEN** a selected contract surface's visible signature recursively exposes
  a forbidden first-party type through a nested generic, tuple, array, or
  wrapper position
- **THEN** the resulting finding's exposure-path evidence names the concrete
  recursive path segments that reached the forbidden type
- **AND** a coarse dependency-direction violation alone does not satisfy this
  scenario

#### Scenario: Normalized projections agree on overlapping canonical facts
- **WHEN** the same v0.8 evidence is projected to JSON, SARIF, Testing,
  Architecture Health, PR Markdown, and the Health badge
- **THEN** the projections agree on overlapping canonical finding identity,
  contract/rule identity, target, strict/audit meaning, Health category, gate
  where represented, effective rule/control count where represented, and
  explicit ignore/waiver debt total where represented
- **AND** an unassessable or unavailable state is reported as such rather than
  a fabricated zero or a recomputation in the reporting layer

#### Scenario: Library and Unity-style shapes prove their own boundaries without duplicating the full cycle
- **WHEN** the library (`api-surface-selector`) and Unity-style
  (`topology-review-unity`) synthetic fixtures run their shape-specific
  scenarios
- **THEN** each proves only its shape-specific boundary (selected public-API
  membership/role/exposure discipline for the library; declared Runtime/Editor
  topology, required-subject mapping, and runtime/public-surface exposure
  rejection for Unity-style) through the same canonical Health/report path
- **AND** neither re-executes the full server/modular pipeline merely to
  increase scenario count
- **NOTE**: the library shape is already satisfied by the existing
  `public-api-surface-selector-*` packed scenarios. The Unity-style shape is
  currently proven only in-process (`TopologyReviewLifecycleAcceptanceTests`),
  not through the packed Checkpoint B candidate CLI this requirement demands;
  a packed Unity-style shard remains an explicit, tracked follow-up rather
  than delivered by this change.

#### Scenario: A missing or duplicated v0.8 scenario fails evidence
- **WHEN** a platform's shard evidence omits a required v0.8 scenario ID,
  reports one more than once, or reports one not in the required inventory
- **THEN** platform evidence merge fails, and no canonical platform record or
  release authorization is produced from it
