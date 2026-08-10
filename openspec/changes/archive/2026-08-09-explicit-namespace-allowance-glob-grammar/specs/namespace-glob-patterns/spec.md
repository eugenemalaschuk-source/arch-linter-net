## ADDED Requirements

### Requirement: Namespace-allowance contract fields accept the same constrained glob grammar

`allowed_only_in_namespaces` (composition, attribute-usage, interface-implementation contracts), `forbidden_in_namespaces` (attribute-usage, interface-implementation contracts), and `must_reside_in_namespaces` (type-placement contracts) SHALL be matched using the same constrained, segment-based glob grammar as `layers.<name>.namespace`, instead of literal-prefix-only matching.

#### Scenario: Literal entries keep current behavior
- **WHEN** a contract declares `allowed_only_in_namespaces: [Example.Domain]` with no `*`
- **THEN** it matches `Example.Domain` and descendant namespaces `Example.Domain.*` exactly as before this change

#### Scenario: Single-segment wildcard matches deterministically
- **WHEN** a contract declares `allowed_only_in_namespaces: [Example.Modules.*.Composition]`
- **THEN** it matches `Example.Modules.Orders.Composition` and its descendants (e.g. `Example.Modules.Orders.Composition.Startup`), consuming exactly one segment for `*`
- **AND** it does not match `Example.Modules.Composition` (zero segments for `*`) or `Example.Modules.Orders.Sub.Composition` (two segments for `*`)

#### Scenario: forbidden_in_namespaces gains the same grammar
- **WHEN** an attribute-usage or interface-implementation contract declares `forbidden_in_namespaces: [Example.*.Legacy]`
- **THEN** matching follows the same segment-based glob semantics as `allowed_only_in_namespaces`

#### Scenario: must_reside_in_namespaces gains the same grammar
- **WHEN** a type-placement contract declares `must_reside_in_namespaces: [Example.Modules.*.Contracts]`
- **THEN** matching follows the same segment-based glob semantics as `allowed_only_in_namespaces`

### Requirement: Unsupported wildcard patterns in namespace-allowance fields are rejected at policy load

The system SHALL validate every entry of `allowed_only_in_namespaces`, `forbidden_in_namespaces`, and `must_reside_in_namespaces` at policy load time and reject unsupported wildcard syntax with an actionable configuration error, instead of silently loading a pattern that then matches nothing at scan time.

#### Scenario: Bare, leading, partial-segment, or `**`/`?`/`[]` patterns rejected
- **WHEN** a contract declares an `allowed_only_in_namespaces`, `forbidden_in_namespaces`, or `must_reside_in_namespaces` entry containing `**`, `?`, `[`, `]`, a partial-segment wildcard (e.g. `Foo*`, `*Bar`), a bare `*`, a leading `*.Namespace`, or an empty segment (e.g. `A..B`)
- **THEN** policy load SHALL fail with a configuration error identifying the contract, the field, the exact pattern, and the specific grammar rule violated

#### Scenario: Diagnostic reuses the same grammar-rule wording as layer namespaces
- **WHEN** a namespace-allowance field entry is rejected
- **THEN** the error message SHALL name the same violated rule that `layers.<name>.namespace` validation would report for the identical pattern (e.g. "Bare wildcard '*' is not allowed", "Partial segment wildcard '...' is not allowed")

### Requirement: Docs and AI guidance document the shared grammar for namespace-allowance fields

The contract documentation and AI-facing policy-authoring guidance SHALL state that `allowed_only_in_namespaces`, `forbidden_in_namespaces`, and `must_reside_in_namespaces` support the same constrained glob grammar as `layers.<name>.namespace`.

#### Scenario: Contract docs mention glob support
- **WHEN** the composition, attribute-usage, interface-implementation, or type-placement contract documentation describes its namespace-allowance field(s)
- **THEN** it SHALL state that a single `*` segment is supported and link to (or restate) the same constraints as the layer namespace glob grammar

#### Scenario: AI policy-authoring guidance covers the shared grammar
- **WHEN** an AI agent reads the policy-authoring guidance for namespace-allowance fields
- **THEN** it SHALL understand that only `*` as a complete segment is supported there too, and that unsupported syntax fails policy load rather than silently no-matching
