## ADDED Requirements

### Requirement: Layer namespace accepts constrained glob patterns

The `namespace` field in `ArchitectureLayer` SHALL accept a constrained glob pattern in addition to literal namespace prefixes.

#### Scenario: Exact literal namespace still works
- **WHEN** a layer defines `namespace: FirstIce.Game.Features`
- **THEN** it matches `FirstIce.Game.Features` and child namespaces `FirstIce.Game.Features.*` (unchanged behavior)

#### Scenario: Single-segment wildcard matches one segment
- **WHEN** a layer defines `namespace: FirstIce.Game.Features.*`
- **THEN** it matches `FirstIce.Game.Features.Audio`, `FirstIce.Game.Features.Fishing`, and their descendants (e.g. `FirstIce.Game.Features.Audio.Player`)

#### Scenario: Wildcard must be a complete segment
- **WHEN** a layer defines `namespace: FirstIce.Game.Features.Foo*`
- **THEN** the pattern SHALL be rejected as invalid
- **WHEN** a layer defines `namespace: FirstIce.Game.*.Features`
- **THEN** it is valid (`*` is a complete segment)

#### Scenario: Bare wildcard rejected
- **WHEN** a layer defines `namespace: *`
- **THEN** the pattern SHALL be rejected as invalid (too broad, no meaningful constraint)

#### Scenario: `**`, `?`, partial segment globs, and character classes rejected
- **WHEN** a layer defines `namespace` containing `**`, `?`, `[`, `]`, or regex-like syntax
- **THEN** the pattern SHALL be rejected with a clear configuration error

### Requirement: Glob semantics are segment-based

Glob matching SHALL operate on namespace segments (split by `.`), not on raw regex.

#### Scenario: Prefix descendants allowed after resolved wildcard
- **WHEN** a layer defines `namespace: FirstIce.Game.Features.*`
- **AND** the namespace to match is `FirstIce.Game.Features.Audio.Player`
- **THEN** `*` consumes segment `Audio`
- **AND** the remaining segment `Player` is treated as a descendant of the resolved prefix `FirstIce.Game.Features.Audio` — match succeeds

#### Scenario: Wildcard requires at least one segment
- **WHEN** a layer defines `namespace: FirstIce.Game.Features.*`
- **AND** the namespace to match is `FirstIce.Game.Features`
- **THEN** match fails (`*` needs exactly one segment, but zero remain)

#### Scenario: Multiple wildcards match multiple segments
- **WHEN** a layer defines `namespace: A.*.B.*`
- **AND** the namespace to match is `A.Foo.B.Bar.Baz`
- **THEN** `*` consumes `Foo`, second `*` consumes `Bar`, remaining `Baz` is descendant — match succeeds

#### Scenario: Leading wildcard not permitted at top level
- **WHEN** a layer defines `namespace: *.Features`
- **THEN** the pattern SHALL be rejected as invalid (bare `*` at a segment level is not permitted; `*` must be preceded by at least one literal segment)

### Requirement: `namespace_suffix` composes with glob patterns

When `namespace` contains a glob pattern AND `namespace_suffix` is set, the suffix position is fixed immediately after the full namespace pattern.

#### Scenario: Glob + suffix fixed-position match
- **WHEN** a layer defines `namespace: FirstIce.Game.Features.*` and `namespace_suffix: Models`
- **AND** the namespace to match is `FirstIce.Game.Features.Audio.Models`
- **THEN** `*` consumes `Audio`, the full pattern resolves to `FirstIce.Game.Features.Audio`, and suffix matches `Models` on the next segment — match succeeds

#### Scenario: Glob + suffix descendant allowed
- **WHEN** a layer defines `namespace: FirstIce.Game.Features.*` and `namespace_suffix: Models`
- **AND** the namespace to match is `FirstIce.Game.Features.Audio.Models.Dto`
- **THEN** match succeeds (suffix matched, `Dto` is descendant)

#### Scenario: Glob + multi-segment suffix match
- **WHEN** a layer defines `namespace: FirstIce.Game.Features.*` and `namespace_suffix: Api.Contracts`
- **AND** the namespace to match is `FirstIce.Game.Features.Audio.Api.Contracts.Dto`
- **THEN** match succeeds and the resolved concrete prefix is `FirstIce.Game.Features.Audio.Api.Contracts`

