## Why

Release Architecture Forensics needs one reviewed, deterministic theory before
the CLI, policy, scoring, graph, report, and dogfood/governance tasks begin.
Without this contract, later tasks can silently diverge on Git metadata decoding,
task identity, rename lineage, normalization, missing-evidence handling, ordering,
and the difference between evidence-backed pressure and proof of a design-law
violation.

## What Changes

- Define the Release Architecture Forensics input identity, raw commit-metadata
  semantics, canonical TaskKey identity, Git-range/file evidence, path/rename
  identity, score components, stable ranking, and report semantics.
- Establish zero-denominator, absent-evidence, encoding, DAG-ambiguity, and
  canonical-number behavior so results never depend on runtime/Git-library
  fallbacks, extractor iteration order, traversal order, or weight renormalization.
- Document a Git-only deterministic core with optional .NET enrichment, plus
  evidence-backed refactoring investigations and interpretation limits.
- Add an internal contributor reference that is discoverable from the internal
  documentation index without presenting unimplemented functionality as a
  public product guarantee.

## Capabilities

### New Capabilities

- `release-architecture-forensics`: Defines deterministic Git-range evidence,
  scoring, finding, recommendation, and report requirements for the planned
  release-forensics feature.

### Modified Capabilities

- None.

## Impact

Adds OpenSpec and internal documentation only. It creates no CLI command, policy
field, public API, analyzer, report writer, or dependency. Follow-on issues #236
through #244 consume this contract when they implement, report, dogfood, and
govern the capability.
