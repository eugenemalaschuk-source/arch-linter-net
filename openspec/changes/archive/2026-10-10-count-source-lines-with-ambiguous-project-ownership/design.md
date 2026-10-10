## Context

See proposal.md for the user-visible problem and specs/repository-metrics-observability/spec.md for the required behavior. `ArchitectureSourceFileFactTraversal` currently resolves project ownership before reading a file, so a readable file under an explicit source root is omitted from both the physical size inventory and source declaration index when ownership is ambiguous.

## Goals / Non-Goals

**Goals:**
- Count physical source files and lines from the configured source-root inventory in the existing single traversal.
- Keep assembly ownership as a prerequisite for source declarations and type-to-source correlation.
- Preserve generated-file exclusion, root boundaries, unreadable-file evidence, and cross-root deduplication.

**Non-Goals:**
- Change how projects or source roots are discovered.
- Infer project ownership from file contents or select an assembly by discovery order.
- Change architecture findings, source enrichment, or public APIs.

## Decisions

- Perform generated-file filtering and source reading before the ownership-dependent parsing branch. Record normalized file identity and physical line count for every readable file under the configured roots.
- Parse declarations into the source fact index only when one owning target assembly is known. If ownership is missing or ambiguous, retain the physical size count but do not add declarations or type ownership.
- Keep file counts in the existing normalized-path dictionary so overlapping roots remain deduplicated. Reuse the same read and scan pass; do not add a second filesystem traversal or MSBuild load.
- Add a regression test with two root-level discovered projects and a readable C# file under the configured source root. Assert that physical metrics count it while source declarations remain unowned.

## Risks / Trade-offs

- A broad configured source root can include readable C# files that are not compiled into selected projects. This is intentional for physical repository-size metrics, while callers retain control over scope through explicit source roots.
- Source file reads now contribute to size metrics even when no type facts can be attributed. The same generated-file filter, unreadable-input tracking, and root containment checks continue to apply.

## Migration Plan

No migration is required. Existing policies keep their configured source-root scope; source lines and file counts become physical inventory values for that scope.
