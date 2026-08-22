## ADDED Requirements

### Requirement: Release-closure dogfood and conformance evidence
Before a Release Architecture Forensics v1 release story closes, the repository
SHALL retain a repository-safe deterministic dogfood summary for at least one
real ArchLinterNet release range. The summary SHALL record separate authored
`from` and `to` operands, their resolved canonical object IDs, effective
history-analysis configuration/profile identity, tool version and source
revision, canonical JSON artifact identities, selected finding/candidate
identities, comparison with known manual observations, documented intentional
v1 false-positive/false-negative limitations, tuning evidence when applicable,
and enrichment status.

The dogfood command SHALL use separate `--from` and `--to` operands; it SHALL
NOT use a Git revision-expression range as one operand. The release conformance
suite SHALL retain focused vectors proving that canonical JSON is invariant under
environment presentation variation and that available or unavailable optional
.NET enrichment changes only the reserved enrichment projection, never Git-level
evidence, findings, scores, ranks, or candidate ordering.

#### Scenario: Historical range has unavailable enrichment
- **WHEN** a real historical release range is analyzed with requested .NET
  enrichment from a worktree whose checked-out `HEAD` differs from resolved `to`
- **THEN** the command produces a successful Git-only report with an explicit
  `unavailable` enrichment status and the same Git-level canonical output as
  the corresponding run without requested enrichment

#### Scenario: Environment-varied canonical report
- **WHEN** identical finalized Git evidence and configuration are rendered in
  different presentation environments
- **THEN** the canonical JSON bytes before the reserved enrichment projection
  are identical and retain the same findings, scores, ranks, and candidates
