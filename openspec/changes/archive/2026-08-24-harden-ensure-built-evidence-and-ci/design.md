## Context

Metadata-only `--ensure-built` preparation deliberately precedes runner materialization so Windows
can replace a selected DLL. The metadata plan is currently retained only after all preflight work
succeeds, and build-state uses request-only output settings even though preparation selected paths
using policy configuration defaults. The Windows installed-tool oracle exists but is not assigned
to a PR packed-artifact shard.

## Goals / Non-Goals

**Goals:**

- Retain every successfully prepared metadata plan for cancellation/error provenance and collision
  protection.
- Ensure an effective output context identifies the same configuration/framework/platform/RID for
  graph build, resolution, manifests, and receipts.
- Execute and publish evidence for the installed Windows rebuild oracle in mandatory PR CI.

**Non-Goals:**

- Add policy keys, change public APIs, redesign receipt schema, or change ordinary validation
  semantics unrelated to policy-default output identity.

## Decisions

### Retain metadata preparation at its point of success

Assign `SnapshotConstructionState.Preparation` immediately after each successful `PrepareRunner`
call. Error and cancellation projections select materialized setup evidence when available and
otherwise derive repository root, selected/missing counts, project paths, artifact paths, and
receipt paths from the retained preparation. This preserves exactly the inputs already consumed and
keeps collision guards fail-closed.

Keeping only the final assignment is rejected because an exception between preparation and that
assignment irreversibly discards provenance.

### Resolve one effective build context before preflight

After policy composition, merge CLI request values over normalized policy defaults. The effective
configuration defaults to the document's `analysis.configuration` (or Debug); the effective target
framework defaults to `analysis.target_framework` when configured. Platform and RID remain
request-derived until policy gains defaults, but participate in the same context and constrain
prepared-path reuse. Pass the context to every preflight/build/manifest/receipt call and cache
context for the snapshot.

Passing only the CLI override to graph build is rejected: a policy-selected Release artifact can
otherwise be re-attested after a Debug build that did not produce it. Preserving any prepared path
when request fields are null is rejected for the same reason.

### Run the Windows oracle as its own packed-artifact shard

Expose a method-specific Make target and add it to the Windows packed-artifact matrix. Reuse the
existing candidate distribution, per-shard evidence directory, upload, and Windows fan-in, so the
new assertion contributes to the canonical required scenario union without changing stable check
names. macOS is excluded because the test is explicitly a Windows file-lock regression.

Folding it into package-and-entrypoints is rejected because scenario evidence would not identify
the lock/replacement proof independently.

## Risks / Trade-offs

- [Effective-context propagation changes receipt metadata for policy-default validation] → Use the
  same values that project discovery already used and cover a policy-selected Release rebuild.
- [Prepared error paths lack loaded assemblies] → Derive only metadata facts and receipts, never
  load an assembly while reporting the failure.
- [One additional Windows shard consumes runner time] → It is narrowly filtered and proves a
  release-blocking platform-specific condition.

## Migration Plan

1. Add focused unit/integration regressions for provenance and Release output replacement.
2. Route the existing installed-tool assertion through the Windows packed-artifact matrix.
3. Run focused tests, formatting, strict architecture lint, code-size lint, and OpenSpec checks.
4. Archive the change and update the existing PR without merging it.

## Open Questions

- None.
