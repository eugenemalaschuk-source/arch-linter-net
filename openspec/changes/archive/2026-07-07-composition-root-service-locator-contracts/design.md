## Context

Issue #88 asks for static composition-root/service-locator usage contracts. Two existing families are the closest precedent:

- **method-body contracts** (`strict_method_body`/`audit_method_body`): forbid calls matching `forbidden_calls` patterns, but the scan target is a single **included** source layer (`source`) — the scanner both selects which files/types to scan and which calls are forbidden within them.
- **interface-implementation contracts** (`strict_interface_implementation`/`audit_interface_implementation`, from PR #87/#198): iterate every type in the codebase (`TypeIndex.AllTypes()`) and classify each as `misplaced`/`forbidden` using the shared `IsAllowedLocation` helper against `allowed_only_in_*`/`forbidden_in_*` location lists.

Composition contracts need the **inverse** of method-body's scan shape: "forbid this call for every type *except* those in the allowed composition layers" — an allow-list over the whole codebase, not an include-list of one layer. That is exactly interface-implementation's iteration shape, just checking for forbidden API calls instead of interface implementations.

## Goals / Non-Goals

**Goals:**
- Let policies declare `allowed_only_in_layers/namespaces/projects/assemblies` (composition boundary) and `forbidden_apis` (call patterns).
- Report a violation for any type outside the allowed boundary whose methods/constructors call a forbidden API.
- Reuse `ArchitectureForbiddenCallMatcher`/`ForbiddenCallPattern` (call-pattern normalization and matching) and the `IsAllowedLocation`/`ResolveProjectAssemblyNames` location helpers verbatim — no new matcher language.
- Deterministic diagnostics, strict/audit modes, ignores, fail-closed loader validation — same shape as every other family.

**Non-Goals:**
- Runtime DI resolution or proving every service is registered correctly.
- Reflection/plugin loading validation.
- Semantic data-flow analysis.
- Roslyn source-level scanning for this family (see Decisions).

## Decisions

### Reflection/IL-only scanning, not Roslyn dual-scan
Method-body contracts run both a Roslyn semantic scan and an IL fallback scan because their scan target is a single named source layer that can be compiled in isolation. Composition contracts scan "every type outside an allow-list" — there is no single layer to compile, and `TypeIndex.AllTypes()` (already reflection-loaded for every other type-iteration family: interface-implementation, inheritance, attribute-usage) gives direct access to every loaded type's methods and constructors via `MethodBase.GetMethodBody()`. Following the reflection-only precedent of attribute-usage/interface-implementation keeps the implementation consistent and avoids standing up a second per-family Roslyn compilation pipeline for no added detection value — the forbidden APIs in scope (DI registration methods, `IServiceProvider.GetService`, container `Resolve` methods) are ordinary member references resolvable via IL metadata tokens, exactly like the existing IL fallback already resolves them for method-body contracts.

**Alternative considered**: reuse `ArchitectureSourceScanner.FindMethodBodyViolations` by inverting its file selection (scan all files, exclude those under allowed layers). Rejected: that scanner selects files by parsing `namespace` declarations per file, which conflates file-level and type-level granularity and would require duplicating the roslyn compilation setup for an inverted-selection case it wasn't designed for. Reusing the *matcher*, not the *scanner's file-selection wrapper*, is the intended reuse boundary.

### Extract the shared per-type IL scan loop
`ArchitectureIlMethodBodyScanner.FindMethodBodyViolations` currently computes its own `sourceTypes` from a namespace/layer before scanning. Extract the inner per-type/per-method matching loop (`FindTypeMatches`/`FindMethodMatches`) into a method that accepts an explicit `IReadOnlyCollection<Type>` so both the existing namespace-scoped entry point and the new composition contract's already-filtered type list can share it. This is additive — the existing public method's behavior and signature are unchanged.

### Location vocabulary is allow-list only (no forbidden-list)
Interface-implementation contracts support both `allowed_only_in_*` and `forbidden_in_*` because "an interface must not be implemented in namespace X" and "an interface may only be implemented in namespace Y" are both meaningful independently. For composition contracts, the natural shape is a single allow-list: "this API may only be called from the composition boundary" — everything outside is forbidden by definition, so a separate `forbidden_in_*` list would be redundant (it would just describe the complement of `allowed_only_in_*`). The contract therefore requires at least one `allowed_only_in_*` entry, matching the issue's own proposed shape (`allowed_layers`), renamed to `allowed_only_in_*` for consistency with the existing location-vocabulary naming convention.

### Naming: `allowed_only_in_*` over the issue's `allowed_layers`
The issue's sample YAML used a flat `allowed_layers` key. The codebase's established convention (interface-implementation, type-placement) is `allowed_only_in_layers/namespaces/projects/assemblies`. Using the existing convention keeps the location vocabulary consistent across families and lets the contract support namespace/project/assembly allow-listing in addition to layers, which the issue's own examples (ASP.NET DI vs. Unity/VContainer bootstraps) benefit from.

## Risks / Trade-offs

- **[Risk]** IL-only scanning cannot see calls made through delegates, expression trees, or reflection-invoked members. → **Mitigation**: documented explicitly as a non-goal, consistent with the existing method-body/external-dependency IL scanners' documented limitations.
- **[Risk]** Extracting shared logic out of `ArchitectureIlMethodBodyScanner` could regress the existing method-body IL fallback if the extraction changes behavior. → **Mitigation**: existing `MethodBodyContractTests`/`ExternalDependencyContractTests` continue to run unchanged against the extracted method; no signature or behavior change to the existing public entry point.
- **[Trade-off]** No Roslyn scan means violations report the declaring type/member rather than a precise source line. Acceptable: other reflection-based families (interface-implementation, inheritance, attribute-usage) already report at type granularity, not line granularity.

## Open Questions

None — the shape mirrors interface-implementation contracts closely enough that no ambiguity remains for implementation.
