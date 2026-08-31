## ADDED Requirements

### Requirement: Baseline projection preserves imported finding identity
Baseline-capable consumers SHALL accept imported-diagnostic candidates expressed through the
existing structured `ArchitectureBaselineCandidate` identity contract. Candidate identity SHALL be
the same canonical occurrence identity as the imported normalized finding and SHALL exclude
transient artifact/run provenance.

#### Scenario: Native and imported candidates remain distinct
- **WHEN** a native finding and an imported diagnostic have similar display text or source labels
- **THEN** their structured baseline candidates remain distinct unless their full canonical
  identities are exactly equal
