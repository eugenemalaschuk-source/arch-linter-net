# AI-first semantic-role governance

Semantic roles make architecture policy understandable to AI agents: a type is
classified with a role such as `DomainLayer`, `ApplicationLayer`, or
`SharedKernel`, and optional metadata such as `domain`, `boundedContext`,
`feature`, `platform`, or `runtime`. The role is evidence from static analysis,
not hidden meaning inferred by the tool.

Use this page with the [semantic role catalog](../policy-format/semantic-role-catalog.md),
[semantic classification](../policy-format/semantic-classification.md), and
[policy review checklist](policy-review-checklist.md).

## Agent workflow

An agent adding or reviewing architecture code should follow this loop:

1. **Inspect facts.** Read the solution/project graph, target assemblies,
   namespaces, existing classification conventions, policy, and current
   validation output. Do not start from an ideal layer name.
1. **Classify the change.** Choose a reviewed role and metadata. Prefer an
   existing namespace, inheritance, or user-owned attribute convention. Record
   the evidence that supports the decision.
1. **Map the boundary.** Add a narrow selector or contextual contract only when
   the role is needed by a real rule. Add semantic-role coverage when the role
   must be governed and unmapped facts must be visible.
1. **Choose adoption mode.** Use audit contracts to discover migration debt and
   strict contracts for stable boundaries. Do not put known-failing future-state
   rules in strict mode just to make the policy look complete.
1. **Validate and explain.** Run the policy and inspect human-readable and JSON
   output. Review classification evidence, coverage deltas, new contexts,
   uncovered namespaces, and cross-context edges.
1. **Review the smallest change.** Prefer a code move or a narrow schema-backed
   policy edit. Every broad exception needs an explicit architectural reason
   and human review.

New code is architecture debt until it is classified, governed by a selector or
contextual contract, or explicitly excluded with a narrow reason. An agent must
not resolve uncertainty by guessing a role or adding a blanket ignore.

## Diagnostics for agents

When a role or boundary is reported, feedback should preserve these facts:

| Diagnostic fact | Agent action |
| --- | --- |
| Expected role or boundary | Compare the type with the contract and intended layer/context. |
| Actual role | Check whether it came from a type attribute, assembly attribute, inheritance, or namespace convention. |
| Metadata and context | Check exact `domain`, `boundedContext`, `feature`, `platform`, or `runtime` values. |
| Evidence | Inspect the matched attribute, base type, or namespace pattern; do not infer missing evidence. |
| Coverage state | Treat `uncovered`, `unknown`, `stale`, and conflict evidence as governance work, not permission to bypass the rule. |
| Suggested action | Prefer a code move, classification correction, narrow selector, or reasoned exclusion. Never auto-weaken the policy. |

For JSON and CI artifact feedback, keep the same meaning as human output:
`classification_roles` identifies the classified subject, role, metadata, source,
and evidence. An agent reviewing a large change should compare the before/after
sets of roles, contexts, uncovered namespaces, and cross-context edges rather
than looking only at the final exit code.

These diagnostics are static-analysis evidence. They do not prove runtime DI
resolution, runtime behavior, security correctness, runtime monitoring, or
semantic data flow.

## Safe governance patterns

### New feature

For a new `Inventory` feature, inspect the actual namespace and then classify it
with the repository's existing convention. A selector can combine namespace and
role, making the boundary explainable:

```yaml
classification:
  namespace:
    - namespace: Acme.Inventory.Application
      role: ApplicationLayer
      metadata: { boundedContext: Inventory, feature: Inventory }

layers:
  inventory-application:
    namespace: Acme.Inventory.Application
    selector:
      role: ApplicationLayer
      metadata: { boundedContext: Inventory }
```

Verify that the namespace exists, the selector matches the intended types, and
the role is consumed by a real contract or coverage rule. Do not add a broad
`Acme.*` selector or unrestricted pattern.

### New bounded context

For a new `Sales` or `Inventory` context, classify its roles and constrain
cross-context dependencies directly:

```yaml
contracts:
  strict_context_dependencies:
    - id: sales-no-inventory
      name: sales-must-not-depend-on-inventory
      source:
        role: DomainLayer
        metadata: { boundedContext: Sales }
      forbidden:
        - role: DomainLayer
          metadata: { boundedContext: Inventory }
      reason: Sales and Inventory communicate through reviewed contracts.
```

Use exact metadata and the smallest selectors that express the boundary. If a
context is still migrating, use the audit equivalent to reveal edges before
making the rule strict.

### Shared kernel exception

A shared kernel is a named architectural exception, not a general-purpose
shared folder. Classify it explicitly and keep the allowed relationship narrow:

