## Context

The current line has published `v0.9.1` and `v0.10.0-preview.2`; the issue identifies this as real-adoption correctness work in the current preview. It describes a loader collision between the CLI's already-loaded `Microsoft.Extensions.DependencyInjection.Abstractions` 9.x and a consumer's 10.x dependency, plus incomplete type observations that can still appear successful.

Core already has a metadata-only prepared-artifact path. `--use-prepared-receipts` verifies current receipts before runner materialization, then resolves the exact selected consumer outputs in an isolated assembly load scope. The standalone Health service omits the internal selection when it creates its snapshot. Topology capture has preparation fields but no prepared-receipt selector; diff and verify share validation options without that selector. `ArchitectureTypeIndex` already records whether reflection produced a complete type universe, but topology observation currently ignores that state.

An active change, `governance-snapshot-change-projection`, covers same-process sharing of an immutable snapshot. This change uses producer-published receipts across independent reader processes and does not extend that snapshot feature or introduce persisted cache state.

## Goals / Non-Goals

**Goals:**

- Preserve `--use-prepared-receipts` through standalone Health analysis.
- Support the same no-build, no-restore selector on topology capture, diff, and verify.
- Continue using existing receipt verification and exact-artifact isolated loading.
- Prevent topology operations from accepting a partial type universe as a complete observation.
- Prove packaged CLI producer/reader behavior, semantic parity, complete type subjects, and unchanged selected artifact bytes.
- Keep command help and topology guidance aligned with the supported reader path.

**Non-Goals:**

- Add a new loader, cache, receipt schema, or persistent analysis store.
- Change `--ensure-built` producer behavior or make ordinary commands build implicitly.
- Rewrite reflection scanning generally or change non-topology best-effort scanning behavior.
- Change topology or Health public output schemas, identities, strict/audit semantics, or Core public API.
- Change package pins, add a shared-framework dependency to the consumer fixture, or copy/replace assemblies.

## Decisions

### Reuse the existing prepared-receipt execution path

The topology commands and standalone Health snapshot SHALL set the existing internal `UsePreparedArtifacts` request selection. Core then performs its current metadata preparation and receipt preflight before it materializes the selected runner in the isolated load scope. No new loader or receipt authorization is needed.

This preserves the producer/reader boundary: one `--ensure-built` command creates and verifies receipts; separate Health or topology invocations verify those receipts and inspect the same output bytes without build or restore. A stale, missing, or unverifiable receipt remains a blocking preflight result.

The alternative of running `--ensure-built` on each reader is rejected because it starts a graph build for every projection even when the selected output bytes have not changed. Ordinary process-wide `Assembly.Load` resolution is also rejected for this supported receipt path because it can bind consumer types against assemblies already loaded by the CLI.

### Keep prepared selection internal to Core requests

The CLI may expose `--use-prepared-receipts`, but Core request plumbing will use the existing internal selection flag. No public Core property or public API snapshot change is required.

### Fail topology operations when the type universe is incomplete

Topology observation SHALL check `ArchitectureTypeIndex.HasCompleteTypeUniverse` before returning subjects or relationships. If reflection could only recover part of an assembly's type set, capture, diff, and verify will fail through the existing analysis error path rather than publish or accept a partial topology result. Other scanner callers and repository-metric completeness behavior remain unchanged.

### Verify through an installed candidate package

Extend the packed-candidate acceptance harness with a small consumer fixture referencing `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.11. It will prepare once, then run Health and topology readers in independent installed-tool processes with prepared receipts. Assertions will cover current preflight, exact subject parity for both the property and interface fixtures, strict/audit parity, no reader build/restore, and unchanged project/dependency DLL and receipt hashes. Missing/stale receipt cases will remain fail-closed.

## Risks / Trade-offs

- A topology invocation that previously hid a reflection-load problem behind a partial successful result will now fail. This exposes an existing false-success condition and is necessary for trustworthy review evidence.
- The package-level fixture adds subprocess and fixture-build time. Keep it bounded and focused in the existing packed CLI harness; leave broader repository acceptance to the normal risk-based workflow and CI.
- A local run can prove only the current host's package and loader behavior. The packed regression should run on the existing Windows, Linux, and macOS CI matrix.

## Migration Plan

No migration is required. Existing `--ensure-built` usage remains valid. After one successful preparation, topology readers may add `--use-prepared-receipts`; Health already accepts the flag and will honor it consistently with or without `--change-snapshot`.

## Open Questions

None. The issue supplies the consumer fixture, expected loader versions, receipt invariants, and command matrix.
