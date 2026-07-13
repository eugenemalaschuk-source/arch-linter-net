## 1. Coverage Contract and Model

- [x] 1.1 Extend the coverage contract model and validator with `semantic_role`, including semantic exclusion fields and required reasons.
- [x] 1.2 Add semantic coverage status/evidence records and ensure contract selection, ignored findings, and severity handling remain aligned with existing coverage scopes.

## 2. Semantic Coverage Execution

- [x] 2.1 Build semantic coverage classification from the session role index without rescanning or re-extracting types.
- [x] 2.2 Treat selector-backed layers and registered contextual consumers as governance sources, reporting uncovered, stale, unknown, excluded, and conflicting cases deterministically.
- [x] 2.3 Integrate semantic findings and summaries into the analysis outcome while preserving existing non-semantic coverage behavior.

## 3. Diagnostics and CLI Output

- [x] 3.1 Add human-readable semantic coverage diagnostics with clear distinction from dependency violations.
- [x] 3.2 Add JSON and CI-artifact semantic coverage summary/evidence fields with stable ordinal ordering.

## 4. Tests and Fixtures

- [x] 4.1 Add Core tests for classified-covered, classified-uncovered, unclassified, excluded, stale-selector, and conflicting-classification cases.
- [x] 4.2 Add Sales/Inventory/SharedKernel and Unity/client namespace-convention validation samples.
- [x] 4.3 Add CLI regression coverage for human, JSON, and CI summary diagnostics and existing coverage scopes.

## 5. Documentation and Completion

- [x] 5.1 Update coverage, semantic classification, policy-authoring, examples, and AI guidance documentation.
- [x] 5.2 Run formatting and acceptance validation, fix failures, synchronize specs, archive the OpenSpec change, and validate all specs.
