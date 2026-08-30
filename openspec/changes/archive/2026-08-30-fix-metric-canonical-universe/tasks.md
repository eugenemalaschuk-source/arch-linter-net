## 1. Canonical metric evidence

- [x] 1.1 Use canonical resolved assembly and complete canonical type identities for footprint and type-count contributors; verify focused Core metric tests preserve distinct same-name contributors.
- [x] 1.2 Make assembly endpoint binding ambiguous whenever its retained simple-name candidate set is not exactly one; verify a reference identity cannot select among duplicate candidates.
- [x] 1.3 Keep every resolved assembly as an assembly-topology subject even when it has no loadable types, and fail closed when an ambiguous endpoint could be the selected metric component.

## 2. Bounded-scope applicability

- [x] 2.1 Reuse `allow_empty` topology scope semantics for an empty selected metric target while retaining stale and unexpected-empty diagnostics; verify evaluable zero and unassessable cases.
- [x] 2.2 Apply project ownership validation for external dependency facts only after their source maps to the selected node; verify unrelated facts do not poison a bounded metric.

## 3. Verification

- [x] 3.1 Run focused Core metric/topology tests and the Core public API approval check; verify all pass.
- [x] 3.2 Run repository lint, architecture lint, policy check, documentation lint, strict OpenSpec validation, and diff check; verify all pass.
