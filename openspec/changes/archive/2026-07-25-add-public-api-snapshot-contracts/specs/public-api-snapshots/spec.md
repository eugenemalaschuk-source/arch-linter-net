## ADDED Requirements

### Requirement: Versioned deterministic public API snapshot format

The system SHALL define a versioned public API snapshot text format carrying `@format`, `@version`, `@contract`, and `@assembly` directives followed by one normalized signature per line. Serialization SHALL order assemblies and signatures ordinal, collapse duplicates, use LF line endings with a trailing newline, and SHALL NOT embed timestamps, file paths, machine names, or tool version stamps.

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

The system SHALL compute a structured delta between a declared public API surface and an actual public API surface, separating additions, removals, and changed signatures. A member present on both sides under the same identity key (declaration kind, fully qualified name including generic arity, and parameter count) but with a different normalized signature SHALL be reported as a changed signature, not as an unrelated addition and removal.

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

The system SHALL require every snapshot path — whether declared as `api_snapshot` in a policy or supplied on the command line — to be repository-local: relative, non-rooted, and not escaping the policy boundary after normalization. The policy boundary is the policy file's directory, or that directory's parent when the policy lives in an `architecture` folder.

#### Scenario: Absolute snapshot path is rejected
- **WHEN** a policy declares an absolute `api_snapshot` path
- **THEN** policy loading SHALL fail with an error identifying the contract and the rejected path

#### Scenario: Escaping snapshot path is rejected
- **WHEN** a snapshot path normalizes to a location outside the policy boundary
- **THEN** the operation SHALL fail with an error identifying the rejected path and the boundary
