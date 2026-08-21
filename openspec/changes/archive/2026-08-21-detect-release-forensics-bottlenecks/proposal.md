## Why

Release forensics already ingests canonical Git evidence and builds the `G0` co-change graph, but it cannot yet identify files under explainable parallel-development pressure. This completes the next evidence projection without conflating the heuristic with proof of a merge conflict.

## What Changes

- Derive independent canonical-TaskKey pairs, exact epoch-second intervals, and temporal proximity per logical file.
- Compute cohort-local bottleneck components, `G0`-only centrality, deterministic rankings, and evidence/provenance suitable for the future report renderer.
- Expose the analysis through the existing deterministic ingestion JSON surface and protect its semantics with conformance tests.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `release-architecture-forensics`: Add the implemented bottleneck evidence projection and its deterministic presentation guarantees.

## Impact

Affected code is confined to the Core history-analysis and intermediate reporting layers, their NUnit conformance tests, and the release-forensics specification. The CLI command shape, Git ingestion semantics, logical-file identity, and `G0` topology remain unchanged.
