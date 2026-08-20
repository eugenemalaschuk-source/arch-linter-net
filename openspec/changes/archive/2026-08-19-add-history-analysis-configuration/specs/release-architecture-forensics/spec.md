## ADDED Requirements

### Requirement: One bounded history-analysis policy authority
The architecture policy MAY contain one optional `history_analysis` object. It
SHALL be loaded, composed, provenance-checked, schema-validated, and raw-YAML
validated by the normal policy lifecycle; a history command SHALL NOT accept a
second configuration document or individual semantic tuning switches.

When the object is absent, the effective profile SHALL contain the fixed #235
default weight profiles, no co-change significance threshold, no configured
path/ignore patterns, and the built-in `issue` TaskKey extractor. Supplying a
policy through the history command SHALL use exactly its effective
`history_analysis` object; omitting a policy SHALL use that default profile.

#### Scenario: Absent configuration preserves defaults
- **WHEN** a valid architecture policy does not declare `history_analysis`
- **THEN** policy loading succeeds and history ingestion uses the built-in issue extractor and default effective profile

#### Scenario: Imported configuration has one source of truth
- **WHEN** an imported architecture policy contributes `history_analysis`
- **THEN** composition and validation apply the same policy schema and diagnostics as every other policy section

### Requirement: Bounded configured TaskKey extractors
Each configured extractor SHALL contain a unique stable `id`, a namespace, and
a pattern. IDs and namespaces SHALL each match `[a-z][a-z0-9._-]*`; `issue` is
reserved for the built-in extractor and SHALL NOT be configured. A pattern
SHALL contain a non-empty literal `prefix` and an optional literal `suffix`.
It SHALL match exactly one positive ASCII-decimal identifier in
`prefix + [0-9]+ + suffix`, with the scalar before and after the full match,
when present, outside `[A-Za-z0-9_#]`.

Configured extractors SHALL scan raw message bytes and emit the whole literal
match as a non-empty half-open byte span. They SHALL pass matches to the
existing canonical TaskKey extraction stage, which alone owns strict UTF-8,
arbitrary-precision normalization, ordering, deduplication, and overlapping
different-key failure. Extractor declaration order SHALL NOT select a result.

#### Scenario: Custom literal extractor preserves provenance
- **WHEN** an extractor with ID `jira`, namespace `jira`, and prefix `JIRA-` examines `fix JIRA-001`
- **THEN** it emits the complete `JIRA-001` byte span with canonical TaskKey `(jira,1)`

#### Scenario: Reserved default semantics cannot be replaced
- **WHEN** a policy configures an extractor whose ID is `issue`
- **THEN** policy loading fails rather than replacing the built-in #235 boundary semantics

#### Scenario: Custom extractor collision fails closed
- **WHEN** the built-in or configured extractors produce overlapping spans for different TaskKeys
- **THEN** canonical extraction fails with the existing ambiguity diagnostic regardless of declaration order

### Requirement: Deterministic path categories and analysis ignores
`history_analysis.paths` MAY configure segment-glob pattern lists for
`production`, `tests`, `docs`, `generated`, `build_ci`, and `samples_examples`.
A glob SHALL use `/`-separated literal segments, `*` as one segment, and `**`
as zero or more segments. Backslashes, empty/dot segments, partial wildcards,
and character classes SHALL be rejected. Matching SHALL compare the exact
strict-UTF8 canonical path scalar sequence without normalization, collation, or
filesystem conversion.

`history_analysis.ignore` MAY contain the same glob grammar. An ignored path
SHALL be removed before category populations, `G0`, and every downstream score;
presentation options SHALL NOT rescore it. A retained path SHALL select the
first matching configured category in fixed order `production`, `tests`,
`docs`, `generated`, `build_ci`, `samples_examples`, or SHALL be `unknown`.

#### Scenario: Exact distinct Unicode paths remain distinct
- **WHEN** configured patterns match one of two scalar-distinct NFC/NFD path spellings
- **THEN** only that exact spelling matches and the other path retains its independently determined category

#### Scenario: Ignore precedes classification
- **WHEN** a path matches both an ignore pattern and a production pattern
- **THEN** it is removed before category and score population construction

### Requirement: Exact validated analysis profiles and threshold
`history_analysis.weights` MAY explicitly set complete hotspot, co-change,
bottleneck, and OCP profiles. Each value SHALL be a finite nonnegative ordinary
base-10 decimal literal with at most nine fractional digits; a positive value
enables its component and zero disables it. Every profile SHALL have at least
one enabled component and an exact sum of `1.000000000`; co-change alpha and
beta SHALL have that exact sum. Invalid input SHALL fail policy validation before
any quantization, analysis, or report construction and SHALL NOT be repaired.

`history_analysis.thresholds.co_change_significance`, when present, SHALL be a
canonical decimal in `[0,1]`. It SHALL be consumed only as the inclusive
`CombinedCoChange >= threshold` gate for later `Gtheta` clusters and candidate
construction, never as a score or `G0` control.

#### Scenario: Invalid profile fails before analysis
- **WHEN** a configured hotspot profile sums to `0.999999999` or has more than nine fractional digits
- **THEN** policy validation fails and no analysis starts

#### Scenario: Threshold equality qualifies only for Gtheta
- **WHEN** a future combined co-change edge equals the configured threshold
- **THEN** it qualifies for `Gtheta` without changing `G0` or file-score inputs
