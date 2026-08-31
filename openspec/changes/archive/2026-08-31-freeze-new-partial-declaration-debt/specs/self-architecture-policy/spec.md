## ADDED Requirements

### Requirement: New or grown production partial-type aggregates are blocked

The repository's architecture contract SHALL declare a strict `layout_conventions` rule reusing
`max_declarations_per_type: 1` over production source (`folder_segment: src`), separate from the
existing audit-only `production-types-have-one-source-declaration` rule, which remains unchanged as
the full-debt inventory `decompose-god-classes` targets. The strict rule SHALL freeze today's
reviewed offending types through exact-match `ignored_violations` entries (exact `source_type` and
exact `forbidden_reference`, matching this repository's already-shipped ignore mechanism) rather
than a new baseline file, metric kind, or per-type numeric-override schema field. Every reviewed
entry SHALL be an exact snapshot of one type's current declaration count and file list.

#### Scenario: An unchanged known aggregate remains accepted debt
- **WHEN** a reviewed type's declarations exactly match its frozen `ignored_violations` entry
- **THEN** `make lint-architecture` does not report a violation for that type

#### Scenario: A reviewed aggregate gains a declaration
- **WHEN** a type with a frozen reviewed entry gains an additional source declaration
- **THEN** its declaration text and canonical identity change, the frozen entry no longer matches,
  and strict validation fails for that type

#### Scenario: A new handwritten partial type is introduced
- **WHEN** a production type outside the reviewed exception list is declared across more than one
  source file
- **THEN** strict validation fails naming the type, its actual declaration count, and its paths

#### Scenario: A reviewed aggregate is fully resolved
- **WHEN** a reviewed type's declarations are reduced to exactly one
- **THEN** the checker stops reporting a candidate for that type and its now-stale
  `ignored_violations` entry must be removed in the same change, because
  `unmatched_ignored_violations` fails closed by default

#### Scenario: Improving a reviewed aggregate without finishing it changes required evidence
- **WHEN** a reviewed type's declaration count decreases but remains above one
- **THEN** its frozen entry's exact text no longer matches the new count, and the change must update
  that entry to the new exact evidence for strict validation to pass

#### Scenario: The audit inventory remains the full-debt authority
- **WHEN** `make audit-architecture` runs `production-types-have-one-source-declaration`
- **THEN** it continues to report every production type above one declaration, including reviewed
  entries accepted by the strict ratchet rule
