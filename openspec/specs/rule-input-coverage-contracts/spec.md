# rule-input-coverage-contracts Specification

## Purpose
TBD - created by archiving change add-rule-input-coverage-checks. Update Purpose after archive.
## Requirements
### Requirement: Rule-input coverage contracts classify referenced contracts' layer inputs
The system SHALL execute `strict_coverage` and `audit_coverage` contracts with `scope: rule_input` by resolving each entry in the contract's `contract_ids` to the referenced contract's layer-bearing input fields and classifying those fields against the shared `ArchitectureCoverageInventory`.

#### Scenario: A referenced contract whose inputs resolve to real code produces no finding
- **GIVEN** a rule-input coverage contract with `contract_ids: [my-dependency-rule]`
- **AND** `my-dependency-rule` is a dependency contract whose `source` and `forbidden` layers both match first-party namespaces present in the inventory
- **WHEN** validation runs for that contract's mode
- **THEN** the system reports no coverage finding for `my-dependency-rule`

### Requirement: A dangling layer reference is classified as unresolved
A rule-input coverage contract SHALL report `unresolved` for any referenced contract's layer-bearing input field whose value does not match a name declared under `layers`.

#### Scenario: A typo'd layer name is reported as unresolved
- **GIVEN** a rule-input coverage contract with `contract_ids: [my-dependency-rule]`
- **AND** `my-dependency-rule`'s `source` field names a layer that is not declared under `layers`
- **WHEN** validation runs for that contract's mode
- **THEN** the system reports an `unresolved` coverage finding identifying `my-dependency-rule` and the dangling layer name

### Requirement: A layer pattern matching no first-party code is classified as empty-input
A rule-input coverage contract SHALL report `empty-input` for any referenced contract's layer-bearing input field whose declared layer name resolves but whose namespace pattern currently matches zero namespaces in the `ArchitectureCoverageInventory`.

#### Scenario: A layer with zero matching namespaces is reported as empty-input
- **GIVEN** a rule-input coverage contract with `contract_ids: [my-dependency-rule]`
- **AND** `my-dependency-rule`'s `forbidden` field names a declared layer whose namespace pattern matches no namespace in the inventory
- **WHEN** validation runs for that contract's mode
- **THEN** the system reports an `empty-input` coverage finding identifying `my-dependency-rule` and the empty layer

#### Scenario: A layer whose namespace was removed after authoring is still detected by current-state resolution
- **GIVEN** a rule-input coverage contract referencing a contract whose layer pattern matched code when the policy was authored
- **AND** the matched namespace has since been deleted or renamed so the pattern now matches zero namespaces
- **WHEN** validation runs for that contract's mode
- **THEN** the system reports the same `empty-input` finding as it would for a layer that never matched any code

### Requirement: Rule-input coverage exclusions require a contract reference and a reason
Rule-input coverage contracts SHALL support `exclude` entries identifying a specific `contract_id`, and SHALL require a non-empty `reason` on every such entry, suppressing both `unresolved` and `empty-input` findings for that referenced contract.

#### Scenario: An intentionally empty rule is excluded with a reason
- **GIVEN** a rule-input coverage contract with `contract_ids: [legacy-rule]`
- **AND** an `exclude` entry with `contract_id: legacy-rule` and a non-empty `reason`
- **AND** `legacy-rule`'s layer pattern currently matches zero namespaces
- **WHEN** validation runs for that contract's mode
- **THEN** the system reports no coverage finding for `legacy-rule`

#### Scenario: An exclusion without a reason is rejected
- **GIVEN** a rule-input coverage contract whose `exclude` entry declares `contract_id` without `reason`
- **WHEN** the policy is loaded
- **THEN** the system rejects the policy with an actionable error identifying the missing reason

### Requirement: Rule-input coverage contracts validate contract_ids at load time
The system SHALL reject, at policy load time, a `scope: rule_input` coverage contract that declares an empty `contract_ids` list, declares `roots` or `between`, or references a `contract_ids` entry that does not resolve to any declared contract ID.

#### Scenario: An empty contract_ids list is rejected
- **GIVEN** a coverage contract with `scope: rule_input` and an empty `contract_ids` list
- **WHEN** the policy is loaded
- **THEN** the system rejects the policy with an actionable error

#### Scenario: A dangling contract_ids entry is rejected at load time
- **GIVEN** a coverage contract with `scope: rule_input` and `contract_ids: [does-not-exist]`
- **AND** no contract in the policy declares the ID `does-not-exist`
- **WHEN** the policy is loaded
- **THEN** the system rejects the policy with an actionable error naming the unresolved ID

#### Scenario: Roots or between on a rule_input contract are rejected
- **GIVEN** a coverage contract with `scope: rule_input` that also declares `roots` or `between`
- **WHEN** the policy is loaded
- **THEN** the system rejects the policy with an actionable error identifying the invalid field for that scope

### Requirement: Rule-input coverage severity follows the existing coverage severity model
Rule-input coverage findings SHALL participate in `analysis.coverage`'s `error`/`warn`/`off` severity exactly as namespace-scope coverage findings already do, and SHALL distinguish `strict_coverage` from `audit_coverage` groups identically.

#### Scenario: A strict rule-input coverage contract fails validation by default
- **GIVEN** a `strict_coverage` contract with `scope: rule_input` that produces an `empty-input` finding
- **AND** `analysis.coverage` is unset
- **WHEN** validation runs in strict mode
- **THEN** validation fails and the finding is reported

#### Scenario: An audit rule-input coverage contract reports without failing
- **GIVEN** an `audit_coverage` contract with `scope: rule_input` that produces an `unresolved` finding
- **AND** `analysis.coverage` is set to `warn`
- **WHEN** validation runs in audit mode
- **THEN** validation passes and the finding is reported

