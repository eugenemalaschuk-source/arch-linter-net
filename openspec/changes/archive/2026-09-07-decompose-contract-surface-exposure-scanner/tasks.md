## 1. Extract scanning responsibilities

- [x] 1.1 Replace the partial scanner and nested walker with non-partial shared-state, recursive-traversal, member/accessor, and attribute-metadata collaborators; verify focused `ContractSurfaceExposureIndexTests` preserve paths, ordering, cycles, and incomplete evidence.
- [x] 1.2 Retain consumer behavior for contract-surface exposure, versioned isolation, and reference-policy checks; verify their focused Core test families pass.

## 2. Govern and validate the completed extraction

- [x] 2.1 Remove the two exact reviewed scanner/walker partial-declaration exceptions and record #775 in the active `decompose-god-classes` design/tasks; verify `make lint-architecture` passes.
- [x] 2.2 Run formatting, code-size, public-API, and OpenSpec validation; inspect the final diff and verify no public API or unrelated formatter changes are included.
