## 1. Expected membership and record integrity

- [x] 1.1 Establish the independent expected applicability-membership collection and its canonical effective-control identity/provenance boundary; verify the modified specification makes membership authoritative outside produced records and preserves #685 as a consumer rather than a counting authority.
- [x] 1.2 Establish the expected-to-produced left join and missing-record rule; verify the A/B required-control scenario preserves B in the denominator as `unassessable` with `missing_applicability_record` without family-semantic inference.

## 2. Exhaustive state invariants

- [x] 2.1 Define and review the complete membership × evidence-condition × state table; verify it permits only required/evaluable-or-unassessable, optional/evaluable-not-applicable-or-unassessable, and not-applicable/not-applicable combinations.
- [x] 2.2 Define optional supplied-invalid and invalid-combination behavior; verify malformed/stale optional evidence is `unassessable` and not-applicable/evaluable or not-applicable/unassessable records cannot be interpreted as valid outcomes.

## 3. Validation and completion

- [x] 3.1 Review the correction proposal, modified delta spec, and design for scope discipline; verify they add no policy schema, production implementation, public API, CLI behavior, finding model, or effective-rule inventory.
- [x] 3.2 Run strict change validation and full OpenSpec validation after synchronization; verify `rtk openspec validate complete-applicability-membership-contract --strict` and `rtk openspec validate --all` both succeed.
