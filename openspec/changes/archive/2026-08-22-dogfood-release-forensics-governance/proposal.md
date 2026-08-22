## Why

Release Architecture Forensics has shipped its canonical Git evidence, scoring,
reporting, and optional .NET enrichment increments. Before the v0.7 story can
close, ArchLinterNet must exercise that delivered surface against a real release
range and make the separation of its completed modules executable policy rather
than relying on implementation convention alone.

## What Changes

- Add a repository-safe, deterministic dogfood record for the real
  `v0.3.1` to `v0.4.0` release range, including its exact input identity,
  observations, intentional v1 limitations, and enrichment outcome.
- Add focused release-closure conformance vectors for canonical JSON stability
  and the invariant that optional enrichment cannot change Git-level output.
- Declare strict self-policy boundaries for the history Git, configuration,
  analysis, reporting, enrichment, and CLI command modules, and include them
  in the repository's rule-input coverage contract.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `release-architecture-forensics`: Require reproducible real-range dogfood
  evidence and release-closure conformance coverage for the finalized v1
  semantics.
- `self-architecture-policy`: Govern the Release Architecture Forensics module
  seams through the existing executable repository policy.

## Impact

Affected areas are Release Architecture Forensics tests and internal
documentation, `architecture/dependencies.arch.yml` and its policy fragments,
the policy rule-input coverage inventory, and the two modified OpenSpec
capability specifications. No public API, package dependency, scoring rule, or
history-analysis configuration semantics change.
