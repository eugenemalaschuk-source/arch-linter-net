## ADDED Requirements

### Requirement: Layer namespace selection supports subtractive exclusion
A layer declared with `namespace` and/or `namespace_suffix` MAY also declare `exclude`, a list of entries using the same `namespace`/`namespace_suffix` glob shape. A namespace SHALL be considered part of the layer's scope only if it matches the layer's inclusion glob(s) AND matches none of the layer's `exclude` entries.

#### Scenario: Namespace excluded from an otherwise matching layer
- **GIVEN** a layer with `namespace: Product.Modules.*` and `exclude: [{ namespace: Product.Modules.*.Infrastructure }]`
- **AND** the namespace `Product.Modules.Weather.Infrastructure`
- **WHEN** layer resolution runs for that namespace
- **THEN** the namespace does not match the layer

#### Scenario: Namespace outside any exclude entry still matches
- **GIVEN** a layer with `namespace: Product.Modules.*` and `exclude: [{ namespace: Product.Modules.*.Infrastructure }]`
- **AND** the namespace `Product.Modules.Weather.Domain`
- **WHEN** layer resolution runs for that namespace
- **THEN** the namespace matches the layer

#### Scenario: Layer without an exclude list is unchanged
- **GIVEN** a layer declared with only `namespace` and no `exclude` key
- **WHEN** layer resolution runs
- **THEN** matching behaves exactly as it did before this capability existed

#### Scenario: Excluded namespace falls back to other declared layers
- **GIVEN** a broad layer `ModulesCore` with `namespace: Product.Modules.*` and `exclude: [{ namespace: Product.Modules.*.Infrastructure }]`
- **AND** a separate declared layer `Infrastructure` with `namespace: Product.Modules.*.Infrastructure`
- **WHEN** layer resolution runs for `Product.Modules.Weather.Infrastructure`
- **THEN** the namespace resolves to the `Infrastructure` layer, not `ModulesCore`

### Requirement: Contract families that reference layers by name inherit layer exclusion transitively
Dependency, allow-only, external-dependency, protected, cycle, and acyclic-sibling contracts SHALL observe a layer's excluded namespaces as not belonging to that layer, without requiring any exclusion configuration of their own.

#### Scenario: A dependency contract's forbidden-layer scope narrows with layer exclusion
- **GIVEN** a layer `ModulesCore` with `namespace: Product.Modules.*` and `exclude: [{ namespace: Product.Modules.*.Infrastructure }]`
- **AND** a dependency contract listing `ModulesCore` as a forbidden layer
- **WHEN** validation evaluates a type in `Product.Modules.Weather.Infrastructure`
- **THEN** that type is not treated as belonging to `ModulesCore` for the dependency contract

### Requirement: Layer exclusion entries are validated and reject unknown keys
`exclude` entries SHALL accept only `namespace` and `namespace_suffix`. A monolithic or composed policy declaring an unrecognized key inside a layer's `exclude` entry SHALL fail to load with an actionable error.

#### Scenario: Unknown key inside an exclude entry is rejected
- **GIVEN** a layer's `exclude` entry declaring an unsupported key such as `role`
- **WHEN** the policy document loads
- **THEN** loading fails with an error naming the offending layer and key

### Requirement: Unmatched layer exclusions are reportable as a policy-consistency finding
An `exclude` entry that matches no first-party namespace within its layer's included scope, across the analyzed namespace inventory, SHALL be reported as an `unmatched-layer-exclusion` policy-consistency finding, governed by `analysis.policy_consistency` like other policy-consistency checks.

#### Scenario: A typo'd exclude pattern is surfaced
- **GIVEN** a layer with `namespace: Product.Modules.*` and `exclude: [{ namespace: Product.Modules.*.Persistnce }]` (typo)
- **AND** the analyzed namespace inventory contains `Product.Modules.Weather.Persistence` but no namespace matching `Product.Modules.*.Persistnce`
- **WHEN** validation runs
- **THEN** an `unmatched-layer-exclusion` policy-consistency finding names the layer and the unmatched exclude pattern

#### Scenario: A matched exclude pattern produces no unmatched-exclusion diagnostic
- **GIVEN** a layer with `namespace: Product.Modules.*` and `exclude: [{ namespace: Product.Modules.*.Persistence }]`
- **AND** the analyzed namespace inventory contains `Product.Modules.Weather.Persistence`
- **WHEN** validation runs
- **THEN** no `unmatched-layer-exclusion` finding is reported for that entry

### Requirement: Layer descriptions used across diagnostics name exclude entries as searchable text
The shared layer-description text used to render a layer's scope in violation messages, policy-consistency findings, and coverage evidence (human, JSON, and SARIF output) SHALL name the layer's `exclude` entries alongside its include pattern, so exclusion participation is visible without color or diagrams.

#### Scenario: Layer description names its exclude entries
- **GIVEN** a layer `ModulesCore` with `namespace: Product.Modules.*` and `exclude: [{ namespace: Product.Modules.*.Infrastructure }, { namespace: Product.Modules.*.Persistence }]`
- **WHEN** the layer's description is rendered (e.g. in a violation reason or coverage evidence entry)
- **THEN** the rendered text includes both the `Product.Modules.*` include pattern and the excluded `Product.Modules.*.Infrastructure`/`Product.Modules.*.Persistence` patterns

#### Scenario: A layer without exclude entries renders unchanged
- **GIVEN** a layer with only `namespace` and no `exclude` key
- **WHEN** the layer's description is rendered
- **THEN** the rendered text is identical to its pre-exclusion form
