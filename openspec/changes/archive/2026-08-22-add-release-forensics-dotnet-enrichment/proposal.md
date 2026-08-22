## Why

Release Architecture Forensics can identify Git-level pressure points, but its
findings lack useful .NET context for a reviewer. That context must remain
optional and revision-safe so build, project-discovery, or parser failures never
weaken the authoritative Git result.

## What Changes

- Add a deterministic optional .NET enrichment projection for finalized
  release-forensics findings.
- Bind enrichment to the analyzed repository revision and explicitly report
  not-requested, not-applicable, available, and unavailable outcomes.
- Reuse the existing Core project/source/type fact services where their trust
  boundary is compatible; do not make canonical Git ingestion depend on them.
- Preserve every Git-level finding, provenance record, score, rank, ordering,
  path identity, and rename outcome regardless of enrichment availability.

## Capabilities

### New Capabilities

- `release-forensics-dotnet-enrichment`: Revision-safe optional projection of
  .NET project, assembly, source, namespace, and type context onto finalized
  release-forensics findings.

### Modified Capabilities

- `release-architecture-forensics`: Define the enrichment boundary and its
  invariance with respect to canonical Git-level evidence.

## Impact

Affected code is limited to Core history analysis/report-facing result models and
their NUnit coverage, with corresponding OpenSpec and internal forensics
documentation. Git ingestion, canonical path/ref/metadata semantics, and scoring
remain unchanged.
