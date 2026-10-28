# Proposal

## Why

After a partial GitHub Actions rerun, the successful Architecture Coverage check can point to a newly identified copy of a job whose original execution and artifact belong to an earlier attempt. The badge publisher currently treats the copied job's attempt as the producer execution, rejecting a valid manifest; resolving the execution lineage must preserve the existing strict provenance checks.

## What Changes

- Resolve the effective producer job and attempt from the selected check's exact run/job identity and trusted per-attempt job history.
- Treat matching valid execution timestamps across consecutive attempts as a copied execution, while keeping a genuinely re-executed job's own attempt and identity.
- Keep the resolved attempt and job identity consistent through artifact validation, producer identity hashing, and temporal receipt validation; fail closed on incomplete or contradictory history.
- Add deterministic regression coverage for copied jobs, consecutive partial reruns, genuine producer reruns, and invalid lineage.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `architecture-health-badge-promotion`: specify how trusted producer-attempt history resolves copied successful jobs while retaining exact provenance validation.

## Impact

Affected code is limited to the badge-promotion GitHub resolver and its focused tooling tests, with a narrow update to the existing promotion contract. No public .NET API, badge payload schema, workflow permission, publisher pin, or publication behavior is intended to change.
