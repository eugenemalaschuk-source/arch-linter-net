## ADDED Requirements

### Requirement: Cache lookup validates a current-run preparation plan
The system SHALL independently determine root outputs, probing inputs, metadata reference closure, PE/PDB/receipt identities, project manifests, and policy/baseline identities before consulting a cache entry. A cache entry SHALL only validate against that plan and SHALL NOT select analyzed artifacts.

#### Scenario: Corrupt entry cannot replace selected artifacts
- **WHEN** a cache entry names artifact paths different from the current prepared plan
- **THEN** the entry is rejected and no artifact choice is read from it

### Requirement: Materialization verifies prepared artifact bytes
The system SHALL load only bytes whose digest matches the preparation plan. A changed artifact SHALL cause a typed cache rejection or preparation restart and SHALL never be published under stale authorization.

#### Scenario: Artifact changes between lookup and load
- **WHEN** a planned PE, PDB, or receipt identity changes after a cache miss
- **THEN** materialization does not load or publish it under the original authorization

### Requirement: Cache profiles expose avoided work on a verified hit
The cache profile SHALL report avoided assembly loads, fact-index materializations, source-scan passes, and contract executions, plus avoided artifact bytes when known. A verified warm hit SHALL report positive avoided assembly and fact or contract work and zero performed assembly loads.

#### Scenario: Warm hit reports avoided work
- **WHEN** a populated entry is reused for a contract-bearing workload
- **THEN** `Hits` equals one, `AssemblyLoads` equals zero, and avoided assembly plus fact-or-contract work are positive
