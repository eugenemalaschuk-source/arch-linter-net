## Context

See [proposal.md](proposal.md). The former scanner nested its complete traversal and mutable
evidence state in `Walker`, then split accessor handling through a second partial declaration.
It must continue to provide one deterministic result per selected root without changing the
existing evidence model or consumers.

## Goals / Non-Goals

**Goals:**

- Give shared result construction, recursive type-shape traversal, member/accessor traversal,
  and compiled attribute metadata explicit internal ownership.
- Retain every path, reflection-failure reason, identity, ordering, and cycle-protection rule.
- Eliminate both partial-declaration debt entries without replacing them with another partial
  aggregate.

**Non-Goals:**

- Change the visible-surface shape, policy/schema behavior, diagnostics, cache identity, or
  public Core.Scanning API.
- Add a second scan, result model, reflection engine, or recursion/deduplication authority.

## Decisions

### Create one shared scan state per root

`ArchitectureContractSurfaceExposureScanState` owns the root target/path, evidence collections,
deduplication, referenced-type map, branch-cycle identities, reflection-safe read recording, and
final deterministic ordering. The facade creates it once and only collaborators receive it. This
keeps incomplete evidence and canonical result construction impossible to diverge by traversal
site; separate state per collaborator was rejected because it would fragment cycle protection and
deduplication.

### Split by reflected evidence responsibility

`ArchitectureContractSurfaceExposureTraversal` owns declared-type relationships, generic
parameters/constraints, type shapes, delegates, and nested types.
`ArchitectureContractSurfaceExposureMemberScanner` owns visible member selection, accessors,
parameters, returns, and method generic parameters. `ArchitectureContractSurfaceExposureAttributeScanner`
owns compiled attribute data, typed arguments, and stable occurrence order. Collaborators recurse
only through the traversal and report all evidence through shared state. Mechanical file splitting
or a generic reflection framework was rejected because neither establishes an ownership boundary.

### Keep the facade and all helpers internal and non-partial

The existing scanner remains the narrow static entry point, preserving every call site. All new
types are internal, single-declaration collaborators; the exact reviewed ignores are removed in
the same change. A public seam was rejected because callers need only the established result, not
implementation-specific traversal services.

## Risks / Trade-offs

- [Relocation changes a path or reflection-failure boundary] → Preserve method order and the
  existing focused recursive, accessor, attribute, cycle, and incomplete-evidence tests.
- [Collaborators introduce competing state] → Construct state only in the facade and ensure all
  collaborators receive the same instance.
- [The cleanup only moves the aggregate] → Keep each collaborator below the code-size warning
  threshold and verify the real partial-declaration policy after removing the ignores.

## Migration Plan

1. Introduce non-partial state, traversal, member/accessor, and attribute collaborators; reduce
   the facade and delete the partial accessor fragment.
2. Remove the exact self-policy ignores and mark the #775 subtask in the active umbrella change.
3. Run focused index/consumer regressions plus formatting, code-size, architecture, public-API,
   and OpenSpec validation; archive this no-delta scoped change while keeping the umbrella active.

Rollback is a normal revert of this internal refactor and restores the former implementation and
its reviewed policy entries together.
