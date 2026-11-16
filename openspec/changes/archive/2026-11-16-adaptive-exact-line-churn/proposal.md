# Proposal

## Why

History line churn currently performs one dynamic-programming update for every pair of lines that remain after common-prefix/suffix trimming. Large files with a few distant edits therefore take quadratic time even though their exact insertion/deletion distance is small.

## What Changes

- Add a bounded exact fast path for sparse line edits, with an exact two-row LCS fallback when the fast-path distance bound is exceeded.
- Preserve raw-byte line splitting, mathematical LCS totals, binary/rename routing, and exact dense-case behavior.
- Reuse the existing #502 History workloads and retain deterministic work and environment-labelled before/after evidence.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `release-architecture-forensics`: specify bounded exact work for small-edit-distance line churn while preserving the existing canonical LCS result and dense fallback.

## Impact

The change is limited to `LineChurnCalculator`, its Core NUnit coverage, the existing History benchmark corpus, internal performance evidence, and the `release-architecture-forensics` specification. No public API, report schema, configuration, dependency, or line-count semantics change.
