## Context

The coverage engine already classifies namespaces, projects, assemblies, dependency edges, and rule inputs. Semantic classification is also available through a per-run lazy role index, selector-backed layers, contextual dependency/allow-only contracts, and conflict/metadata diagnostics. The missing behavior is an integration that explains semantic coverage without duplicating discovery or changing existing coverage scopes.

## Goals / Non-Goals

**Goals:**

- Add `scope: semantic_role` to the existing coverage contract family.
- Evaluate discovered role/metadata facts against selector-backed layers and registered contextual consumers.
- Report covered, excluded, uncovered, stale, unknown, and conflicting semantic facts with deterministic evidence.
- Keep human, JSON, and CI output additive and distinguish coverage findings from dependency violations.

**Non-Goals:**

- No new role-discovery source, data-flow analysis, runtime behavior validation, or replacement of existing coverage scopes.
- No implementation of reserved `classification.path`, `overrides`, or `exclusions` execution.
- No speculative policy syntax beyond the existing coverage contract and exclusion shapes.

## Decisions

1. **Reuse the existing coverage family and add one discriminant.** `semantic_role` uses the same strict/audit lists, severity, ignored-violation, summary, and output plumbing as other coverage scopes. This avoids a parallel diagnostic vocabulary and preserves non-semantic behavior.

2. **Use the session role index as the source of truth.** Semantic units are the classified types and their resolved `(role, metadata)` descriptors. The index is already per-run and cached, so coverage does not rescan assemblies or invoke extraction again.

3. **Define governance as selector or contextual consumption.** A classified fact is covered when a selector-backed layer matches it or a registered contextual contract references its role/metadata key. A selector with no matching current fact is stale. Contextual references are registered at session construction so coverage ordering and contract filtering cannot hide them.

4. **Keep semantic evidence separate from dependency findings.** Semantic coverage uses the existing `ArchitectureCoverageSummary` buckets and coverage findings with semantic-specific status text/fields. Classification conflicts and metadata failures remain in their existing classification sections and are linked as unknown/conflicting evidence rather than emitted as dependency violations.

5. **Require explicit, reasoned exclusions.** Semantic exclusions identify a role and optional metadata constraints using the existing exclusion object extension; every exclusion requires a non-empty reason. Matching is exact and deterministic, consistent with selector semantics.

6. **Treat empty configured selectors as stale.** A selector-backed layer or contextual semantic selector that matches no current classified fact contributes stale evidence. Undeclared referenced layers/roles contribute unknown evidence. This makes policy drift visible without treating an empty repository as an error.

## Risks / Trade-offs

- [Risk] Existing policies could change behavior if semantic coverage were implicitly enabled. → Semantic coverage is opt-in only when a `semantic_role` contract is declared.
- [Risk] A role can be consumed by a selector but not by a contract selected for the current run. → Register consumers from the full document, matching existing configuration-reference collection, and document that governance is policy-level rather than execution-selection-level.
- [Risk] Metadata constraints can be difficult to render consistently. → Reuse exact metadata comparison and serialize canonical role/metadata evidence in ordinal order.
- [Risk] The issue asks for broad sample fixtures. → Keep fixtures focused on the existing test model and add one representative Sales/Inventory/SharedKernel and one Unity/client convention policy sample without changing unrelated architecture tests.

## Migration Plan

No data migration is required. Existing policies remain unchanged until they declare `strict_coverage` or `audit_coverage` with `scope: semantic_role`. Policy authors can add reasoned exclusions or baseline entries for existing semantic debt, then enable error severity through the existing `analysis.coverage` setting.

## Open Questions

None blocking implementation. Reserved path-convention classification remains outside this change and continues to report deferred support according to its existing behavior.
