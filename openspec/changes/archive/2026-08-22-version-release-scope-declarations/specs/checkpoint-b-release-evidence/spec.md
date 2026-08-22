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
