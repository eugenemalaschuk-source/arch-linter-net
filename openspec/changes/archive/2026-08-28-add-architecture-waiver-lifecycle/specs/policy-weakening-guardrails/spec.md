## ADDED Requirements

### Requirement: Structured waiver changes remain normalized weakening evidence
The policy-weakening comparer SHALL recognize added structured waivers and
deterministically provable target broadening from typed policy-context waiver
evidence. It SHALL emit existing normalized change-time findings with the
configured policy-weakening severity and provenance, and SHALL NOT create
baseline debt or independently compose a gate result.

#### Scenario: New structured waiver is visible to the existing guardrail
- **WHEN** current context adds a complete structured waiver absent from base
  context under the strict v0.8 profile
- **THEN** comparison emits a deterministic configured-severity weakening
  finding identifying the waiver ID, governed contract, target, and provenance
