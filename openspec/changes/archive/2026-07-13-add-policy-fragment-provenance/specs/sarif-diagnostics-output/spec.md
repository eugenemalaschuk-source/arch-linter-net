## ADDED Requirements

### Requirement: SARIF results expose policy definition origins as related locations
When a diagnostic has policy-origin metadata, the SARIF formatter SHALL add the primary
and related policy definitions to the result's `relatedLocations` using portable
artifact URIs and YAML-path messages. Policy related locations SHALL be ordered by
composed source ordinal and YAML path and SHALL NOT replace existing physical source
locations or logical code locations.

#### Scenario: Fragment contract violation has a related location
- **WHEN** a contract loaded from `architecture/policy/domain.yml` produces a violation
- **THEN** the SARIF result keeps its existing code location and adds a related location whose artifact URI is `architecture/policy/domain.yml` and whose message identifies the contract YAML path

#### Scenario: Conflict carries two policy locations
- **WHEN** a machine-readable policy diagnostic represents a root-versus-fragment conflict
- **THEN** its related locations identify both definitions in deterministic original-then-conflicting order

#### Scenario: SARIF remains stable across machines
- **WHEN** equivalent imported-policy validation runs on different operating systems
- **THEN** SARIF policy artifact URIs use the same repository-relative `/`-separated paths and contain no machine-specific absolute path