```yaml
classification:
  namespace:
    - namespace: Acme.SharedKernel
      role: SharedKernel
      metadata: { stability: stable }

layers:
  shared-kernel:
    namespace: Acme.SharedKernel
    selector: { role: SharedKernel }

contracts:
  strict_context_allow_only:
    - id: sales-domain-own-context-or-shared-kernel
      name: sales-domain-may-depend-only-on-own-context-or-shared-kernel
      source:
        role: DomainLayer
        metadata: { boundedContext: Sales }
      allowed:
        - role: DomainLayer
          metadata: { boundedContext: Sales }
        - role: SharedKernel
      reason: Sales may use its own domain and the explicitly governed SharedKernel only.
  strict_coverage:
    - id: sales-inventory-shared-kernel-semantic-coverage
      name: sales-inventory-shared-kernel-semantic-coverage
      scope: semantic_role
      roots:
        - namespace: Acme
      reason: Every discovered Sales, Inventory, or SharedKernel role must be governed or explicitly excluded.
```

The allow-list is the safe exception: it permits only the source context and
the explicitly governed `SharedKernel`, with a reason that identifies the
architectural intent. The surrounding policy should also identify owners,
responsibility, and stability. Do not turn `SharedKernel` into an unrestricted
target or use it to hide a new helper with unclear ownership. The semantic-role
coverage contract makes a newly discovered Sales, Inventory, or SharedKernel
fact visible until a selector or contextual contract governs it.

### Legacy migration

For a legacy area, begin with a narrow audit convention and inspect its output.
Move types or add explicit user-owned attributes as they are reviewed; then
replace the audit boundary with a strict boundary when it passes without new
debt. Keep migration exceptions exact, reasoned, and linked to remaining work.
A baseline freezes known debt; it must not hide new violations.

## Layout examples

### Sales, Inventory, and SharedKernel

The modular-monolith shape is governed by three distinct facts:

- `Sales` and `Inventory` receive their own `domain`/`boundedContext` metadata.
- `SharedKernel` is selected explicitly and kept small.
- Contextual dependency or allow-only contracts constrain cross-context edges;
  semantic coverage exposes roles that no contract consumes.

Review against the actual assembly and namespace inventory, not these names
blindly. A role that matches zero types, stale selectors, missing metadata, and
uncovered roles are all review signals.

### Unity/client-style growth

Use namespace, inheritance, and metadata facts for a fast-growing client layout:

```yaml
classification:
  namespace:
    - namespace: Game.Gameplay
      role: Feature
      metadata: { feature: Gameplay, platform: Unity, runtime: player }
    - namespace: Game.Gameplay.Systems
      role: System
      metadata: { feature: Gameplay, platform: Unity, runtime: player }
    - namespace: Game.Editor
      role: UnityEditor
      metadata: { platform: Unity, runtime: editor }
    - namespace_suffix: ViewModels
      role: ViewModel
      metadata: { platform: Unity }
    - namespace_suffix: Views
      role: View
      metadata: { platform: Unity }

layers:
  gameplay-feature:
    namespace: Game.Gameplay
    selector:
      role: Feature
      metadata: { feature: Gameplay, platform: Unity, runtime: player }
  gameplay-systems:
    namespace: Game.Gameplay.Systems
    selector:
      role: System
      metadata: { feature: Gameplay, platform: Unity, runtime: player }
  editor-tooling:
    namespace: Game.Editor
    selector:
      role: UnityEditor
      metadata: { platform: Unity, runtime: editor }

contracts:
  strict_context_dependencies:
    - id: unity-player-no-editor-tooling
      name: unity-player-must-not-reference-editor-tooling
      source:
        role: System
        metadata: { platform: Unity, runtime: player }
      forbidden:
        - role: UnityEditor
          metadata: { platform: Unity, runtime: editor }
      reason: Player systems must not reference editor-only tooling.
```

`.asmdef` contracts can validate assembly-definition references, while semantic
roles explain the feature and runtime/editor context boundaries. Neither
inspects scenes, runtime composition, gameplay behavior, DI resolution, or
security correctness.

## Review guardrails

- Use only fields and contract families documented by the current schema.
- Prefer exact selectors and full-segment namespace globs; semantic selectors
  are exact and AND-combined.
- Treat a conflict, missing evidence, empty selector match, stale selector, or
  uncovered role as a fact to resolve.
- Require a concrete reason for every broad override or exclusion, and keep
  exceptions narrower than the boundary they protect.
- Review generated policy suggestions as patches. They are never authorization
  to edit production code or weaken a strict rule automatically.
- Keep this workflow static-analysis-only and retain human PR review.
