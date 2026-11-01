## Why

The cycle detector repeats path-local DFS from every vertex, so an acyclic dense graph explores exponentially many paths even though it reports nothing. Partitioning the graph into strongly connected components can reject those paths before enumeration while preserving the complete cycle diagnostics callers already receive.

## What Changes

- Partition each cycle graph into strongly connected components and enumerate only within cyclic components.
- Prepare ordered adjacency once per analysis invocation, preserving current cycle strings and their deterministic ordering.
- Add structural regression coverage for dense and layered DAGs, cyclic components, tails, disconnected nodes, and self-edges.
- Record reproducible before/after work, elapsed-time, and allocation evidence using the existing benchmark and test foundation.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `cycle-contracts`: Specify complete simple-cycle output and deterministic ordering independently of the detector algorithm.

## Impact

The change is internal to Core's cycle graph detector and its two callers. It adds no public API or dependency. Tests will verify exact cycle-set parity and a bounded work model for zero-output DAGs; the evidence note will distinguish detector-local measurements from unmeasured end-to-end phase share.
