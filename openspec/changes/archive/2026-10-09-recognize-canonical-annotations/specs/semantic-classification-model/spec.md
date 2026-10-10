## ADDED Requirements

### Requirement: Canonical role evidence retains provenance in the existing role fact

Canonical role annotations SHALL resolve through the existing classification result and role index. The resulting role fact SHALL retain the exact FQN of every canonical or user-mapped attribute that contributes to an effective assignment. Evidence provenance SHALL be exposed with the same role fact used by selectors, contracts, coverage, explain, and report projections; canonical annotations SHALL NOT create a parallel semantic fact type or role index.

#### Scenario: A canonical role annotation is visible as canonical evidence
- **WHEN** a supported canonical role annotation is the only role evidence at its source specificity
- **THEN** the existing role fact exposes the canonical role, existing attribute source, and exact canonical FQN

#### Scenario: Equivalent canonical and custom evidence coalesces with both provenances
- **WHEN** a canonical role annotation and a distinct user-mapped attribute at the same specificity resolve to the same role and equivalent metadata
- **THEN** the classifier exposes one effective role fact and retains both exact FQNs in deterministic order so consumers can distinguish reserved canonical evidence from user-owned mapped evidence

#### Scenario: Contradictory canonical and custom evidence fails closed
- **WHEN** canonical and user-mapped attribute evidence at the same specificity resolve to different roles or incompatible same-specificity role metadata
- **THEN** the classifier records a deterministic conflict, exposes no effective role for that specificity, and does not choose a winner from attribute enumeration or YAML declaration order

#### Scenario: Existing source precedence remains authoritative
- **WHEN** a type-level canonical role conflicts with an assembly-level role annotation
- **THEN** the existing type-attribute precedence selects the type role, and metadata from the losing assembly role is not merged
