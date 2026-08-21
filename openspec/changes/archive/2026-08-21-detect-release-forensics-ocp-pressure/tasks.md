## 1. OCP analysis model and scorer

- [x] 1.1 Add internal OCP finding, raw evidence, normalized component, and weight models.
- [x] 1.2 Implement deterministic ASCII filename-stem role tokenization.
- [x] 1.3 Implement category-local OCP scoring from independent TaskKey pairs and raw `G0` centrality.
- [x] 1.4 Attach OCP analysis to successful history ingestion without changing canonical identity boundaries.

## 2. Reporting and tests

- [x] 2.1 Emit OCP findings and auditable evidence in canonical JSON.
- [x] 2.2 Add focused scorer tests for canonical TaskKeys, repeated-edit deduplication, role vectors, missing evidence, and `G0` threshold isolation.
- [x] 2.3 Extend canonical JSON tests for OCP evidence and pathname-reuse disclosure.

## 3. Verification and synchronization

- [x] 3.1 Run focused Core tests, formatting, OpenSpec validation, and the relevant architecture gate.
- [x] 3.2 Synchronize and archive the OpenSpec change.
