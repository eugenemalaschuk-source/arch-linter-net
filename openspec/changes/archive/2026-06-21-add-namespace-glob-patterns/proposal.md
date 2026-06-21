## Why

Layer namespace definitions currently support only literal prefix matching. Policy authors covering repeated sibling namespaces (e.g., feature modules under `FirstIce.Game.Features.*`) must either list every namespace explicitly or use a broader prefix that risks capturing unintended namespaces. A constrained single-segment `*` wildcard lets authors express "all immediate child namespaces under this prefix" safely and readably, without exposing policy authors to unrestricted regex.

## What Changes

- `ArchitectureLayer.Namespace` field accepts constrained glob patterns (`*` as a complete namespace segment) in addition to literal prefixes
- `*` matches exactly one namespace segment; descendants of the resolved concrete prefix are included (matching existing child-prefix semantics)
- `namespace_suffix` composes with glob patterns: when glob is present, suffix position is fixed to the segment immediately after `*` (vs. current `EndsWith` behavior for literal-only patterns)
- Literal prefixes outrank glob patterns in `ResolveContainingLayer` tiebreaking
- Invalid patterns (`**`, `?`, partial-segment `*`, bare `*`, character classes, empty segments) are rejected at config load time with clear diagnostics
- Violation diagnostics include both the configured pattern and the resolved concrete namespace prefix
- JSON schema, documentation, samples, and AI-facing guidance are updated
- All existing literal namespace definitions continue to work unchanged

## Capabilities

### New Capabilities
- `namespace-glob-patterns`: Constrained single-segment `*` wildcard support in `ArchitectureLayer.Namespace`, with segment-based matching, suffix composition, load-time validation, enriched diagnostics, and deterministic tiebreaking

### Modified Capabilities
- *(none — existing specs do not define namespace matching semantics; this is a new capability)*

## Impact

- `src/ArchLinterNet.Core/Resolution/ArchitectureLayerResolver.cs` — new `NamespaceGlobPattern` matching; `MatchesNamespace` becomes wrapper around richer API; `ResolveContainingLayer` tiebreaker updated
- `src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs` — no model change (overload existing `namespace` field)
- `schema/dependencies.arch.schema.json` — `namespace` property description updated
- `tests/ArchLinterNet.Core.Tests/LayerResolverTests.cs` — new test groups for glob matching, suffix composition, invalid patterns, tiebreaking
- `docs/` — glob pattern documentation and AI-facing guidance
