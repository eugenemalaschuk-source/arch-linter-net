## 1. Extract topology observation

- [x] 1.1 Move normal validation/capture and metric ownership observation behind named internal
  collaborators, preserving the existing session fact sources and deterministic observation data;
  verify focused topology and metric tests compile and pass.
- [x] 1.2 Reduce `ArchitectureTopologyEvaluator` to one non-partial policy evaluator that consumes
  observed facts, and rewire validation, capture, and metric entry points; verify existing topology
  mapping, capture, and metric ownership tests pass.

## 2. Prove and govern compatibility

- [x] 2.1 Add focused parity coverage for the shared normal-validation/capture compatibility
  projection and the distinct canonical metric ownership projection; verify its focused tests pass.
- [x] 2.2 Remove the exact reviewed `ArchitectureTopologyEvaluator` declaration-count exception
  without adding a replacement partial aggregate; verify `make lint-architecture` passes.

## 3. Complete the change

- [x] 3.1 Run risk-appropriate topology, capture/diff/verify, metric, formatting, code-size,
  architecture, public-API, and OpenSpec validation; inspect the final diff and record results.
- [x] 3.2 Archive the scoped OpenSpec change after synchronization and confirm its archive and main
  specs state; retain the unrelated umbrella cleanup change as active.
