## ADDED Requirements

### Requirement: Layer selector predicates evaluate an optional `when` expression

A `layers.<name>.selector` MAY declare `when`, compiled under the typed `subject` context defined by `cel-policy-model`, and when present it SHALL be evaluated as an additional AND-ed condition alongside the selector's literal `role`/`metadata` constraints: a type matches only if both the literal constraints match and the compiled `when` evaluates to `true` against that type's typed subject facts. A selector with no `when` SHALL never construct a CEL context and SHALL behave exactly as before this requirement.

A `when` evaluation failure (as opposed to a well-typed `false` result) SHALL be reported as a blocking policy/configuration error for the run, identical in severity to a selector compilation error. It SHALL NOT be treated as a non-match and SHALL NOT be suppressed by baseline.

#### Scenario: `when` refines a role-based selector match

- **WHEN** a layer declares `selector: { role: DomainLayer, when: "subject.metadataText[\"domain\"] == \"Sales\"" }`
- **THEN** a type belongs to the layer only if its resolved role is
  `DomainLayer` and the compiled predicate evaluates to `true` for that type

#### Scenario: Well-typed false is an ordinary non-match

- **WHEN** a selector's `when` evaluates to `false` for a type that otherwise
  matches the selector's literal `role`/`metadata`
- **THEN** the type does not belong to that layer and no expression error is
  reported

#### Scenario: Evaluation failure blocks the run

- **WHEN** a layer selector's `when` evaluation fails (e.g. a referenced
  metadata key is absent) for some classified type
- **THEN** the run fails with a reported expression evaluation error, not a
  silent non-match

#### Scenario: Selector without `when` is unaffected

- **WHEN** a layer selector declares only `role`/`metadata` with no `when`
- **THEN** matching behaves identically to its pre-#164 behavior, with no CEL
  context constructed for that selector

## MODIFIED Requirements

### Requirement: Classification vocabulary is defined

The semantic classification model SHALL define exactly ten classification terms with non-overlapping meanings: `role`, `metadata`, `source`, `evidence`, `confidence`/`precedence`, `conflict`, `override`, `exclusion`, `stale selector`, and `uncovered semantic fact`.

#### Scenario: Uncovered semantic fact is distinguished from an unclassified type
- **WHEN** a type receives a role/metadata assignment from some classification source
- **AND** no coverage-participating construct consumes that role/metadata and no `exclusion` names the type
- **THEN** the model classifies this as an `uncovered semantic fact`, a status distinct from a type that received no role at all

#### Scenario: An override-assigned role does not by itself exempt a type from coverage
- **WHEN** a type's role/metadata was assigned by a `classification.overrides` entry
- **AND** no coverage-participating construct consumes that role/metadata and no `exclusion` names the type
- **THEN** the model SHALL still classify this as an `uncovered semantic fact` — `override` is a classification source, not a coverage-exemption mechanism, and only `exclusion` removes a type from coverage consideration

#### Scenario: Contextual dependency and allow-only contracts register executable coverage consumption
- **WHEN** a contextual dependency or allow-only contract declares a source, target, or exclusion selector
- **THEN** the model records the selector's complete role, metadata values, operators, `when` expression (when declared), and source-target relationship as coverage-participating consumption
- **AND** semantic coverage evaluates that record with the same matching semantics as the contextual contract

#### Scenario: Stale selector is distinguished from an unmatched namespace layer
- **WHEN** a `layers.<name>.selector`'s `role`/`metadata` criteria — combined with its `when` expression when one is declared — match zero types classified by the model
- **THEN** the model classifies this as a `stale selector`, a status distinct from any existing namespace-layer diagnostic

### Requirement: Layer selector diagnostics are deterministic and explainable

The system SHALL reject invalid selector definitions with deterministic configuration diagnostics, and SHALL expose a deterministic empty-match diagnostic for a valid selector that matches no classified type (accounting for its `when` expression when one is declared) unless the layer is external. Layer descriptions and relevant diagnostics SHALL identify semantic selection when a selector participates, and SHALL identify the `when` expression's source text and YAML location when one contributed to or blocked a match.

#### Scenario: Selector without a role is rejected
- **WHEN** a layer declares `selector` without a non-empty `role`
- **THEN** policy validation rejects the document with a selector configuration diagnostic

#### Scenario: Empty selector match is visible
- **WHEN** a non-external selector-backed layer matches no loaded type, whether because no type matches its literal `role`/`metadata` or because its `when` expression evaluates `false` for every literal-matching type
- **THEN** configuration or coverage diagnostics report that the semantic selector matched no types

#### Scenario: External empty selector is suppressed consistently
- **WHEN** an external selector-backed layer matches no loaded type
- **THEN** the existing external-layer empty-layer suppression behavior is preserved

#### Scenario: Match diagnostics identify the matching mechanism
- **WHEN** a type is resolved into a selector-backed layer
- **THEN** layer descriptions or diagnostics can distinguish namespace matching, semantic selector matching (role/metadata), and `when` expression matching, including the `when` source text when one participated

#### Scenario: Expression evaluation failure is reported as a configuration diagnostic
- **WHEN** a selector-backed layer's `when` expression fails to evaluate for some classified type
- **THEN** the system reports a deterministic expression evaluation error diagnostic identifying the layer, the selector's YAML location, and the expression source text, rather than silently treating the type as unmatched
