## 1. Policy model and expansion

- [x] 1.1 Add the schema-backed `source_sets` model, contract-level `sources`/`source_sets`, and the list-shaped companion keys.
- [x] 1.2 Resolve sets deterministically against declared policy inputs, with fail-closed zero-match, unknown-reference, out-of-target, and bounded-expansion diagnostics.
- [x] 1.3 Expand authored contracts into per-source instances and re-bind authored provenance.

## 2. Identity, coverage, and projections

- [x] 2.1 Keep authored identity resolvable for contract selection and rule-input coverage.
- [x] 2.2 Expose the resolved expansion inventory through the coverage inventory, `explain`, JSON, and SARIF.

## 3. Verification and documentation

- [x] 3.1 Add unit tests for explicit lists, named sets, globs, overlap, optional-empty, bounds, and large expansions.
- [x] 3.2 Add imported-policy provenance, coverage, and `explain` integration tests.
- [x] 3.3 Update schema, capability manifest, docs, and AI guidance.
- [x] 3.4 Run focused tests, formatting, acceptance, and OpenSpec validation; archive the change.
