## Why

`allowed_only_in_namespaces` (and its structural siblings `forbidden_in_namespaces` on attribute-usage/interface-implementation contracts and `must_reside_in_namespaces` on type-placement contracts) accept any string, but are matched at runtime with a pure literal `StartsWith`/equality check (`ArchitectureLayerResolver.MatchesPrefix`). A pattern such as `Example.Modules.*.Composition` is therefore accepted syntactically and silently matches nothing, even though `layers.<name>.namespace` already supports exactly this constrained, segment-based glob grammar (`NamespaceGlobPattern`). The rule stays strict, but the intended allowance silently has no effect — a fail-open trap for policy authors. This is Finding F9 from issue #443 (parent story #434).

## What Changes

- `ArchitectureLayerResolver` gains a `MatchesNamespacePattern(namespaceName, pattern)` helper that parses `pattern` with the existing `NamespaceGlobPattern` grammar and matches on segments. Literal patterns (no `*`) resolve to the same prefix semantics `MatchesPrefix` already provided — fully backward compatible.
- The shared `IsAllowedLocation` helper (used by Composition, AttributeUsage, InterfaceImplementation, and TypePlacement contract execution) is routed through `MatchesNamespacePattern` instead of `MatchesPrefix`, so `allowed_only_in_namespaces`, `forbidden_in_namespaces`, and `must_reside_in_namespaces` all gain the same wildcard semantics as `layers.<name>.namespace`.
- Policy load gains eager validation of every entry in these three fields (mirroring `LayerNamespacesValidator`'s eager `layer.GlobPattern` validation): an unsupported wildcard pattern (`**`, `?`, `[...]`, partial-segment `*`, bare/leading `*`) now fails policy load with an actionable `InvalidOperationException` naming the contract, the field, the pattern, and the violated grammar rule — instead of silently compiling and then matching nothing at scan time.
- No JSON schema change: these fields are already unconstrained `stringList` refs, consistent with how `layers.<name>.namespace` is validated (grammar enforced at Core runtime load, not schema level).

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `namespace-glob-patterns`: broaden the existing constrained-glob-pattern requirements (currently scoped to `layers.<name>.namespace`) to also cover `allowed_only_in_namespaces`, `forbidden_in_namespaces`, and `must_reside_in_namespaces`, and add a requirement that unsupported wildcard patterns in these fields are rejected at policy load with an actionable diagnostic.

## Impact

- `src/ArchLinterNet.Core/Resolution/ArchitectureLayerResolver.cs` — new `MatchesNamespacePattern` helper.
- `src/ArchLinterNet.Core/Execution/ArchitectureAnalysisSession.TypePlacement.cs` — `IsAllowedLocation` now matches via the glob grammar.
- `src/ArchLinterNet.Core/Contracts/Validators/PolicyDocumentValidatorSupport.cs` — shared eager-validation helper for `*_in_namespaces` fields.
- `src/ArchLinterNet.Core/Contracts/Validators/{CompositionValidator,AttributeUsageValidator,InterfaceImplementationValidator,TypePlacementValidator}.cs` — call the shared helper for their respective namespace fields.
- `docs/contracts/{composition,attribute-usage,interface-implementation,type-placement}.md`, `docs/ai/policy-authoring-guide.md` — document the now-explicit glob grammar.
- `tests/ArchLinterNet.Core.Tests/*ContractTests.cs` and validator tests — new literal/glob/invalid-pattern coverage.
- No public API surface change, no schema change, no breaking change to existing literal-namespace policies.
