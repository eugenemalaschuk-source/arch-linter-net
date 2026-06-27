## ADDED Requirements

### Requirement: Namespace coverage contracts classify first-party namespaces under declared roots
The system SHALL execute `strict_coverage` and `audit_coverage` contracts with `scope: namespace` by classifying first-party namespaces discovered in the shared `ArchitectureCoverageInventory` under the contract's `roots`.

#### Scenario: An uncovered namespace under a root produces a coverage finding
- **GIVEN** a namespace coverage contract whose `roots` include `MyApp.Features`
- **AND** the inventory contains `MyApp.Features.Audio`
- **AND** `MyApp.Features.Audio` matches no declared layer, no layer glob, no expanded layer-template layer, and no coverage exclusion
- **WHEN** validation runs for that contract's mode
- **THEN** the system reports `MyApp.Features.Audio` as uncovered coverage

### Requirement: Declared layers, namespace globs, and expanded layer templates provide namespace coverage
Namespace coverage classification SHALL treat declared layers, declared namespace-glob layers, and expanded layer-template layers as coverage providers for namespaces under the contract roots.

#### Scenario: Glob and template coverage prevent duplicate blind-spot findings
- **GIVEN** a namespace coverage contract rooted at `MyApp.Features`
- **AND** a declared layer with `namespace: MyApp.Features.*`
- **AND** an expanded layer-template layer `MyApp.Features.Billing.Contracts`
- **WHEN** the inventory contains namespaces matched by either provider
- **THEN** those namespaces are classified as covered and do not produce uncovered findings

### Requirement: Namespace coverage exclusions suppress findings only for explicitly excluded namespaces
Namespace coverage contracts SHALL honor explicit `exclude` entries that use the existing namespace matcher shape plus a required reason.

#### Scenario: Generated or test-only namespaces can be excluded
- **GIVEN** a namespace coverage contract rooted at `MyApp.Features`
- **AND** it excludes `MyApp.Features.*` with `namespace_suffix: Generated` for a documented reason
- **WHEN** the inventory contains `MyApp.Features.Audio.Generated`
- **THEN** that namespace does not produce an uncovered finding
- **AND** unrelated uncovered namespaces under the same root still produce findings

### Requirement: Namespace coverage findings are deterministic and actionable
Namespace coverage findings SHALL be sorted by uncovered namespace using ordinal comparison and SHALL include the uncovered namespace, a representative source type from that namespace, and the contract name/ID context.

#### Scenario: Repeated runs emit the same namespace coverage findings
- **WHEN** the same target assemblies are validated twice without code changes
- **THEN** namespace coverage findings are reported in the same order with the same representative type evidence

### Requirement: Namespace coverage severity follows analysis.coverage without affecting non-coverage policies
Namespace coverage findings SHALL respect `analysis.coverage` as `error`, `warn`, or `off`, while policies that declare no coverage contracts remain behaviorally unchanged.

#### Scenario: Warn severity reports without failing validation
- **GIVEN** a policy with `analysis.coverage: warn`
- **AND** a namespace coverage contract finds an uncovered namespace
- **WHEN** validation runs
- **THEN** the uncovered namespace is still reported
- **AND** validation does not fail solely because of that coverage finding

#### Scenario: Policy without namespace coverage contracts is unchanged
- **GIVEN** a policy that declares no `strict_coverage` or `audit_coverage` contracts
- **WHEN** validation runs
- **THEN** the presence of namespace coverage support does not change the result

### Requirement: Unsupported coverage scopes are rejected until their implementations land
Until the project, assembly, dependency-edge, and rule-input coverage families are implemented, the system SHALL reject those declared scopes with an actionable error instead of silently accepting them.

#### Scenario: A project coverage contract is still reserved
- **WHEN** a policy declares a coverage contract with `scope: project`
- **THEN** validation fails with an error explaining that only `scope: namespace` coverage is implemented
