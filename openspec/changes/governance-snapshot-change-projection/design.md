## Context

`ArchitectureHealthApplicationService` already evaluates strict and audit
from one snapshot and passes it to the debt gate. The legacy `change snapshot`
command then repeats validation, graph setup, and baseline candidate
collection in a new process. Removing that boundary is the next
orchestration-level optimization before Core algorithm work.

## Decisions

### Reuse a caller-owned snapshot, not persisted state

The Health command creates one `ArchitectureAnalysisSnapshot`, evaluates its
requested modes, verifies baseline debt from the retained candidate receipt,
and projects the change snapshot before disposing it. No runner/session
escapes the snapshot, so build-state provenance, cancellation, and load-context
disposal remain under Core authority.

Persisted prepared state is out of scope: it would introduce authorization,
identity, invalidation, and artifact-provenance contracts before the simpler
one-process alternative is exhausted.

### Preserve graph semantics while reusing preparation

The change projector still receives the same validation outcome, namespace
graph, assembly graph, and frozen baseline-debt entries. Graph projections run
the historical graph contract set (`includeAsmdefContracts: false`) against the
retained runner/session rather than calling the independent graph service.

### Keep public API verification separate

Public API verification has a baseline-independent reviewed-surface authority
and a distinct request shape. It is not silently folded into Health.

### Fail closed and retain canonical output

The shared path requires a complete Health receipt and successful baseline
candidate comparison before writing the change snapshot. A blocked preflight,
incomplete baseline, cancellation, or projection exception prevents
publication. The existing Core projector and serializer remain authoritative.

## Equivalence evidence

The Core test compares serialized `architecture-change-snapshot/v2` documents
from shared and independent paths and asserts one snapshot materialization.
The CLI integration test verifies a complete readable change snapshot while
ordinary Health output and exit semantics remain unchanged.
