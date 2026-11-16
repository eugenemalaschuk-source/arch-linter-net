# Spec Delta

## ADDED Requirements

### Requirement: Exact sparse line churn uses bounded work

For raw-byte line sequences of lengths n and m with insertion/deletion distance D, churn computation SHALL use an exact low-distance path bounded by a fixed K. For D ≤ K, the path SHALL return mathematical LCS with O((n+m)(D+1)) work and O(K) auxiliary frontier memory. If search reaches K without a result, computation SHALL use the exact O(nm) two-row fallback with O(min(n,m)) DP memory; it SHALL never emit partial churn.

#### Scenario: Sparse distant edits stay on the exact fast path

- **WHEN** large line sequences contain a small insertion/deletion distance within K, including edits separated by long unchanged regions
- **THEN** churn uses the exact LCS result without performing the full n×m dynamic-programming work

#### Scenario: Dense differences use exact bounded-memory fallback

- **WHEN** the edit distance exceeds K
- **THEN** the bounded search stops, the exact two-row fallback computes the mathematical LCS, and no unbounded frontier trace or partial result is retained

#### Scenario: Ambiguous alignments preserve canonical totals

- **WHEN** repeated lines admit multiple valid alignments
- **THEN** additions and deletions remain derived only from line counts and the unique mathematical LCS length
