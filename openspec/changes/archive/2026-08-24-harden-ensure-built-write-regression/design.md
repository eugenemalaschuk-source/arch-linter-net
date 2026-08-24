## Context

Issue #639 moved uncached ensure-built through metadata-only preparation so the temporary graph
build runs before the selected artifact is materialized. The initial regressions only exercised
no-op builds, however: an existing output could remain untouched and conceal a Windows sharing
violation. The prepared-output resolver also has acceptance and fallback branches that need
coverage independent of a slow package fixture.

## Goals / Non-Goals

**Goals:**

- Force the selected `ArchLinterNet.Testing.dll` to be replaced in the installed-tool regression
  and prove its receipt binds the replacement bytes.
- Make the source-CLI test select the exact fixture output before build and force it stale.
- Cover every prepared-path acceptance/fallback decision with fast tests.

**Non-Goals:**

- Change build-state production behavior, receipts, cache behavior, or policy syntax.
- Replace the existing full packed-candidate release gate with a new package harness.

## Decisions

### Force a real output replacement

Each regression first builds its fixture, records the selected Debug DLL digest, modifies a compiled
source file, and sets its last-write timestamp after the recorded DLL. The policy provides the
Debug output through `analysis.assembly_search_paths`, so the old pre-fix setup would load exactly
the DLL that the child graph build must replace. After `--ensure-built --no-restore`, the test checks
that the DLL digest changed and the receipt's digest equals the new bytes.

Appending a source member rather than changing project metadata makes the rebuild portable and
keeps the fixture's policy valid. It also avoids relying only on timestamp-based incremental-build
heuristics because MSBuild sees changed compile content.

### Expose only the path-selection seam required for tests

The resolver's helper stays private. Fast tests invoke the existing internal post-build resolution
path with a fixture project graph and resolved-path map, asserting the chosen `ResolvedAssemblyPaths`
value. This tests observable behavior without widening the public or production API.

### Keep explicit requests authoritative

Tests verify that prepared-path reuse applies only when configuration, target framework, and RID
are all unspecified. Explicit values continue through existing constrained output lookup.

## Risks / Trade-offs

- [Filesystem timestamp granularity hides a stale write] → set the modified source timestamp to a
  value after the recorded DLL and also change source content.
- [Windows-only lock behavior is untested elsewhere] → mark the lock oracle Windows-only while
  preserving portable branch tests for resolver behavior.
- [Packed candidate remains slow] → retain it as release-gate coverage and keep path-selection
  cases in fast Core tests for coverage gates.

## Migration Plan

No migration is required. These are regression and coverage tests for existing behavior; rollback
reverts the tests and this OpenSpec change.

## Open Questions

None.
