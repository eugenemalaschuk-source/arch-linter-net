## 1. Deterministic record join

- [x] 1.1 Define zero-or-one produced-record cardinality for every expected applicability control; verify the modified requirement keeps one expected control/denominator member independent of source expansion and disallows duplicate record identities.
- [x] 1.2 Define duplicate-record failure handling; verify the scenario preserves a required control once in the denominator, excludes duplicates from the evaluable numerator, and exposes deterministic unassessable provenance.

## 2. Validation and completion

- [x] 2.1 Review the narrow correction for scope discipline and run strict validation; verify it changes only the existing OpenSpec contract and `rtk openspec validate enforce-applicability-record-cardinality --strict` succeeds.
