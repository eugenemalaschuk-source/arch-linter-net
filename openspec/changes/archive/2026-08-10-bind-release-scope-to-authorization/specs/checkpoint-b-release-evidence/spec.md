## ADDED Requirements

### Requirement: Publication authorization proves the release scope is closed
The aggregation job SHALL consume an authoritative release-scope inventory bound to the candidate
manifest digest and source commit, listing every required release-scope item and its resolved
state. The required inventory SHALL be declared in the repository so it is reviewed like any other
release artifact, and the resolved state SHALL come from the issue tracker rather than the
declaration. Authorization SHALL be refused while any required item is not closed, and the
inventory, including the explicitly excluded items and their reasons, SHALL appear in the emitted
JSON and Markdown evidence.

#### Scenario: A required release-scope item is open
- **WHEN** any required item of the release scope is open at aggregation time
- **THEN** the evidence states FAIL, lists that item, and the aggregation job terminates
  unsuccessfully

#### Scenario: The inventory cannot be trusted
- **WHEN** the release-scope inventory is missing, unbound from the candidate manifest digest or
  source commit, empty, or contains an item with no resolved state
- **THEN** aggregation fails and no authorization statement is emitted

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
