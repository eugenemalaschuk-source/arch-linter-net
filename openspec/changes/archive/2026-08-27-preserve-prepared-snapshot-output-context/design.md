## Context

The current snapshot orchestration builds once during validation, then sends a boolean
`UsePreparedPostBuildState` to graph and baseline contributors. Those contributors create a
post-build runner by rediscovering project outputs from policy configuration. CLI output overrides
are not part of that rediscovery, and RID outputs are not represented by its ordinary output path.

## Goals / Non-Goals

**Goals:**

- Carry validation's exact receipt-verified artifact selection to every prepared snapshot
  contributor.
- Keep one graph build and fresh isolated runner contexts.
- Fail closed if an explicit prepared snapshot cannot materialize its selected artifacts.

**Non-Goals:**

- Change policy-default discovery for ordinary validation or other CLI commands.
- Share one mutable AssemblyLoadContext between validation, graph, and baseline collection.
- Change the build receipt format or ordinary non-RID build process invocation.

## Decisions

### Hand off the refreshed runner preparation

`ArchitectureRunnerPreparation` already is the immutable metadata-only representation of a
selected artifact closure: after ensure-built preflight, `PostBuildArtifactEvidenceRefresher`
updates its `ProjectDiscovery.ResolvedAssemblyPaths`, root selection, and captured content digests
to the requested configuration/framework/platform/RID artifacts. Expose that final preparation on
`ValidationOutcome` and carry it on graph and baseline-diff requests.

Prepared graph and baseline paths call `MaterializePreparedRunner` with this object. It re-verifies
the captured artifact digests and resolves an isolated runner from the retained exact paths; it
does not invoke project discovery. The existing `BuildRunnerForPostBuild` path remains for a
standalone request that itself asks to ensure-build.

Passing only output override values was rejected: it still delegates artifact choice to discovery,
does not express a concrete receipt-backed identity, and cannot correctly represent RID paths.

### Build RID output through a project-level driver

The .NET SDK rejects a runtime identifier passed to a solution build (NETSDK1134), including the
temporary `.slnx` graph used by ordinary ensure-built preparation. For a RID request, generate one
temporary MSBuild driver project instead. It restores and builds selected graph roots in one process
while passing the configuration/framework/platform/RID properties to each project-level MSBuild
invocation, where the SDK supports them. This preserves the selected dependency closure without
falling back to a non-RID output or issuing one build process per contributor.

### Keep the handoff explicit and fail closed

`UsePreparedPostBuildState` remains the opt-in signal, but it requires the matching preparation.
If materialization cannot use that selection (missing roots, changed content, or an invalid
preparation), the contributor fails before collecting facts; snapshot output remains unwritten.

### Test the real divergence

Extend the packaged ASP.NET fixture command to request `Release` while its policy remains at the
default `Debug`, and add a host-appropriate RID variant. Success proves that the isolated graph
and baseline paths consume the release/RID receipt-backed selection instead of policy rediscovery.

## Risks / Trade-offs

- [A selected artifact changes between validation and a contributor] → materialization verifies
  captured digests and fails closed.
- [Public request/outcome models expand] → deliberately update both reviewed Core API baselines.
- [RID test requires a host-specific identifier] → compute the current platform RID in the test;
  use an SDK fixture with no application-specific runtime assets and a project-level build driver.

## Migration Plan

No CLI syntax or snapshot schema changes. Existing callers that do not select prepared state retain
ordinary behavior. Rollback is a code revert; receipts and snapshots remain compatible.

## Open Questions

None.
