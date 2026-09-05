## 1. Extract internal responsibilities

- [x] 1.1 Introduce a non-partial internal artifact-acquisition collaborator that preserves normalized repository-local paths, containment/reparse/regular-file checks, bounded reads, partial-byte hash facts, and cancellation; verify direct acquisition tests cover safe, missing, unsafe, unreadable, and over-limit outcomes.
- [x] 1.2 Introduce non-partial internal SARIF document, context, and source-diagnostic parsing/projection collaborators that retain current shape/status/detail and deterministic source facts; verify direct collaborator tests and existing source-projection tests pass.
- [x] 1.3 Reduce `SarifEvidenceReader` to its existing non-partial public constructor and `Read(...)` facade, composing internal facts into the unchanged result/provenance authority; verify the focused reader and trust-binding regression suite passes.

## 2. Govern the completed extraction

- [x] 2.1 Remove the exact reviewed `SarifEvidenceReader` partial-declaration exception and record the completed #774 slice in the active `decompose-god-classes` design/tasks; verify the architecture policy accepts no replacement partial aggregate.
- [x] 2.2 Run focused external-evidence binder/federation and CLI consumer tests to prove all consumers retain the same trust authority and zero-result/optional/required semantics.

## 3. Validate and finalize

- [x] 3.1 Run formatter, code-size, architecture, public-API, and OpenSpec checks; inspect the final diff and record results.
- [x] 3.2 Synchronize the scoped no-delta change with the actual implementation, archive it while retaining the umbrella cleanup change active, and verify `openspec validate --all`.
