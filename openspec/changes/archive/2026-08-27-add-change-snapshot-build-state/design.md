## Context

`change snapshot` currently obtains validation facts, namespace and assembly
graphs, and optional baseline-debt facts through separate Core requests. The
validation request already supports metadata-first `--ensure-built` preparation,
but graph and baseline-diff requests only use ordinary assembly resolution. An
ASP.NET Core consumer therefore succeeds for validation and then fails when the
snapshot's graph or baseline contributor resolves its framework-dependent types.

The established #486 baseline-verify implementation demonstrates the required
post-build sequence: collect metadata and preflight evidence, perform an explicit
build, then materialize a fresh isolated runner whose opted-in shared-framework
probing paths are available. The normal no-option path must remain non-building.

## Goals / Non-Goals

**Goals:**

- Provide `change snapshot` with the same explicit build-state selection used by
  the existing post-build command paths.
- Apply the selected preparation mode and output context consistently to every
  analysis that contributes to a snapshot.
- Preserve snapshot schema, deterministic identity, strict/audit mode behavior,
  and the separate report-only command.
- Prove the packaged CLI works with the real ASP.NET Core fixture without a
  consumer runtimeconfig, NuGet-cache probing path, or `dotnet exec` wrapper.

**Non-Goals:**

- Changing `analysis.shared_frameworks` discovery, policy schema, or the
  non-isolated ordinary-resolution behavior.
- Making unrelated graph or baseline CLI commands expose new build-state flags.
- Replacing the existing snapshot projection or report comparison model.
- Adding a new public facade or a second build-state implementation.

## Decisions

### Reuse the established six-option build-state surface

`change snapshot` will accept `--ensure-built`, `--no-restore`,
`--configuration`, `--framework`, `--platform`, and `--runtime`. Although #670
requires the first three selection controls at minimum, the existing command
conventions expose all output-context dimensions together. Keeping that surface
aligned prevents a snapshot from validating one configuration and projecting a
different graph or baseline state.

### Forward options to every contributing Core request

The change command will map its typed options to validation, namespace graph,
assembly graph, and optional baseline-diff requests. Graph and baseline-diff
request models will gain the existing preparation/output-context properties, so
callers can select the supported path without a CLI-only workaround. This is an
additive Core public API change and will be reflected in reviewed API evidence.

### Use the existing post-build runner sequence in Core

When `EnsureBuilt` is selected, graph and baseline candidate collection will use
their existing runner/preflight seams to identify metadata before the build, then
materialize a fresh post-build isolated runner after verification. They will reuse
the canonical `BuildStatePreflightRunner` and `IBuildStatePreparationService`; no
parallel build process or resolver is introduced. Ordinary requests retain their
present no-build setup path.

An alternative that only forwards `--ensure-built` to validation is rejected:
the snapshot still creates graph and optional baseline facts using ordinary
resolution, reproducing the ASP.NET Core load failure. Replacing snapshot
projection with a new all-in-one analysis facade is also rejected because it
broadens the public design beyond this focused parity fix.

### Add end-to-end evidence at the packaged CLI boundary

The existing `aspnet-host` fixture already models the consumer and runs the
built CLI as a separate `dotnet` process. A `change snapshot` acceptance test
will invoke that artifact with `--ensure-built --configuration Debug --framework
net10.0`, deserialize the output, and assert success. Focused handler and Core
tests will additionally verify option forwarding and post-build routing,
including the optional baseline contributor.

## Risks / Trade-offs

- [Several snapshot contributors have separate executions] → each receives the
  same explicit output context and post-build runner contract; the regression
  test verifies the complete artifact rather than only validation.
- [Additive request properties alter the reviewed Core API] → update approval
  evidence and run the repository's read-only API check.
- [The packaged acceptance test depends on an installed ASP.NET Core runtime] →
  reuse the existing cross-platform fixture and `analysis.shared_frameworks`
  contract already maintained for that runtime requirement.
- [Build-state errors could otherwise permit partial facts] → retain existing
  fail-closed preflight behavior and ensure every snapshot contributor receives
  the same preparation selection.

## Migration Plan

The change is additive. Existing `change snapshot` commands retain ordinary,
non-building behavior. Consumers needing framework-dependent analysis add
`--ensure-built` and, where needed, their explicit configuration/framework
selection. Rollback is a normal code revert; persisted snapshot artifacts remain
readable because their schema and identity model do not change.

## Open Questions

None.
