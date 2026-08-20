## ADDED Requirements

### Requirement: Deterministic in-memory hotspot findings
The Core history-analysis layer SHALL expose a deterministic in-memory hotspot
analysis result for a successful canonical ingestion result and its validated
effective `history_analysis` configuration. Each retained logical-file finding
SHALL retain canonical path, primary category, raw commit/churn/TaskKey/author/
temporal evidence, nine-decimal canonical components and score, effective
hotspot weights, line-count statuses, and the inherited pathname-reuse
limitation status. The analysis layer SHALL consume only canonical ingestion
evidence and SHALL NOT re-resolve refs, re-decode metadata, re-extract task keys,
segment same-path lifetimes, re-evaluate rename candidates, or use host
date/time conversion.

#### Scenario: Canonical evidence is independent of source spellings
- **WHEN** canonical evidence contains task spellings `#001` and `#1`, or equal
  committer epoch integers with distinct timezone tokens
- **THEN** hotspot task breadth and temporal span consume the canonical TaskKey
  and exact epoch integer without observing those source spelling/token variants

### Requirement: Cohort-safe hotspot result ordering
The hotspot analysis layer SHALL apply configured history ignores before
classification, normalization, and scoring. It SHALL normalize each hotspot
component only among retained files in the same primary category and SHALL group
results in canonical category order with production as the first human-facing
group. Within one category it SHALL order findings by descending canonical score,
descending canonical TaskKey spread, descending churn, descending commit count,
and ascending scalar-value canonical path.

#### Scenario: Non-production cannot change production scores
- **WHEN** a generated file has more churn than every production file
- **THEN** the generated file does not affect any production hotspot component,
  score, or production-group ranking
