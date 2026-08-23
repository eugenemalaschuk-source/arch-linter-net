## Context

`ArchitectureChangeSnapshot` is a Core-owned, versioned persisted contract, but
`ChangeCommandHandler.BuildSnapshot` currently decides which analysis facts become entries and
how project, edge, role, context, coverage, finding, and baseline-debt identities are formed.
The handler also owns legitimate CLI duties: validating options and output collisions, invoking
the runtime, writing artifacts, and presenting exceptions as exit codes. Core already exposes
internals to both the CLI and Core test assembly.

## Goals / Non-Goals

**Goals:**

- Put the deterministic complete-analysis-to-snapshot transformation behind an internal
  `ArchLinterNet.Core.Change` seam.
- Preserve every v2 serialized identity, ordering, schema field, and comparison result for the
  same authoritative inputs.
- Keep the CLI handler as the transport and orchestration boundary.
- Characterize projection behavior directly in Core while retaining CLI command regression tests.

**Non-Goals:**

- Changing the snapshot schema, serialized JSON shape, report comparison, validation, graph,
  classification, coverage, or baseline semantics.
- Adding a supported public API, a new dependency, filesystem/console behavior in Core, or a
  broad command-handler refactor.
- Encoding this implementation detail in self-policy when the existing Core/CLI directional
  boundary already governs the assemblies.

## Decisions

1. **Use one internal Core projector for all canonical fact mapping.**
   `ArchitectureChangeSnapshotProjector` receives the existing `ValidationOutcome`, namespace
   and assembly graph outcomes, frozen `ArchitectureBaselineComparisonEntry` values, mode, and
   condition-set name. It produces the existing snapshot records using the current canonical
   identities. An internal static seam is sufficient because no external consumer requirement
   exists, while the established `InternalsVisibleTo` entries allow the CLI and Core tests to use
   it.

   Alternative considered: make the projector public. Rejected because it would expand the
   supported Core surface for a refactoring-only requirement.

2. **Move baseline identity conversion with the rest of projection.**
   The CLI will pass frozen baseline comparison entries unchanged; the Core projector will require
   their authoritative identities and serialize them exactly as before. This avoids leaving one
   canonical snapshot fact in the CLI.

   Alternative considered: keep conversion in `ChangeCommandHandler`. Rejected because
   baseline-debt identity is part of the snapshot fact model.

3. **Keep snapshot creation orchestration in the handler.**
   The handler remains responsible for mode and output validation, runtime calls, input-collision
   checks, artifact serialization and writing, and exception-to-exit-code behavior. It will only
   delegate projection after it has gathered the authoritative inputs.

4. **Test behavior at the owning boundary.**
   Core tests will assert project path, graph edge, role/context, coverage blind-spot, normalized
   finding, and frozen baseline identity projection. CLI tests retain output/collision and command
   orchestration coverage. Existing serialized snapshot and report comparison tests remain the
   compatibility regression.

## Risks / Trade-offs

- [Moving a helper can subtly change identity strings or enumeration order] → retain the existing
  transformation verbatim, add Core characterization tests, and run serialization/comparison
  regressions.
- [An internal seam becomes a de facto general analysis API] → keep the type `internal`, limit
  its input set to the existing command's authoritative results, and add no public wrapper.
- [Handler cleanup accidentally removes I/O safeguards] → leave option, collision, write, and
  error code paths in place and run the focused CLI tests.

## Migration Plan

This is source-compatible and artifact-compatible. Build the Core projector, replace the handler
mapping with one call, relocate behavior tests, and validate the unchanged v2 serialization and
comparison behavior. A revert restores the previous in-handler mapping without data migration.

## Open Questions

None.
