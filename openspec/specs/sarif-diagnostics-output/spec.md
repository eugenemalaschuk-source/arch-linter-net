# sarif-diagnostics-output Specification

## Purpose
TBD - created by archiving change add-sarif-diagnostics-output. Update Purpose after archive.
## Requirements
### Requirement: SARIF envelope shape
The system SHALL render a valid SARIF 2.1.0 document with `version: "2.1.0"`, a `$schema` pointing at the SARIF 2.1.0 schema, and a single `runs` entry whose `tool.driver.name` identifies the CLI.

#### Scenario: Valid SARIF envelope
- **WHEN** the CLI is invoked with `--format sarif`
- **THEN** the output parses as JSON with `version == "2.1.0"`, a `$schema` field, and `runs` containing exactly one run with `tool.driver.name` set

### Requirement: Contract IDs become stable SARIF rule IDs
The SARIF formatter SHALL map each diagnostic's contract ID (falling back to the normalized contract name when the ID is absent) to a `rule.id` in `tool.driver.rules`, deduplicated across all results referencing the same contract.

#### Scenario: Rule ID from explicit contract ID
- **WHEN** a contract with `id: my-rule` produces a violation
- **THEN** the SARIF output contains a rule with `id == "my-rule"` and at least one result with `ruleId == "my-rule"`

#### Scenario: Rule ID from fallback when ID is absent
- **WHEN** a diagnostic has no `ContractId`
- **THEN** the SARIF formatter derives the rule ID by normalizing the contract name, using the same normalization the policy loader uses for fallback IDs

#### Scenario: Rules are deduplicated
- **WHEN** multiple diagnostics reference the same contract ID
- **THEN** `tool.driver.rules` contains exactly one rule entry for that ID

### Requirement: Severity reflects strict versus audit mode
The SARIF formatter SHALL set each result's `level` to `"error"` when the CLI is run with `--mode strict` and `"warning"` when run with `--mode audit`.

#### Scenario: Strict mode produces error level
- **WHEN** the CLI is invoked with `--mode strict --format sarif` and violations are found
- **THEN** every result in the SARIF output has `level == "error"`

#### Scenario: Audit mode produces warning level
- **WHEN** the CLI is invoked with `--mode audit --format sarif` and violations are found
- **THEN** every result in the SARIF output has `level == "warning"`

### Requirement: Method-body diagnostics include physical locations
The SARIF formatter SHALL populate `physicalLocation` (artifact URI and start line) for diagnostics produced by method-body scanning, parsing the file path and line number already carried on the diagnostic.

#### Scenario: Physical location for a method-body violation
- **WHEN** a method-body contract produces a violation with `SourceType` set to a repo-relative file path and a `ForbiddenReferences` entry shaped like `"line 42: pattern -> symbol"`
- **THEN** the corresponding SARIF result includes a `physicalLocation` with `artifactLocation.uri` equal to the file path and `region.startLine == 42`

#### Scenario: Unparseable reference still includes the file
- **WHEN** a method-body violation's reference entry does not match the expected `"line {N}: ..."` shape
- **THEN** the SARIF result still includes `artifactLocation.uri` for the file, without a `region`

### Requirement: Non-source diagnostics include logical locations
The SARIF formatter SHALL populate `logicalLocations` with a fully-qualified name for diagnostic kinds that identify a type, namespace, assembly, or package rather than a file position.

#### Scenario: Logical location for a namespace-level violation
- **WHEN** a layer or dependency contract produces a violation with no file location available
- **THEN** the corresponding SARIF result includes a `logicalLocations` entry with `fullyQualifiedName` equal to the diagnostic's source identifier

#### Scenario: Logical location for a cycle
- **WHEN** cycle detection produces a `CycleDiagnostic`
- **THEN** the corresponding SARIF result includes a `logicalLocations` entry with `fullyQualifiedName` equal to the cycle path

### Requirement: SARIF output is deterministically ordered
The SARIF formatter SHALL order `results` by contract ID, then source identifier, then forbidden namespace, and SHALL order `tool.driver.rules` alphabetically by rule ID.

#### Scenario: Stable ordering across repeated runs
- **WHEN** the same validation outcome is formatted as SARIF twice
- **THEN** both outputs are byte-identical

#### Scenario: Mixed violation kinds are ordered together
- **WHEN** the validation outcome contains a mix of dependency, external-dependency, and cycle diagnostics
- **THEN** the SARIF `results` array is sorted by `(ruleId, sourceIdentifier, category)` regardless of diagnostic kind

### Requirement: SARIF scope excludes policy-level diagnostics
The SARIF formatter SHALL render only violations and cycles as SARIF results. It SHALL NOT render coverage findings, unmatched-ignored-violations, or policy-consistency findings as SARIF results.

#### Scenario: Coverage findings are absent from SARIF
- **WHEN** the validation outcome includes coverage findings alongside violations
- **THEN** the SARIF `results` array contains entries only for the violations and cycles, not the coverage findings

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

### Requirement: SARIF results expose CEL expression provenance as related locations
When a diagnostic rendered as a SARIF result has CEL expression participation (per the `violation-reporting` capability's `when_expression` data), the SARIF formatter SHALL add a related location whose message includes the expression's source text and result, in addition to any existing physical/logical locations and policy-origin related locations. SARIF results for diagnostics without expression participation SHALL be unaffected.

#### Scenario: SARIF result includes expression-provenance related location
- **WHEN** a context-dependency violation with a `when`-bearing forbidden selector is rendered as SARIF
- **THEN** the result's `relatedLocations` includes an entry whose `message.text` states the evaluated expression's source text and that it matched

#### Scenario: Existing policy-origin related locations are preserved
- **WHEN** a diagnostic has both policy-origin related locations and CEL expression participation
- **THEN** both sets of related locations are present, and neither replaces the other

#### Scenario: Non-CEL SARIF results are unaffected
- **WHEN** a diagnostic has no `when`-bearing selector
- **THEN** its SARIF result is identical to the output produced before this change

