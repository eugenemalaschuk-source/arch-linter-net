## MODIFIED Requirements

### Requirement: Candidate-bound release history analysis

The manual release workflow SHALL run a standalone, read-only history-forensics job after preparing the immutable candidate. The job SHALL verify and use the exact candidate CLI package and candidate source commit/tree, read the complete required Git object graph, and run exactly one Git-only analysis that emits canonical JSON and Markdown from the same finalized result. It SHALL NOT build the analyzed solution or run automatic .NET enrichment. Candidate versions SHALL use the complete package-version syntax accepted by the existing `version_override` validation.

Stable candidates SHALL use the highest lower stable SemVer release tag whose peeled commit is an ancestor of the candidate. Prerelease candidates SHALL have a distinct `preview:X.Y.Z` series identity and use the highest lower SemVer prerelease tag in that same `X.Y.Z` line when one exists; prerelease identifiers SHALL be ordered using NuGet SemVer precedence. The first prerelease in a line SHALL use the highest lower stable ancestor. Build metadata SHALL remain part of the candidate version/tag identity but SHALL NOT affect version precedence. Stable selection SHALL ignore prerelease and development-build tags. Selection SHALL pin the authored tag and resolved full SHA before analysis and SHALL fail closed for shallow history, missing required objects, ambiguous SemVer-precedence tags, invalid candidate identity, or mismatched candidate package identity. If no eligible predecessor exists, the result SHALL be typed `not-applicable: no_previous_release`, with no fabricated root predecessor or empty successful analysis.

The selected range SHALL be the complete exclusive-base/inclusive-candidate Git range. It SHALL NOT be truncated to a fixed commit count, changed to a date range, or reduced to first-parent history.

#### Scenario: Stable candidate uses the previous stable ancestor

- **WHEN** a stable candidate has lower stable, prerelease, and `main.N` tags reachable from its commit
- **THEN** the analysis starts exclusively at the highest lower stable tag on candidate ancestry and ends inclusively at the exact candidate commit
- **AND** prerelease and development-build tags do not become the stable boundary

#### Scenario: Preview candidate remains in its own line

- **WHEN** an `alpha`, `beta`, `rc`, or `preview` candidate has a lower prerelease tag in the same `X.Y.Z` line on candidate ancestry
- **THEN** the analysis uses the highest lower tag by NuGet SemVer precedence and records series identity `preview:X.Y.Z`
- **WHEN** no prior prerelease in that line exists
- **THEN** the analysis uses the highest lower stable ancestor, if present
- **AND** build metadata is preserved in candidate identity but does not change predecessor ordering

#### Scenario: First release has no predecessor

- **WHEN** no eligible stable predecessor exists for a candidate
- **THEN** the workflow produces a typed `not-applicable: no_previous_release` result
- **AND** it does not invent a root predecessor or claim a successful empty-range analysis

#### Scenario: Incomplete or ambiguous Git identity

- **WHEN** the checkout is shallow, a required tag/object is missing, multiple eligible tags have equivalent SemVer precedence, or candidate/package identity differs
- **THEN** the history job fails and no successful forensics bundle is published

#### Scenario: Findings do not gate release

- **WHEN** a complete history analysis contains high hotspots, bottlenecks, or OCP findings
- **THEN** those findings remain evidence and do not alter release authorization
- **WHEN** range selection, ingestion, rendering, or required artifact production fails
- **THEN** the job fails visibly and package publication remains blocked

## ADDED Requirements

### Requirement: Bundle rendering duration excludes analyzer process time

The operational observation `bundle_render_ms` SHALL measure runner-side report and provenance-manifest rendering after the candidate analyzer has returned. The analyzer process wall duration SHALL be recorded separately as `analyzer.process_wall_ms` and SHALL NOT be duplicated in `bundle_render_ms` or another orchestration duration.

#### Scenario: Analyzer process duration is separate from bundle rendering

- **WHEN** an applicable candidate completes the history analysis and bundle generation
- **THEN** `analyzer.process_wall_ms` records the candidate CLI process duration
- **AND** `bundle_render_ms` measures only the runner-side report/manifest work after that process completes
