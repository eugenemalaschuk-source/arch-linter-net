## 1. Core matching engine

- [x] 1.1 Create `NamespaceGlobPattern` class with `Parse`, `Validate`, and `Match` methods
- [x] 1.2 Implement segment-split parsing: pattern and namespace split by `.` into arrays
- [x] 1.3 Implement `*` as single-segment wildcard: consumes one namespace segment at same position
- [x] 1.4 Implement resolved prefix extraction: return concrete namespace prefix that `*` resolved to
- [x] 1.5 Implement suffix composition: when glob present, suffix checked at fixed position after `*`
- [x] 1.6 Create `ArchitectureNamespaceMatch` record: `Matched`, `Pattern`, `MatchedNamespacePrefix`
- [x] 1.7 Create `InvalidNamespacePatternException` for config load-time errors

## 2. Pattern validation

- [x] 2.1 Reject `**` as invalid (deferred to future)
- [x] 2.2 Reject `?` as invalid (not supported in v1)
- [x] 2.3 Reject partial-segment `*` (e.g. `Foo*`, `*Bar`, `F*o`)
- [x] 2.4 Reject bare `*` as entire namespace
- [x] 2.5 Reject empty segments (`A..B`), leading dots (`.Foo`), trailing dots (`Foo.`)
- [x] 2.6 Reject leading wildcard (`*.Features`) — `*` must have at least one literal segment before it
- [x] 2.7 Produce clear error messages identifying the pattern and the reason

## 3. ArchitectureLayerResolver integration

- [x] 3.1 Add `MatchNamespace(ArchitectureLayer, string) -> ArchitectureNamespaceMatch` method
- [x] 3.2 Update `MatchesNamespace` to delegate to `MatchNamespace` for backward compatibility
- [x] 3.3 Update `DescribeLayer` to handle glob patterns: format pattern + suffix without visual duplication
- [x] 3.4 Implement specificity scoring: literal segments (10), suffix (5), wildcards (-1 each)
- [x] 3.5 Replace `ResolveContainingLayer` `Namespace.Length` ordering with specificity score
- [x] 3.6 Add `NamespaceGlobPattern` caching or lazy initialization on `ArchitectureLayer`

## 4. Diagnostics enrichment

- [x] 4.1 Update `ArchitectureNamespaceViolationFinder` to pass `MatchedNamespacePrefix` into violation
- [x] 4.2 Update violation message format to include both pattern and concrete match
- [x] 4.3 Ensure literal namespace diagnostics are unchanged

## 5. Config load-time validation

- [x] 5.1 Hook `NamespaceGlobPattern.Validate()` into the contract loading pipeline
- [x] 5.2 Reject invalid patterns with clear config error before any matching occurs
- [x] 5.3 Add test coverage for each invalid pattern scenario

## 6. Tests

- [x] 6.1 Add test group: exact literal namespace compatibility (no regression)
- [x] 6.2 Add test group: `*` matches one segment with descendant prefix allowance
- [x] 6.3 Add test group: `*` fails when fewer segments than pattern
- [x] 6.4 Add test group: multiple `*` wildcards in one pattern
- [x] 6.5 Add test group: glob + suffix fixed-position matching
- [x] 6.6 Add test group: glob + suffix descendant allowance
- [x] 6.7 Add test group: glob + suffix wrong position fails
- [x] 6.8 Add test group: literal + suffix old `EndsWith` behavior unchanged
- [x] 6.9 Add test group: all invalid pattern rejection scenarios
- [x] 6.10 Add test group: `DescribeLayer` output for glob+suffix patterns
- [x] 6.11 Add test group: `ResolveContainingLayer` specificity ranking
- [x] 6.12 Add test group: literal beats glob in tiebreaking
- [x] 6.13 Add test group: diagnostics include pattern and concrete match
- [x] 6.14 Run `rtk make acceptance` to verify no regressions

## 7. Schema, docs, and samples

- [x] 7.1 Update `dependencies.arch.schema.json` `namespace` property description to document glob support
- [x] 7.2 Add glob pattern example to architecture policy documentation
- [x] 7.3 Add glob pattern sample to sample policy YAML
- [x] 7.4 Update AI-facing policy-authoring guidance with glob constraints
- [x] 7.5 Verify documentation builds with `rtk make docs-build`
