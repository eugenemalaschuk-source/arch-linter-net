## 1. Provenance Model and Composition

- [x] 1.1 Add typed source descriptor, source location, diagnostic, and provenance-index models with portable public paths
- [x] 1.2 Enrich import graph sources with graph roles, authored edges, source ordinals, and root-based import chains
- [x] 1.3 Return effective-path-to-original-location metadata from composition for maps, sequences, scalars, contracts, and ignored violations
- [x] 1.4 Build and bind equivalent provenance indexes for imported and monolithic policy documents after deserialization and fallback ID assignment

## 2. Diagnostic Propagation

- [x] 2.1 Attach typed primary/related locations and import chains to graph, shape, schema, and composition failures
- [x] 2.2 Preserve owning locations for imported semantic validator failures without changing monolithic exception compatibility
- [x] 2.3 Enrich configuration violations, contract violations, policy-consistency findings, coverage findings, and unmatched ignored violations from the shared document index
- [x] 2.4 Verify CLI, Testing, graph, and explain continue to consume the same one-path resolved policy model

## 3. Output Compatibility

- [x] 3.1 Add compact policy locations to human diagnostic output while retaining existing text and root context
- [x] 3.2 Add structured policy and related locations to CI JSON as additive fields
- [x] 3.3 Add deterministic SARIF related locations without replacing existing physical or logical locations

## 4. Tests and Synchronization

- [x] 4.1 Add resolver/composer tests for root/fragment roles, YAML paths, both conflict locations, import chains, arbitrary names, and rename invariance
- [x] 4.2 Add semantic, missing-reference, consistency, ignored-violation, monolithic compatibility, and shared-entry-point provenance tests
- [x] 4.3 Add human, JSON, and SARIF serialization compatibility tests using portable paths
- [x] 4.4 Synchronize implementation decisions with the delta specs and internal import architecture documentation
- [x] 4.5 Run `rtk make fmt`, `rtk make acceptance`, and `rtk openspec validate --all`, fixing and rerunning until green
