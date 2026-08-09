## Context

`BuildStatePreparationService` correctly builds selected projects, emits a
receipt, and re-verifies the resulting artifacts before validation continues.
Some contract families then use Buildalyzer to obtain MSBuild-evaluated source
and reference data. Its design-time invocation can remove or rewrite primary
outputs that the preparation pass just proved current, leaving a receipt that
does not correspond to a consumable build output.

## Goals / Non-Goals

**Goals:**

- Preserve the selected assembly, PDB, and adjacent primary outputs while
  obtaining Buildalyzer's project context.
- Preserve the exact project-aware source/reference information already used
  by method-body and framework-reference contracts.
- Keep explicit preparation receipt-backed and fail closed if output state no
  longer verifies.
- Exercise the public CLI and Testing API paths against real compiled
  fixtures.

**Non-Goals:**

- Replacing Buildalyzer or changing its dependency boundary.
- Accepting timestamp-only evidence, removing receipts, or relaxing preflight.
- Adding consumer rebuild wrappers or unrelated build-performance work.

## Decisions

### Preserve primary outputs during project-aware MSBuild evaluation

Each Buildalyzer caller in `Core.Discovery` snapshots the project's existing
primary artifacts immediately before its design-time evaluation. If Buildalyzer
removes or changes any of those artifacts, the evaluator restores the recorded
bytes before it returns. Restoration stages the bytes in a temporary file in
the artifact directory and replaces the artifact atomically, so consumers never
load a partial assembly or PDB.

The preservation boundary is shared by project-aware Roslyn resolution and
framework-reference evaluation. It retains their existing design-time
configuration, source/reference semantics, and restore behavior; it does not
infer a normal build from the presence of outputs.

### Retain the existing preparation ordering

`EnsureBuilt` continues to build once, write receipts only after post-build
evaluation succeeds, and re-evaluate receipts before returning. The resolver
change removes the mutation source; it does not weaken the existing check if
another process changes an artifact after verification.

### Verify at public surfaces with real artifacts

Focused fixtures will build an SDK project, record the assembly/PDB bytes, and
run the relevant CLI and Testing API workflows. Tests will verify that the
outputs survive unchanged when no source rebuild is required and that two
`WithEnsureBuilt()` validations can run sequentially in one process.

## Risks / Trade-offs

- **[Risk]** Buildalyzer can remove an output before the preservation boundary
  restores it. → **Mitigation:** restore before returning from evaluation, use
  atomic replacement, and verify real CLI and Testing API flows retain the
  original output bytes.
- **[Risk]** a non-Buildalyzer actor can still mutate outputs after preflight.
  → **Mitigation:** retain the receipt re-verification and TOCTOU protections.
- **[Risk]** changing design-time properties could alter returned references.
  → **Mitigation:** keep the existing design-time mode and assert successful
  context resolution in focused tests.

## Migration Plan

No configuration or data migration is required. The change is internal and
backward compatible: existing policies retain their current syntax and
preflight results, while successful explicit preparation no longer damages its
own verified outputs. Reverting the shared preservation boundary restores the
prior behavior if a compatibility issue is discovered.

## Open Questions

None.
