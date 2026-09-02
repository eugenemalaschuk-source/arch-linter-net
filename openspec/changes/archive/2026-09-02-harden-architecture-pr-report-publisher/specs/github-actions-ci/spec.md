## ADDED Requirements

### Requirement: Architecture report readiness is separate from strict gate enforcement

The read-only architecture report producer SHALL expose a successful bounded artifact-production
outcome when it has rendered and uploaded canonical report inputs, even if strict architecture
coverage has a valid failure. A dependent Architecture PR Report Gate SHALL fail the CI for that
strict result. The completed-CI publisher SHALL use the named producer job outcome and artifact
protocol rather than the aggregate CI conclusion.

#### Scenario: Strict failure still has a report artifact

- **WHEN** canonical report production succeeds but strict architecture coverage finds a failure
- **THEN** the producer job completes with its bounded artifact available
- **AND** the dependent Architecture PR Report Gate fails the CI
- **AND** the publisher can use the producer artifact without inspecting unrelated job outcomes
