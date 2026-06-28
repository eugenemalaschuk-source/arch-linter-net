## MODIFIED Requirements

### Requirement: Coverage summary computation
For every coverage contract that is selected and run, the system SHALL compute a deterministic coverage summary containing counts for `covered`, `excluded`, `uncovered`, `stale`, and `unknown`, derived from the existing coverage inventory and coverage findings without altering coverage validation behavior.

#### Scenario: Namespace scope summary counts
- **WHEN** a `scope: namespace` coverage contract runs against a repository with namespaces matching a declared layer, namespaces matching an `exclude` rule, and namespaces matching neither
- **THEN** the summary reports the matched-layer namespaces under `covered`, the excluded namespaces under `excluded`, and the unmatched namespaces under `uncovered`, with `stale` and `unknown` at zero

#### Scenario: Rule-input scope summary counts
- **WHEN** a `scope: rule_input` coverage contract runs with one referenced contract whose layer no longer matches any code and one referenced contract whose layer name does not exist
- **THEN** the summary reports the first under `stale` and the second under `unknown`, with `covered`, `excluded`, and `uncovered` reflecting any remaining referenced contracts that resolve cleanly or are excluded

#### Scenario: Project scope summary counts
- **WHEN** a `scope: project` coverage contract runs against a repository with discovered projects matching a declared layer, projects matching an `exclude` rule, projects matching neither, and a project whose assembly could not be resolved
- **THEN** the summary reports the matched-layer projects under `covered`, the excluded projects under `excluded`, the unmatched projects under `uncovered`, and the unresolved project under `unknown`, with `stale` at zero

#### Scenario: Assembly scope summary counts
- **WHEN** a `scope: assembly` coverage contract runs against a repository with resolved assemblies matching a declared layer, assemblies matching an `exclude` rule, and assemblies matching neither
- **THEN** the summary reports the matched-layer assemblies under `covered`, the excluded assemblies under `excluded`, and the unmatched assemblies under `uncovered`, with `stale` and `unknown` at zero

#### Scenario: Empty repository
- **WHEN** a coverage contract runs against a repository with no namespaces, no discovered projects/assemblies, or no referenced rule contracts in scope
- **THEN** the summary reports all counts as zero for that contract, with no error

#### Scenario: Fully covered repository
- **WHEN** every namespace under a `scope: namespace` contract's roots, or every discovered project under a `scope: project` contract, or every resolved assembly under a `scope: assembly` contract, matches a declared layer, namespace-glob layer, or expanded layer template
- **THEN** `uncovered`, `stale`, and `unknown` are zero and `covered` equals the total in-scope unit count

#### Scenario: Reserved scopes are not summarized
- **WHEN** a policy is loaded
- **THEN** the summary never contains entries for `scope: dependency_edge`, because the loader rejects that scope before any contract instance exists to summarize
