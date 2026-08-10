## Context

`layers.<name>.namespace` already supports a constrained, segment-based glob grammar via `NamespaceGlobPattern` (`ArchLinterNet.Core.Resolution`): literal `.`-delimited segments plus a whole-segment `*` wildcard, with `**`, `?`, `[...]`, partial-segment wildcards, and leading/bare `*` all rejected at parse time with an actionable message.

`allowed_only_in_namespaces` (composition, attribute-usage, interface-implementation contracts), `forbidden_in_namespaces` (attribute-usage, interface-implementation contracts), and `must_reside_in_namespaces` (type-placement contracts) are separate `List<string>` fields matched by a single shared helper, `IsAllowedLocation` (`ArchitectureAnalysisSession.TypePlacement.cs`), which calls `ArchitectureLayerResolver.MatchesPrefix` — a literal `StartsWith("." )`/equality check with no glob support at all. A `*` in one of these fields is not a syntax error; it is just a namespace character no compiled .NET type will ever have, so the entry silently never matches.

`MatchesPrefix` is also used by ~8 unrelated call sites (package id / framework reference / `ArchitectureTypeMatcher.Namespace` selector matching / `InheritanceChecker.SourceNamespaces` / `PolicyConsistency` ancestor-namespace resolution / `ArchitectureTypeIndex`). None of those are namespace *allowance* fields the issue is about, and none of them have a documented glob grammar to reuse — they are out of scope.

## Goals / Non-Goals

**Goals:**
- `allowed_only_in_namespaces`, `forbidden_in_namespaces`, and `must_reside_in_namespaces` support the same constrained glob grammar as `layers.<name>.namespace`, with identical matching semantics.
- Literal (non-glob) entries in these fields keep byte-for-byte current matching behavior.
- An entry using unsupported wildcard syntax fails policy load with a diagnostic naming the contract, the field, the exact pattern, and the grammar rule violated — never a silent no-op.

**Non-Goals:**
- No unrestricted regular expressions or new wildcard syntax (`**`, `?`, `[...]` stay rejected).
- No change to `layers.<name>.namespace`/`namespace_suffix` matching itself.
- No change to `ArchitectureTypeMatcher.Namespace` (the `types_matching`/`exclude_types_matching` selector field) or any of the other `MatchesPrefix` call sites listed above — those match a different kind of field (a type *selector*, not a namespace *allowance*) and are not part of this finding.
- No JSON schema change — these fields are already unconstrained `stringList`; grammar is a Core runtime concern exactly as it already is for `layers.<name>.namespace`.

## Decisions

**Reuse `NamespaceGlobPattern` directly, via a new `ArchitectureLayerResolver.MatchesNamespacePattern(namespaceName, pattern)` helper.**
`MatchesNamespacePattern` parses `pattern` with `NamespaceGlobPattern.Parse` and returns `.Match(namespaceName).Matched`. For a literal pattern (no `*`), `NamespaceGlobPattern.Match` walks `.`-split segments and requires the namespace to have at least as many segments, each literal segment equal — which is exactly the boundary `MatchesPrefix` already enforced (`"A.B".StartsWith("A.B.")` vs exact-equals). So swapping the matcher is behavior-preserving for every existing literal policy. Considered: writing a bespoke prefix-plus-wildcard matcher local to `IsAllowedLocation` — rejected, since it would be a second implementation of the same grammar `NamespaceGlobPattern` already owns, risking drift (exactly the kind of inconsistency this issue is fixing).

**Route only the shared `IsAllowedLocation` helper, not `MatchesPrefix` itself.**
`IsAllowedLocation` is the single call site behind all three affected fields (`allowedNamespacePrefixes` parameter, called from Composition/AttributeUsage/InterfaceImplementation for `AllowedOnlyInNamespaces`/`ForbiddenInNamespaces`, and from TypePlacement for `MustResideInNamespaces`). Changing this one function's namespace-prefix argument to route through `MatchesNamespacePattern` covers all three fields with one edit and touches none of `MatchesPrefix`'s other unrelated callers. Considered: changing `MatchesPrefix` itself — rejected as out of scope and higher blast radius (package ids and framework names are not namespaces and must never be glob-parsed).

**Validate eagerly at policy load, mirroring `LayerNamespacesValidator`.**
`LayerNamespacesValidator` already eagerly forces `layer.GlobPattern`/`exclusion.GlobPattern` evaluation during `Validate()` so a malformed layer namespace fails at load time with full contract context, catching `InvalidNamespacePatternException` and rethrowing as `InvalidOperationException` with the layer name prefixed. The four contract-family validators (`CompositionValidator`, `AttributeUsageValidator`, `InterfaceImplementationValidator`, `TypePlacementValidator`) each already validate their own contract's fields; add one shared helper, `PolicyDocumentValidatorSupport.ValidateNamespacePatterns(contractLabel, fieldName, entries)`, that each of the four calls once per relevant field. It parses every entry with `NamespaceGlobPattern.Parse`, and on `InvalidNamespacePatternException` throws `InvalidOperationException($"{contractLabel} {fieldName} entry '{pattern}': {ex.Message}")` — reusing the exact grammar-rule wording `NamespaceGlobPattern` already produces (e.g. "Bare wildcard '*' is not allowed...", "Partial segment wildcard '...' is not allowed..."). Considered: one new top-level validator added to `ArchitecturePolicyDocumentValidatorPipeline` that scans every contract family generically — rejected as a bigger structural change than the fix needs (the pipeline's stated invariant is that validator order is load-bearing behavior; a shared per-family helper avoids touching that list at all) and it does not match the existing "each validator owns its own family's fields" pattern the four validators already follow.

**Do not cache parsed patterns.**
`IsAllowedLocation` reparses each pattern string via `NamespaceGlobPattern.Parse` on every call (same cost profile `MatchesPrefix` already had — a cheap `.Split('.')`+loop, no regex). Contracts are typically evaluated over hundreds to low-thousands of types per run; this is not a hot path that justifies a memoization layer the rest of the codebase doesn't otherwise use for these list-based matchers. If profiling ever shows otherwise, that is a separate, unrequested optimization.

## Risks / Trade-offs

- **Behavior change for any *existing* policy that (accidentally) put a literal `*` in one of these three fields expecting it to be a no-op string.** → Such a policy was already non-functional (the entry never matched anything), so the only observable effect is that policy load now fails fast with a clear error instead of the rule quietly doing nothing. This is the intended fix, not a regression, and matches the issue's explicit acceptance criterion ("Unsupported wildcard patterns are rejected before validation instead of silently matching nothing").
- **Validator wording duplicated across four call sites via the shared helper.** → Mitigated by centralizing the parse+wrap logic in one `PolicyDocumentValidatorSupport` method; each validator only supplies its contract label and field name.

## Migration Plan

Not applicable — no data migration, no schema version bump, no CLI flag. Existing literal-namespace policies are unaffected. Policy authors who want the new wildcard support opt in by writing `*` segments; nothing else changes for them.
