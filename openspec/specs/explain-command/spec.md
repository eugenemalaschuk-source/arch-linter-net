# explain-command Specification

## Purpose
TBD - created by archiving change add-dependency-graph-export-and-explain. Update Purpose after archive.
## Requirements
### Requirement: `explain` CLI verb reports a dependency path between two nodes
The system SHALL provide an `explain` CLI verb accepting `--source`, `--target`, `--policy`, `--level` (`namespace` default, `type`), `--format` (`human` default, `json`), and `--condition-set` options, which builds the dependency graph internally and reports the relationship between the `--source` and `--target` nodes.

#### Scenario: Direct dependency is reported
- **WHEN** a direct edge exists from the `--source` node to the `--target` node
- **THEN** the command reports a path of length one and any contract IDs on that edge

#### Scenario: Transitive dependency is reported via shortest path
- **WHEN** no direct edge exists but a path exists through one or more intermediate nodes
- **THEN** the command reports the shortest path (as an ordered list of node IDs) and the contract IDs on each hop

### Requirement: No-path result is a successful outcome, not an error
When no path exists between `--source` and `--target`, the `explain` command SHALL report an explicit "no dependency path found" result and exit with code `0`, distinct from a runtime error.

#### Scenario: Unreachable target reports no path
- **WHEN** no first-party or external edge connects `--source` to `--target` at the requested level
- **THEN** the command prints a "no dependency path found" message (or `"path": null` in JSON format) and exits with code `0`

### Requirement: External-group target is explainable
When `--target` matches the name of a declared external dependency group, the `explain` command SHALL resolve any first-party node with an edge into that group's `External` node and report the match, including the contract ID when the edge corresponds to a violation.

#### Scenario: Explaining a violation against an external group
- **WHEN** `--target` is `logging-libs`, a declared external dependency group, and a first-party namespace has an edge into that group tagged with contract id `no-logging-in-domain`
- **THEN** the command reports that namespace as the source of the dependency and includes `"no-logging-in-domain"` in the result

#### Scenario: External group with no matching first-party edge
- **WHEN** `--target` is a declared external dependency group name but no first-party node has an edge into it
- **THEN** the command reports "no dependency path found" and exits with code `0`

### Requirement: Assembly-level explain is rejected
The `explain` command SHALL reject `--level assembly` with a clear error message and exit code `2`, since assembly-level graphs support only direct-edge presence checks, not path resolution.

#### Scenario: Assembly level rejected
- **WHEN** a user runs `arch-linter-net explain --source A --target B --level assembly`
- **THEN** the command prints an error explaining that assembly-level explain is not supported and suggesting `graph --level assembly` instead, and exits with code `2`

### Requirement: JSON output format for explain
The `--format json` output SHALL include the resolved `source`, `target`, the `path` (an ordered array of node IDs, or `null` when no path exists), and a `contractIds` array (deduplicated, ordinally sorted) aggregating all contract IDs found across the reported path's edges.

#### Scenario: JSON result includes ordered path and contract IDs
- **WHEN** `arch-linter-net explain --source A --target C --format json` finds path `[A, B, C]` where edge A→B has contract id `x` and edge B→C has contract id `y`
- **THEN** the JSON output has `"path": ["A", "B", "C"]` and `"contractIds": ["x", "y"]`

### Requirement: Explain reports CEL expression participation on the resolved path
When the `explain` command resolves a path (or direct edge) whose contract IDs include a context-dependency or context-allow-only contract with a `when`-bearing selector, the command SHALL additionally report, for each such selector, the expression's source text and whether it matched for the actual `--source`/`--target` pair being explained. The result is derived from the same violation objects the single contract-execution pass already produced — no separate re-evaluation of any selector. Each entry is attributed to the specific hop (source and target node IDs) where it occurred, so the same expression on multiple hops of the path produces distinct entries.

#### Scenario: Human explain output shows expression participation
- **WHEN** `arch-linter-net explain --source A --target B` resolves a path where a hop's contract has a `when`-bearing forbidden selector
- **THEN** the human-readable output includes the hop node IDs, the expression's source text, and whether it matched for that hop

#### Scenario: JSON explain output shows expression participation
- **WHEN** the same explain request is made with `--format json`
- **THEN** the JSON output includes an `expressionParticipation` array with entries containing the contract ID, hop source and target node IDs, expression source text, YAML location, and match result

#### Scenario: Explain without CEL involvement is unaffected
- **WHEN** the resolved path involves no `when`-bearing selector on any hop
- **THEN** the explain output (human or JSON) is identical to the output produced before this change

#### Scenario: No-path result has no expression participation
- **WHEN** no path exists between `--source` and `--target`
- **THEN** the "no dependency path found" result is unchanged and includes no expression participation data

### Requirement: `explain` reports authored source-set expansion
The `explain` command SHALL report, in both human and JSON output, each expanded contract's authored contract id, the set that produced each instance, the concrete resolved source, and the authored policy location, so an author can see why a contract applies to a given source.

#### Scenario: Explain shows set, source, and fragment
- **WHEN** `explain` runs against a policy whose contracts were expanded from a named source set
- **THEN** the output names the authored contract, the set, each concrete resolved source, and the authored policy fragment location

### Requirement: Explain reports effective subtractive scope
The explain command SHALL report effective compatible selector scope with authored and imported provenance without requiring clients to parse display prose. For runtime type-placement and layout-convention selectors, JSON SHALL include typed inclusion/exclusion participation with matched, stale-exclusion, and evaluation-failed states; human output SHALL render the same state.

#### Scenario: Explain identifies an exclusion
- **WHEN** an effective scope excludes a resolved item
- **THEN** explain output SHALL identify the exclusion item and its provenance

#### Scenario: Explain shows runtime stale exclusion
- **WHEN** a type-placement or layout-convention exclusion matches no candidate accepted by its inclusion matcher
- **THEN** explain output SHALL identify that exclusion as stale alongside the positive matcher participation

### Requirement: Explain JSON errors are authoritative

When `explain --format json` terminates on an owned configuration, policy, or build-state failure, it SHALL retain its existing single structured JSON document on stdout. Its exit code and human-format output behavior SHALL remain unchanged.

#### Scenario: Explain policy failure is parseable JSON
- **WHEN** `explain --format json` loads an invalid policy
- **THEN** stdout parses as one JSON error document

### Requirement: Explain projects deferred source-set provenance
The `explain` command SHALL report authored contract, source-set, source-set reference, selector,
and resolved project provenance for project metadata contracts whose project sets were bound from
solution discovery.

#### Scenario: Explain reports an imported project selector
- **WHEN** an imported policy fragment contributes a solution-derived project set to a metadata
  contract
- **THEN** explain identifies the fragment location and the resolved project path for the selector

