## Why

The #244 dogfood guardrail currently groups canonical file-evidence construction
and heuristic scoring in one namespace layer. That permits unreviewed scorer
changes to reach back into finalized file identity, rename lineage, and churn
semantics — exactly the semantic drift the release closure must prevent.

## What Changes

- Separate canonical file-evidence construction from heuristic scoring in
  production namespaces and the repository self-policy.
- Add strict one-way architecture contracts: evidence cannot depend on scoring;
  scoring may consume finalized evidence but cannot use raw Git ingestion,
  reports, or enrichment.
- Specify that dogfood observations requiring canonical-evidence semantic change
  are separate reviewed specification/migration work, not in-story tuning.
- Prove the corrected specifications in strict OpenSpec mode.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `release-architecture-forensics`: Preserve canonical evidence semantics during
  dogfooding and route semantic changes through a separate reviewed migration.
- `self-architecture-policy`: Govern the strict one-way boundary between History
  evidence construction and scoring.

## Impact

`ArchLinterNet.Core.History` namespaces, History NUnit tests,
`architecture/dependencies.arch.yml`, dependency-boundary coverage, and the two
affected OpenSpec capabilities. No public API, CLI syntax, report schema, or
scoring algorithm changes.
