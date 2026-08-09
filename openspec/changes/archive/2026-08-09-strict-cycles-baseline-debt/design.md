## Context

Cycle contracts construct a layer graph from type-reference evidence. The shared execution context
currently turns every non-ignored observation into a baseline candidate while that graph is being
constructed, before the cycle detector establishes whether the edge contributes to a violation.
Baseline verification separately classifies candidates but its sync verdict does not include the
new-candidate set.

## Goals / Non-Goals

**Goals:**

- Admit only evidence for directed layer edges that participate in a detected cycle as strict-cycle
  baseline candidates.
- Preserve the existing cycle detector's results, ordering, ignore behavior, and v2 structured
  candidate identity.
- Make the baseline sync verdict derive from every non-matched lifecycle category, including `new`.
- Verify Core collection and CLI human/JSON/exit behavior with acyclic and cyclic multi-layer cases.

**Non-Goals:**

- Change the policy schema, cycle output format, or the identities of other contract families.
- Alter cycle detection algorithms or optimize graph traversal generally.
- Retroactively rewrite existing baselines outside normal update/prune workflows.

## Decisions

### Defer strict-cycle candidate admission until graph evidence is classified

Cycle collection will retain per-reference candidate evidence locally without adding it to the
session's baseline output. After graph construction, an edge is eligible only when its target can
reach its source in the same graph; this is equivalent to that directed edge being part of a cycle.
Eligible evidence is then admitted with its existing exact identity.

This avoids parsing formatted cycle paths and leaves the detector untouched. Filtering only after
the detector's input graph is complete also prevents ordinary acyclic edges from becoming debt.

### Keep ignore matching and occurrence assignment unchanged

Evidence collection still calls the existing execution context, so ignored references remain
excluded from the graph and occurrence identities continue to be assigned before the ignore
decision. The narrowly scoped deferred-admission hook exists solely because a cycle contract cannot
know whether an observation is reportable until the complete graph is available.

### Compute verification sync from all unsynchronized states

`InSync` will require zero `new`, `resolved`, `stale`, and `ambiguous` entries. Human and JSON
formatters already consume the same Core outcome, so this makes their displayed counts agree with
the gate and exit code without adding formatter-specific policy.

## Risks / Trade-offs

- [A graph traversal for each candidate could be expensive] → Use a bounded reachability walk over
  the already-built small layer graph; no assembly/type scan is repeated.
- [An edge can have multiple underlying type references] → Preserve every exact non-ignored
  candidate on a cycle edge, matching existing edge-level ignore and baseline semantics.
- [Existing baselines contain formerly accepted acyclic edges] → Verification marks them resolved;
  normal prune/update workflows make their removal reviewable.
