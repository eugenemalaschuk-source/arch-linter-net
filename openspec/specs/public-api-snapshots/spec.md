# public-api-snapshots Specification

## Purpose
Let a large exported API surface be governed as a reviewed file diff instead of a hand-maintained inline signature list: a versioned, deterministic snapshot format plus capture, structured diff, safe update, and inline-list migration operations available through the CLI and the Core application seam.
## Requirements
### Requirement: Versioned deterministic public API snapshot format

The system SHALL define a versioned public API snapshot text format carrying `@format`, `@version`, `@contract`, and `@assembly` directives followed by one normalized signature per line. Serialization SHALL order assemblies and signatures ordinal, collapse duplicates, use LF line endings with a trailing newline, and SHALL NOT embed timestamps, file paths, machine names, or tool version stamps.

Captured entries SHALL use the exact grammar: the identity signature followed by a deterministic detail suffix carrying constant and enum member values, enum underlying type, accessor shape and accessor visibility, static/abstract/virtual/override/sealed/readonly modifiers, `ref`/`out`/`in`/`params` parameter direction, and generic constraints.

#### Scenario: Constant value change is visible in the snapshot
- **WHEN** an exported `const` field's value changes while its declaration is otherwise unchanged
- **THEN** the captured snapshot SHALL differ, and exact validation SHALL report the member as changed

#### Scenario: Accessor shape change is visible in the snapshot
- **WHEN** an exported property gains a setter, or an accessor's visibility widens
- **THEN** the captured snapshot SHALL differ from the previously reviewed one

#### Scenario: Parameter direction change is visible in the snapshot
- **WHEN** an `out` parameter becomes a `ref` parameter of the same type
- **THEN** the captured snapshot SHALL differ from the previously reviewed one

#### Scenario: Snapshot ownership is recorded
- **WHEN** a snapshot is captured for a contract
- **THEN** it SHALL carry an `@contract` directive naming that contract

#### Scenario: Capturing the same surface twice is byte-identical
- **WHEN** the same contract's exported surface is captured twice without any change to the target assemblies
- **THEN** the two snapshot documents SHALL be byte-identical

#### Scenario: Serialization is host independent
- **WHEN** a snapshot is serialized on any host operating system
- **THEN** every line SHALL be terminated with a single LF character and the document SHALL end with a trailing newline

#### Scenario: Unsupported version is rejected
- **WHEN** a snapshot declares `@version 2`
- **THEN** parsing SHALL fail with an error naming the unsupported version and the supported version

#### Scenario: Unknown directive is rejected
- **WHEN** a snapshot contains an unrecognized `@` directive
- **THEN** parsing SHALL fail with an error naming the directive, rather than ignoring it

#### Scenario: Entry before any assembly directive is rejected
- **WHEN** a snapshot contains a signature line before its first `@assembly` directive
- **THEN** parsing SHALL fail with an error identifying the line number

#### Scenario: Snapshot size is bounded
- **WHEN** a snapshot exceeds the supported maximum entry count or maximum line length
- **THEN** parsing SHALL fail with a deterministic error naming the exceeded bound

### Requirement: Structured public API delta

The system SHALL compute a structured delta between a declared public API surface and an actual public API surface, separating additions, removals, and changed signatures. A member present on both sides under the same identity key (assembly, declaration kind, fully qualified name including generic arity, and parameter count) but with a different normalized signature SHALL be reported as a changed signature, not as an unrelated addition and removal.

A declared entry with no assembly attribution (an inline `declared_api` entry) SHALL act as a wildcard matching any assembly.

#### Scenario: Added signature is reported as an addition
- **WHEN** the actual surface contains a normalized signature absent from the declared surface, with no declared entry sharing its identity key
- **THEN** the delta SHALL report it as an addition

#### Scenario: Removed signature is reported as a removal
- **WHEN** the declared surface contains a normalized signature absent from the actual surface, with no actual entry sharing its identity key
- **THEN** the delta SHALL report it as a removal

#### Scenario: Changed signature is reported once as a change
- **WHEN** a declared member's return or parameter type changes while its kind, qualified name, and parameter count stay the same
- **THEN** the delta SHALL report exactly one changed entry carrying both the previous and the current normalized signature, and SHALL NOT also report an addition or a removal for it

#### Scenario: Overload addition is not misreported as a change
- **WHEN** a new overload with a different parameter count is added to an already declared method
- **THEN** the delta SHALL report it as an addition

#### Scenario: Enum member and public constant changes are detected
- **WHEN** an exported enum member or public `const` field is added, removed, or has its type changed
- **THEN** the delta SHALL report it under the corresponding addition, removal, or change category

#### Scenario: Same signature in another assembly does not mask a removal
- **WHEN** two assemblies export the same fully qualified signature and it is removed from one of them
- **THEN** the delta SHALL report exactly one removal, attributed to the assembly that lost it

