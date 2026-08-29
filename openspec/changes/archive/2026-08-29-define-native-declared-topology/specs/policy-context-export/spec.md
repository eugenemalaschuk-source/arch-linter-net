## ADDED Requirements

### Requirement: Effective policy context exports native topology facts
When an effective policy declares topology, the versioned policy-context export SHALL include its mode, observed subject kind and scope, empty-universe setting,
nodes and mapping selectors, directional edges, reviewed out-of-scope
declarations and reasons, stale-declaration setting, and portable effective
provenance. Collections SHALL use deterministic semantic ordering and the
export SHALL remain a static policy projection that performs no topology or
architecture analysis.

#### Scenario: Imported topology retains reviewed exclusion provenance
- **WHEN** a root policy imports a fragment declaring a reviewed topology out-of-scope entry
- **THEN** the exported topology fact names the entry, selector, reason, and portable fragment provenance without loading assemblies
