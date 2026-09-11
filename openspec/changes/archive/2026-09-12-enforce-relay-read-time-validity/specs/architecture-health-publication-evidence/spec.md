## ADDED Requirements

### Requirement: Transport validity checks preserve the canonical reuse boundary
Any publication transport that presents a bounded-current representation SHALL
compare the product-owned semantic reuse horizon against its trusted read-time
clock before serving readiness. The transport SHALL treat that horizon as a
verified boundary only; it SHALL NOT re-evaluate waivers, rules, external
evidence, Gate, Health, counts, or canonical payload content to derive a later
or current result.

#### Scenario: Same tree cannot revive elapsed canonical evidence
- **WHEN** a stored canonical payload has unchanged bytes but its product-owned
  reuse horizon has elapsed
- **THEN** the transport reports the publication unavailable at read time
- **AND** it does not use the unchanged tree or payload digest to renew it