#### Scenario: Same identity in different assemblies is not paired as a change
- **WHEN** a declared entry in one assembly and an actual entry in a different assembly share an identity key but not a signature
- **THEN** the delta SHALL report a removal and an addition, not a cross-assembly change

#### Scenario: Unattributed inline entry matches any assembly
- **WHEN** a declared entry carries no assembly name and the signature is exported by one of the contract's assemblies
- **THEN** the delta SHALL treat it as matched

#### Scenario: Delta ordering is deterministic
- **WHEN** the same declared and actual surfaces are compared twice
- **THEN** the additions, removals, and changes SHALL be produced in the same order both times

### Requirement: Capture a public API snapshot

The system SHALL provide a `public-api capture` operation that resolves a named public API surface contract from a policy, captures the exported surface of its assemblies, and returns a snapshot document. Capture SHALL NOT overwrite an existing snapshot file whose content differs unless overwriting is explicitly requested.

#### Scenario: Capture writes a new snapshot
- **WHEN** `public-api capture --policy <policy> --contract <id> --output <path>` runs and `<path>` does not exist
- **THEN** the snapshot SHALL be written to `<path>` and the operation SHALL report the captured entry count

#### Scenario: Capture refuses to silently overwrite
- **WHEN** capture targets an existing file whose content differs from the captured snapshot and overwriting was not explicitly requested
- **THEN** the operation SHALL fail without writing and SHALL name the update path that would accept the change

#### Scenario: Capture over an identical file succeeds
- **WHEN** capture targets an existing file whose content is byte-identical to the captured snapshot
- **THEN** the operation SHALL succeed and report that the snapshot is already current

#### Scenario: Unknown contract id fails
- **WHEN** capture names a contract id that is not a public API surface contract in the policy
- **THEN** the operation SHALL fail with an error listing the available public API surface contract ids

#### Scenario: First capture of a declared but not yet created snapshot succeeds
- **WHEN** a policy already declares `api_snapshot` for a file that does not exist and capture targets that path
- **THEN** the policy SHALL load and the capture SHALL succeed, creating the file

### Requirement: Diff a public API snapshot against the live surface

The system SHALL provide a `public-api diff` operation that compares a snapshot file against the live exported surface of a contract's assemblies and reports additions, removals, and changed signatures separately.

#### Scenario: Diff reports separated deltas
- **WHEN** the live surface adds one member, removes another, and re-signs a third relative to the snapshot
- **THEN** the diff SHALL report one addition, one removal, and one changed signature in separate sections

#### Scenario: Diff of an unchanged surface reports no drift
- **WHEN** the live surface matches the snapshot exactly
- **THEN** the diff SHALL report no additions, removals, or changes and SHALL indicate the snapshot is in sync

### Requirement: Update a public API snapshot without silent writes

The system SHALL provide a `public-api update` operation that rewrites a snapshot from the live surface, and SHALL support a dry-run mode that reports the structured delta and the proposed file content without writing anything.

#### Scenario: Dry-run previews without writing
- **WHEN** `public-api update --snapshot <path> --dry-run` runs against a drifted surface
- **THEN** the operation SHALL print the structured delta and the proposed snapshot content, and `<path>` SHALL be unchanged on disk

#### Scenario: Update rewrites the snapshot
- **WHEN** `public-api update --snapshot <path>` runs without dry-run against a drifted surface
- **THEN** `<path>` SHALL be replaced with the captured snapshot and the applied delta SHALL be reported

#### Scenario: Update preserves unchanged reviewed entries
- **WHEN** an update applies additions and removals to a snapshot
- **THEN** every entry that is unchanged SHALL appear in the updated snapshot exactly as before, so the file diff contains only the changed lines

#### Scenario: Update of an inline declaration is refused, not guessed
- **WHEN** `public-api update` targets a contract that declares its surface inline via `declared_api` and has no `api_snapshot`
- **THEN** the operation SHALL refuse to write, SHALL state that a comment-preserving round-trip of the policy file is unavailable, and SHALL name the migration path

### Requirement: Migrate an inline declaration to a snapshot without accepting drift

The system SHALL provide a `public-api migrate` operation that converts a contract's inline `declared_api` list into a snapshot file, classifying every inline entry that is absent from the live surface and every live entry that is absent from the inline list, and SHALL refuse to write while such drift is unacknowledged.

#### Scenario: Clean inline list migrates
- **WHEN** a contract's inline `declared_api` list exactly matches the live exported surface
- **THEN** migrate SHALL write a snapshot containing those entries and report no drift

#### Scenario: Drifted inline list is not silently accepted
- **WHEN** the inline list differs from the live surface and drift acceptance was not requested
- **THEN** migrate SHALL fail without writing and SHALL list every stale inline entry and every undeclared live entry

