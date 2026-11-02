## Why

The indexed recorder wins for large evidence batches but can pay a fixed SCC construction cost on small batches. Existing profiles report only the enclosing cycle-family phase, so the recorder's cumulative share and the joint per-invocation workload `(V,E,B,U,K)` cannot be attributed on a real analysis. Add opt-in measurements before deciding whether a production fallback has a material end-to-end benefit.

## What Changes

- Extend the opt-in `analysis-profile/v1` output with bounded, per-invocation cycle-recorder observations: strict/audit group, enabled/empty state, graph vertex/edge counts, evidence count and unique endpoint-pair count, emitted candidate count, total/SCC-build/evidence-processing duration, and recorder allocations/work counters.
- Include disabled and empty recorder calls in the observation denominators; emit numeric counts only, never layer names, evidence identities, repository paths, or graph topology.
- Capture observations only when profiling is explicitly enabled. Keep ordinary analysis, candidate semantics, and the SCC-only production algorithm unchanged; do not add a selector, public CLI/YAML setting, or telemetry upload.
- Reuse the #502/#1123 profiling and benchmark infrastructure for paired end-to-end and direct-recorder evidence. Keep synthetic stress results separate from the maintainer-approved ArchLinterNet self-analysis dogfood run.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `analysis-profile`: define the opt-in recorder observation contract and its no-topology/no-telemetry boundary.

## Impact

Affected areas: Core cycle recording/session profiling, the `analysis-profile/v1` model/writer/schema/dictionary, benchmark evidence and focused tests. No dependency, public CLI/YAML option, architecture policy, or production algorithm-selection change is intended. This is post-release performance stabilization for the published v0.10.0 line; it does not reopen release scope or make #1147 a publication blocker.
