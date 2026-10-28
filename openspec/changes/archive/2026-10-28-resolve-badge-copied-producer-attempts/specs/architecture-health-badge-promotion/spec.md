# Spec Delta

## MODIFIED Requirements

### Requirement: Promotion uses approved configuration and exact producer provenance
The reusable promotion capability SHALL obtain repository, workflow, job, check, artifact, branch, event, and adapter selection from an approved configuration or registry that is independent of consumer-controlled pull-request artifacts. It SHALL prove the exact merged commit, base/head/tree relation, required check identity, workflow run and attempt, producer job, artifact identity, size, schema, digest, and canonical bytes before a ready publication. The resolver SHALL obtain the workflow file SHA from the provider's immutable content response at the verified producer revision and compare it with the approved pin; it SHALL not populate evidence by copying the expected pin. Names, conclusions, arbitrary URLs, or a generic workflow success SHALL not establish those facts. When a selected check points to a copied successful job, the resolver SHALL determine the effective execution and producer job identity from the selected check's exact run/job binding and complete bounded attempt history. Matching valid execution timestamps across consecutive attempts SHALL identify copies; a distinct valid timestamp pair in chronological order SHALL identify a later execution. Missing, invalid, ambiguous, misordered, or contradictory lineage SHALL fail closed. The effective attempt SHALL be used to validate the artifact manifest, and any job identity present in that manifest SHALL match the resolved producer job. The effective attempt and resolved producer job identity SHALL be used to compute producer identity and bind any required temporal receipt; artifact contents SHALL not authorize the expected producer.

#### Scenario: Mismatched producer evidence fails closed
- **WHEN** a producer run, check, artifact, tree, attempt, job, repository, or merge relation differs from approved configuration or the merged commit
- **THEN** promotion publishes the fixed unavailable state with a redacted actionable reason
- **AND** it does not reuse a different green artifact or preserve the previous ready state

#### Scenario: Consumer input cannot choose a producer
- **WHEN** a consumer-controlled input supplies a workflow name, artifact name, run URL, repository URL, or source checkout path that is not the approved configuration
- **THEN** promotion rejects the input before any publication write
- **AND** no consumer source, script, action, or generated artifact is executed

#### Scenario: Copied successful producer jobs retain original execution provenance
- **WHEN** the selected successful check points to a later copied producer job and preceding attempts contain the same unique producer execution with matching valid timestamps
- **THEN** promotion uses the earliest matching attempt to validate the original artifact manifest
- **AND** any job identity present in the manifest matches the resolved producer job
- **AND** the effective attempt and resolved producer job identity bind the producer identity and any required temporal receipt

#### Scenario: A genuine producer rerun establishes a new execution
- **WHEN** the selected producer job has valid timestamps that differ from the preceding attempt's unique producer job and are chronologically consistent
- **THEN** promotion retains the selected job's attempt and identity
- **AND** it does not select an older attempt or artifact solely because it is older

#### Scenario: Missing or contradictory producer lineage is unavailable
- **WHEN** attempt history is missing, ambiguous, malformed, timestamp-invalid, misordered, or inconsistent with the selected run, head, attempt, or job
- **THEN** promotion rejects the evidence before publication
- **AND** artifact contents do not supply replacement provenance