#### Scenario: Drift can be explicitly accepted
- **WHEN** drift acceptance is explicitly requested
- **THEN** migrate SHALL write a snapshot describing the live surface and SHALL still report the accepted drift

#### Scenario: Dry-run migration writes nothing
- **WHEN** migrate is run in dry-run mode
- **THEN** it SHALL report the drift and the destination it would have written, and SHALL NOT write any file

### Requirement: Fail on unusable build state before capture, diff, update, and migrate

The system SHALL run the same build-state preflight used by validation before any capture, diff, update, or migrate operation, and SHALL fail the operation when preflight is blocked by a missing, stale, or wrong-target-framework assembly.

#### Scenario: Missing assembly blocks capture
- **WHEN** a contract's target assembly cannot be resolved
- **THEN** the operation SHALL fail with the preflight diagnostics and SHALL NOT produce a snapshot

#### Scenario: Stale assembly blocks update
- **WHEN** build-state preflight reports the resolved assembly as stale relative to its project sources
- **THEN** the update SHALL fail with the preflight diagnostics and SHALL NOT write the snapshot

### Requirement: Snapshot paths are repository-local and bounded

The system SHALL require every snapshot path — whether declared as `api_snapshot` in a policy or supplied on the command line — to be repository-local: relative, non-rooted, and not escaping the policy boundary after normalization. The policy boundary is the policy file's directory, or that directory's parent when the policy lives in an `architecture` folder. The policy file itself SHALL NOT be an accepted snapshot destination.

The system SHALL resolve every artifact path once, in the application seam, and SHALL perform every existence check, read, staging write, and atomic replacement against that resolved path.

#### Scenario: Command-line destination is boundary-checked
- **WHEN** a command-line snapshot destination is absolute or escapes the policy boundary
- **THEN** the operation SHALL fail without reading or writing any file, even when overwriting was explicitly requested

#### Scenario: Policy file is refused as a destination
- **WHEN** a snapshot destination resolves to the policy file
- **THEN** the operation SHALL fail without writing

#### Scenario: Resolved destination is what gets written
- **WHEN** the process working directory is not the repository root
- **THEN** the file written SHALL be the boundary-resolved destination, not the authored string resolved against the working directory

#### Scenario: Absolute snapshot path is rejected
- **WHEN** a policy declares an absolute `api_snapshot` path
- **THEN** policy loading SHALL fail with an error identifying the contract and the rejected path

#### Scenario: Escaping snapshot path is rejected
- **WHEN** a snapshot path normalizes to a location outside the policy boundary
- **THEN** the operation SHALL fail with an error identifying the rejected path and the boundary


### Requirement: Snapshot ownership is enforced

The system SHALL verify that a snapshot's `@contract` directive matches the contract consuming it and that every assembly the snapshot describes is declared by that contract, before its entries are used or the file is overwritten.

#### Scenario: Snapshot captured for another contract is rejected
- **WHEN** a contract consumes a snapshot whose `@contract` directive names a different contract
- **THEN** the system SHALL report an ownership error and SHALL NOT use its entries

#### Scenario: Snapshot without a contract directive is rejected
- **WHEN** a snapshot carries no `@contract` directive
- **THEN** the system SHALL report an ownership error

#### Scenario: Snapshot describing an undeclared assembly is rejected
- **WHEN** a snapshot contains entries attributed to an assembly the contract does not declare
- **THEN** the system SHALL report an ownership error naming that assembly

#### Scenario: Update targeting a different file is refused
- **WHEN** `public-api update` is given a snapshot path that does not resolve to the contract's own `api_snapshot`
- **THEN** the operation SHALL fail without writing

### Requirement: Operation outcomes carry a typed failure category

The system SHALL distinguish a completed gate that found drift from an operation that did not complete, and hosts SHALL map only the former to a drift exit code.

#### Scenario: Refused migration drift is a drift failure
- **WHEN** migrate refuses because the inline list drifted from the live surface
- **THEN** the failure category SHALL be drift and the CLI SHALL return exit code 1

#### Scenario: Unknown contract is not a drift failure
- **WHEN** an operation fails because the contract id is unknown, a path is unsafe, a snapshot is unusable, or preflight is blocked
- **THEN** the failure category SHALL be invalid input and the CLI SHALL return exit code 2

### Requirement: Machine-readable output is a single parsable document

The system SHALL emit exactly one parsable document per invocation for machine formats, and SHALL reject a requested format it does not implement for that operation rather than emitting a different format.

#### Scenario: JSON update output is one document
- **WHEN** `public-api update --format json` runs, including in dry-run mode
- **THEN** stdout SHALL parse as a single JSON document carrying status, destination, delta, and proposed content, with no additional prose

#### Scenario: Unsupported SARIF request is rejected
- **WHEN** `sarif` is requested for capture, update, or migrate
- **THEN** the operation SHALL fail with an error naming the supported formats, rather than emitting human-readable text