#### Scenario: Glob with middle wildcard and suffix match
- **WHEN** a layer defines `namespace: FirstIce.Game.*.Features` and `namespace_suffix: Contracts`
- **AND** the namespace to match is `FirstIce.Game.Audio.Features.Contracts.Dto`
- **THEN** match succeeds and the resolved concrete prefix is `FirstIce.Game.Audio.Features.Contracts`

#### Scenario: Glob + suffix wrong position fails
- **WHEN** a layer defines `namespace: FirstIce.Game.Features.*` and `namespace_suffix: Models`
- **AND** the namespace to match is `FirstIce.Game.Features.Audio.Internal.Models`
- **THEN** match fails (suffix must be immediately after the full resolved namespace pattern, but `Internal` occupies that position)

#### Scenario: Literal namespace + suffix retains old behavior
- **WHEN** a layer defines `namespace: FirstIce.Game.Features` and `namespace_suffix: Models` (no glob)
- **AND** the namespace to match is `FirstIce.Game.Features.Audio.Internal.Models`
- **THEN** match succeeds (existing `EndsWith(".Models")` behavior — no breaking change)

### Requirement: Deterministic layer resolution ranking

When multiple layers match a type namespace, `ResolveContainingLayer` SHALL use deterministic specificity ranking, not string length.

#### Scenario: Literal beats glob
- **WHEN** layers `audio` (`namespace: FirstIce.Game.Features.Audio`) and `features` (`namespace: FirstIce.Game.Features.*`) both match a type
- **THEN** `audio` wins (literal prefix beats glob pattern)

#### Scenario: More literal segments beat fewer
- **WHEN** `a` (`namespace: FirstIce.Game.*`) and `b` (`namespace: FirstIce.Game.Features.*`) both match
- **THEN** `b` wins (more literal segments)

#### Scenario: Fewer wildcards beat more
- **WHEN** both patterns are globs with equal literal prefix count, fewer `*` wins

#### Scenario: Stable tiebreaker
- **WHEN** two patterns have identical specificity
- **THEN** layer name ordinal comparison decides (stable, input-order-independent)

### Requirement: Invalid glob patterns produce configuration errors

The system SHALL validate all `namespace` fields at policy load time and reject invalid glob patterns.

#### Scenario: Invalid patterns rejected
- **WHEN** a pattern contains `**`, `?`, `[`, `]`, partial segment `*` (e.g. `Foo*`, `*Bar`), empty segments (e.g. `A..B`), leading/trailing dots, or is bare `*`
- **THEN** a configuration error is produced with a clear message identifying the pattern and the reason

### Requirement: Diagnostics show pattern and concrete match

Violation diagnostics SHALL include both the configured glob pattern and the concrete namespace prefix that was resolved during matching.

#### Scenario: Violation message includes pattern
- **WHEN** a type in `FirstIce.Game.Features.Audio.Player` violates a contract referring to a layer with `namespace: FirstIce.Game.Features.*`
- **THEN** the diagnostic SHALL identify the pattern `FirstIce.Game.Features.*` and the resolved prefix `FirstIce.Game.Features.Audio`

#### Scenario: Literal namespace diagnostics unchanged
- **WHEN** a layer uses an exact literal namespace (no glob)
- **THEN** diagnostics match current behavior (no extra info needed)

### Requirement: JSON schema, docs, examples updated

The schema, documentation, samples, and AI-facing guidance SHALL reflect the supported glob syntax.

#### Scenario: JSON schema documents glob support
- **WHEN** the `namespace` property description in the schema is read
- **THEN** it SHALL state that glob patterns (`*` for one segment) are supported alongside literal prefixes

#### Scenario: Samples use glob patterns
- **WHEN** a sample policy document is read
- **THEN** it SHALL demonstrate at least one glob-based layer

#### Scenario: AI guidance documents glob constraints
- **WHEN** an AI agent reads the policy-authoring guidance
- **THEN** it SHALL understand that only `*` as a complete segment is supported, `**` and partial globs are rejected

### Requirement: Backward compatibility for literal namespace definitions

All existing layer definitions using literal namespaces SHALL continue to work without modification.

#### Scenario: Existing literal layers unchanged
- **WHEN** any acceptance test or policy file uses exact namespace prefixes
- **THEN** no change in matching behavior
