## Context

Layer namespace matching currently uses literal prefix comparison (`MatchesPrefix`) plus optional `EndsWith` suffix. This prevents policy authors from describing repeated sibling namespaces (e.g., `FirstIce.Game.Features.Audio`, `FirstIce.Game.Features.Fishing`) without either hand-listing each one or using a looser prefix that risks namespace bleed. The existing `ArchitectureIgnoreMatcher` already implements glob-to-regex conversion for `ignored_violations`, but its semantics (path-style, `*` = `[^/]*`) don't directly apply to dot-separated namespaces.

The implementation is constrained to a single `*` segment wildcard — no `**`, `?`, or regex — to keep validation simple, behavior deterministic, and AI-authored policies safe.

## Goals / Non-Goals

**Goals:**
- Add constrained `*` glob support to `ArchitectureLayer.Namespace` without changing the YAML model
- `*` matches exactly one namespace segment; descendants of the resolved prefix are allowed
- `namespace_suffix` composes with glob patterns (fixed position when glob is present)
- Invalid patterns rejected at config load time with clear diagnostics
- Literal prefixes outrank glob patterns in `ResolveContainingLayer`
- Violation diagnostics include both pattern and concrete match
- Update JSON schema, docs, samples, AI-facing guidance

**Non-Goals:**
- No new YAML fields or model changes (overload existing `namespace`)
- No `**`, `?`, partial-segment globs, character classes, or regex support
- No changes to existing literal namespace semantics
- No changes to `ArchitectureIgnoreMatcher` or other glob systems

## Decisions

### D1: Segment-based matching over regex conversion

**Choice**: Parse namespace patterns and namespace strings into dot-separated segment arrays; match segment-by-segment rather than converting to regex.

**Rationale**: Segment-based matching is deterministic, trivially verifiable, easy to validate, and naturally supports extracting the resolved concrete prefix (needed for diagnostics). Regex conversion (as in `ArchitectureIgnoreMatcher`) adds escaping complexity and makes the resolved prefix extraction harder to compute.

**Alternatives considered**:
- Regex conversion (pattern -> `^FirstIce\.Game\.Features\.[^.]+(\.|$)` ) — rejected: harder to extract resolved prefix, risks edge cases with escaped dots, over-engineered for single `*`
- Raw `String.Split` + enumerator — chosen: simplest correct implementation

### D2: Overload existing `namespace` field

**Choice**: No model changes. Detect glob presence via `Contains('*')`; route to `NamespaceGlobPattern` when found; keep existing `MatchesPrefix` path for literals.

**Rationale**: No breaking changes. No new YAML syntax to learn. The existing `ArchitectureLayer` model stays identical — only matching logic changes.

### D3: `namespace_suffix` position-fixed only when glob is present

**Choice**: When `namespace` contains `*`, suffix is checked at a fixed position immediately after the full namespace pattern. When `namespace` is literal, suffix retains current `EndsWith(".Suffix")` behavior.

**Rationale**: With glob patterns, the suffix position is unambiguous once the full namespace pattern is resolved. Changing `EndsWith` for literals would break existing policy files. The divergence is easy to document: "position-fixed under glob, free-form under literal."

### D4: Literal beats glob in `ResolveContainingLayer` tiebreaker

**Choice**: Specificity ranking: literal (exact or child-prefix) > glob pattern; more literal segments > fewer; suffix adds specificity; fewer wildcards > more; layer name ordinal as final tiebreaker.

**Rationale**: Replaces the current `Namespace.Length` ordering which is meaningless for glob patterns. The ranking ensures backward compatibility — any type that matched a literal layer before will still match that same layer when a glob alternative is added nearby.

## Implementation

### `NamespaceGlobPattern`

Internal class with three responsibilities:

```
Parse(string pattern)
  → stores segments[] (string or wildcard marker)
  → stores IsGlob flag
  → calls Validate()

Validate()
  → rejects **, ?, partial * (e.g. Foo*, *Bar), bare *, empty segments, leading/trailing dots
  → throws InvalidNamespacePatternException

Match(string namespaceName, string? suffix)
  → splits ns into segments
  → walks pattern segments vs ns segments
  → wildcard consumes one ns segment at same position
  → if suffix set: checks segment at pattern.length == suffix
  → returns ArchitectureNamespaceMatch { Matched, Pattern, MatchedNamespacePrefix }
```

### Specificity scoring

```csharp
int SpecificityScore(ArchitectureLayer layer) {
    // Higher score = more specific
    // literal segments: 10 points each
    // suffix: 5 points
    // wildcards: -1 point each (fewer is better)
    // segments total: +0 (tiebreaker only by name)
}
```

### Integration

`ArchitectureLayerResolver` gains:
- `MatchNamespace(ArchitectureLayer, string) → ArchitectureNamespaceMatch`
- `MatchesNamespace` becomes: `return MatchNamespace(layer, ns).Matched;`
- `DescribeLayer` updated to handle glob+ suffix formatting
- `ResolveContainingLayer` ordering replaced with `SpecificityScore`

## Risks / Trade-offs

- **[Compatibility] Suffix-only layers** — Layers using `namespace_suffix` without `namespace` are currently possible (`namespace: ""`). This is untouched, but glob patterns require a non-empty `namespace`. **Mitigation**: validation only triggers when `namespace` contains glob chars; empty namespace is unchanged.
- **[Understanding] Position-fixed suffix** — Authors used to `EndsWith` behavior with literals may be surprised that suffix is fixed under glob. **Mitigation**: document explicitly; diagnostics always show the resolved concrete prefix so authors can verify.
- **[Future] `**` addition** — Adding `**` later would require a new validation rule and extension of the segment walker. **Mitigation**: the segment-based design makes this a localized change (add `"**"` as a new token type in `Parse` and a consume-all in `Match`).
