## 1. Reproducible cycle evidence and regression cases

- [x] 1.1 Add exact cycle-output cases for cyclic components, acyclic tails, disconnected nodes, and self-edges; verify focused Core tests and reproduce the pre-change work counts.
- [x] 1.2 Add a focused detector benchmark to the existing Core test/benchmark foundation; run complete-DAG and output-heavy SCC baselines and retain elapsed, allocation, retained-memory, and output-digest evidence.

## 2. SCC-pruned cycle enumeration

- [x] 2.1 Prepare ordered adjacency once, classify strongly connected components iteratively, and skip acyclic components/inter-component edges before path enumeration; verify structural work bounds for V=10/20/30/50 DAGs.
- [x] 2.2 Preserve all cycle rotations, self-edge output, ordering, and both contract checker paths; run CycleDetector, CycleContract, AcyclicSiblingContract, strict/audit, suppression, cancellation, identity, and determinism tests.

## 3. Evidence and OpenSpec completion

- [x] 3.1 Run the same direct detector workload after the change and record work, elapsed, allocation, retained-memory, output digest, environment, and the unmeasured end-to-end phase share in the existing benchmarking evidence directory.
- [x] 3.2 Synchronize and archive the cycle-contract spec delta, run OpenSpec validation, and review the final diff for API, architecture, and scope drift.
