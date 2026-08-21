## 1. Bottleneck evidence model

- [x] 1.1 Add immutable bottleneck finding, pair, interval, component, and category-group models in Core history analysis.
- [x] 1.2 Implement independent pair detection from canonical file-event commits and exact epoch-second proximity.
- [x] 1.3 Implement category-local components, raw `G0` centrality, effective-weight scoring, and deterministic ranking.

## 2. Pipeline and evidence output

- [x] 2.1 Attach finalized bottleneck analysis to the ingestion result without changing ingestion or graph semantics.
- [x] 2.2 Serialize bottleneck findings and provenance through the interim canonical JSON writer.

## 3. Conformance and validation

- [x] 3.1 Add Git-backed tests for TaskKey canonicalization, pair exclusivity, exact intervals, identity boundaries, and JSON evidence.
- [x] 3.2 Add focused scorer tests for `G0`-only centrality, cohort normalization, thresholds, weights, and ranking.
- [x] 3.3 Run focused tests, formatter, architecture lint, and OpenSpec validation; synchronize and archive the change.
