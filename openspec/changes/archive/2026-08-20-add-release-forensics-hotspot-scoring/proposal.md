## Why

Release Architecture Forensics ingests canonical Git file evidence but cannot yet
turn it into deterministic, cohort-safe hotspot findings. This change implements
the first derived scoring stage from the semantic authority established for the
release-forensics milestone.

## What Changes

- Add deterministic, file-level hotspot metric calculation from canonical history
  evidence and the validated effective history-analysis profile.
- Normalize commit, churn, task, author, and temporal evidence independently in
  each file's primary category; calculate and rank canonical hotspot scores.
- Retain finding evidence needed by the later report work, including raw and
  canonical metrics, weights, category, and limitations.
- Add conformance tests for canonical TaskKey and author breadth, exact temporal
  integers, zero-churn rename and binary events, pathname reuse, ordering, and
  category isolation.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `release-architecture-forensics`: add the implemented hotspot-scoring behavior
  and its deterministic evidence, cohort, and ranking guarantees.

## Impact

This affects `ArchLinterNet.Core` History analysis models and services plus Core
history tests. It consumes existing canonical ingestion/configuration APIs and
does not add a CLI command, Git/ref parsing behavior, report format, co-change,
bottleneck, OCP, or .NET-enrichment behavior.
