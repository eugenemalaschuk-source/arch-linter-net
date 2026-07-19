## ADDED Requirements

### Requirement: Violations expose CEL expression participation additively
When a context-dependency, context-allow-only, or layout-convention violation involves a selector that declares `when`, the human-readable and CI JSON output SHALL include the expression's source text, its YAML location, and its result (`matched`, `not_matched`, or `evaluation_failed`), in addition to all existing fields for that diagnostic kind. Diagnostics with no `when`-bearing selector SHALL omit this data entirely rather than emitting empty or null placeholders.

#### Scenario: JSON violation includes expression participation
- **WHEN** a context-dependency contract's `forbidden[*].when` predicate evaluates `true` and produces a violation
- **THEN** the JSON output for that violation includes a `when_expressions` array; each entry has the expression's source text, its YAML path, its `location` (e.g. `"forbidden"`), and `"result": "matched"`

#### Scenario: Human output includes the expression text
- **WHEN** the same violation is rendered as human-readable output
- **THEN** the output line includes the evaluated `when` expression's source text alongside the existing violation description

#### Scenario: Non-CEL violation is unaffected
- **WHEN** a context-dependency or layout-convention violation involves no `when`-bearing selector
- **THEN** its human-readable and JSON output are identical to the output produced before this change

#### Scenario: Layout-convention violation includes expression participation
- **WHEN** a `strict_layout_conventions[*].files_matching.when` predicate narrows which files are checked and a violation results
- **THEN** the JSON output for that violation includes the same `when_expressions` array shape used for context-dependency/context-allow-only violations
