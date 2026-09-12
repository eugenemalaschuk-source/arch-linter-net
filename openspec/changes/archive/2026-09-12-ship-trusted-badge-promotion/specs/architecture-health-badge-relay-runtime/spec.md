## ADDED Requirements

### Requirement: Relay publisher integration preserves challenge and recovery authority
The reusable Relay adapter SHALL use the Relay's registered immutable identity, bounded challenge/generation/revocation protocol, and conditional commit semantics for initial publication, retry, renewal, invalidation, and recovery. It SHALL never derive authorization from a caller-supplied repository, workflow, run, URL, or SHA ordering and SHALL treat storage, clock, provider, and capability uncertainty as unavailable.

#### Scenario: Recovery requires current approved proof
- **WHEN** a Relay destination is recovered after restore, deletion, revocation, or ownership/configuration change
- **THEN** the adapter obtains a fresh approved current publisher proof and commits only through the Relay's current generation and revocation epoch
- **AND** it does not revive historical ready bytes or infer current main status from an unchanged tree

#### Scenario: Transport uncertainty does not preserve ready state
- **WHEN** the Relay reports timeout, storage uncertainty, provider uncertainty, unsupported configuration, or an ambiguous write result
- **THEN** the adapter reports a fixed transport-unavailable diagnostic
- **AND** it does not claim successful publication or extend a previous lease

### Requirement: Relay integration does not disclose private provenance
The reusable Relay adapter SHALL send only the approved canonical payload and minimum private envelope required by the Relay protocol. It SHALL not send raw tokens, source names, repository history, PR/run details, paths, or confidential diagnostics to public read routes or public error summaries.

#### Scenario: Public response remains opaque
- **WHEN** a Relay-backed publication succeeds or fails
- **THEN** public reads expose only the configured disclosure representation or fixed unavailable representation
- **AND** private provenance remains available only through the private source receipt path
