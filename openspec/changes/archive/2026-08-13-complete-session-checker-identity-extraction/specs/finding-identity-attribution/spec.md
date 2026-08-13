## ADDED Requirements

### Requirement: Canonical finding-identity attribution is owned by a dedicated component

`ArchLinterNet.Core.Execution.ArchitectureFindingIdentityAttributor` SHALL own canonical finding-identity attribution, exposing `Attach(candidateLog, cursor, violations)` as a pure function: it SHALL read the finding-identity candidate log from `cursor` onward, SHALL NOT write to that log or to any other session state, and SHALL hold no state between calls. `ArchitectureAnalysisSession` SHALL retain the candidate log and the `FindingIdentityCursor` that brackets one contract's candidates, and `ArchitectureAnalysisSession.AttachFindingIdentities` SHALL be a delegation to this component.

#### Scenario: Attribution runs without a session lifecycle

- **WHEN** a caller invokes `ArchitectureFindingIdentityAttributor.Attach` with a hand-built candidate list, a cursor, and a hand-built violation list
- **THEN** it SHALL return the attributed violations without requiring an `ArchitectureAnalysisSession`, an `ArchitectureContractDocument`, a policy load, or any assembly resolution

#### Scenario: Attribution does not mutate its input

- **WHEN** `Attach` is called twice with the same candidate log, cursor and violations
- **THEN** both calls SHALL return equivalent attributed violations, because the component removes consumed candidates only from its own per-call working set and never from the caller's log

### Requirement: Attribution preserves candidate ordering, selection and tie-breaking

`Attach` SHALL select candidates by bucketing them on `(contract id, source type)` in candidate-log order, then, for each violation, matching a candidate's identity `TargetMember` or `ForbiddenReference` against each reported forbidden reference either exactly or as an `'<identity>@…'` / `'<identity> …'` prefix. For every family except `composition` it SHALL select at most one candidate per reported forbidden reference, in reported-reference order, consuming each candidate at most once across the violations of a single contract, and SHALL record the reference each identity was selected for. For a violation carrying a `CompositionPayload` it SHALL take every match and record no reference attribution. A violation with no matching candidate SHALL be returned unchanged.

#### Scenario: One identity per reported reference, in reported order

- **WHEN** a violation reports several forbidden references and the candidate log holds a matching candidate for each
- **THEN** the attributed violation SHALL carry one identity per reported reference in reported order, `Identity` SHALL be the first of them, and `IdentityReferences` SHALL name the reference each identity was selected for

#### Scenario: A candidate is consumed at most once

- **WHEN** two violations of the same contract and source type both report the same forbidden reference and two matching candidates exist
- **THEN** each violation SHALL receive a distinct candidate, so occurrence discrimination survives attribution

#### Scenario: Candidates before the cursor are not consumed

- **WHEN** `Attach` is called with a cursor equal to the candidate log's length
- **THEN** no violation SHALL receive an identity, so one contract's attribution cannot consume an earlier contract's candidates

### Requirement: Attribution cannot publish partial identity state

Finding-identity attribution SHALL be invoked exactly once per contract by `ArchitectureContractExecutor`, after that contract's checking has completed and after the executor's per-contract cancellation check. A cancelled run SHALL therefore expose either a contract's fully attributed findings or none of them.

#### Scenario: Cancellation between contracts

- **WHEN** a validation run is cancelled while contracts are executing
- **THEN** every finding already returned SHALL carry its complete canonical identity attribution, and no partially attributed finding SHALL be observable
