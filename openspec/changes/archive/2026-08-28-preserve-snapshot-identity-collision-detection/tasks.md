## 1. Collision-safe semantic projection

- [x] 1.1 Replace the global projected-entry deduplication with structural semantic-role and semantic-context deduplication, leaving all remaining entry identities for final validation; verify the Core project builds without warnings or errors.
- [x] 1.2 Add projector regressions proving equivalent observations are collapsed while delimiter-induced identity collisions between distinct metadata structures remain fail-closed.

## 2. Linked-source regression and lifecycle

- [x] 2.1 Add an integration regression that emits same-full-name marked CLR types in separate assemblies, verifies per-assembly classification facts, and verifies their snapshot role/context surfaces collapse successfully.
- [x] 2.2 Run focused snapshot/classification tests and the full Core test project; verify all pass.
- [x] 2.3 Format changed files, synchronize and archive OpenSpec, and verify `openspec validate --all` plus relevant repository lint checks pass.
