## 1. Schema and canonical model

- [x] 1.1 Add structured waiver YAML fields, profile configuration, typed validation, duplicate-ID checks, exact target-fingerprint support, and composed provenance resolution while retaining legacy/baseline compatibility.
- [x] 1.2 Add public canonical waiver declaration/lifecycle records and deterministic date/fingerprint helpers with focused policy-load and identity tests.

## 2. Lifecycle evaluation and enforcement

- [x] 2.1 Implement one Core lifecycle evaluator over tracked ignore matches and an explicit date/UTC-clock boundary, with invalid/expired/stale/metadata-incomplete/active precedence.
- [x] 2.2 Integrate lifecycle records and strict/compatibility hygiene into validation snapshots, outcomes, cache persistence, normal formatters, and focused Core tests.

## 3. Existing governance seams

- [x] 3.1 Extend effective policy-context exports with typed waiver/profile evidence and update schema/readback tests.
- [x] 3.2 Extend the existing policy-weakening comparer to classify structured waiver additions/broadenings using that evidence, preserving #119 severity and gate boundaries.

## 4. Consumer projections and documentation

- [x] 4.1 Expose lifecycle records and explicit evaluation-date configuration through the Testing adapter and CLI, including human/JSON diagnostics and parity tests.
- [x] 4.2 Update policy examples, migration/governance documentation, approved public API snapshots, and relevant self-policy fixtures for the v0.7-to-v0.8 compatibility path.

## 5. Verification and OpenSpec completion

- [x] 5.1 Run focused Core, CLI, and Testing tests; formatting; policy/schema/OpenSpec validation; and the risk-appropriate architecture gate, fixing issue-related failures.
- [x] 5.2 Synchronize the implemented specification, archive the OpenSpec change, inspect the archive, and run final OpenSpec validation before opening the PR.
