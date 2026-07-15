## Why

`cel-profile-v1` (#324) fully pins the normative Profile v1 language subset, precedence table, and
the observable compilation behavior a working tokenizer/parser must produce, but
`ArchLinterNet.CEL.CelEnvironment.CompilePredicate`/`Compile` still return a blanket
`NotYetImplemented` diagnostic for every input — including syntactically invalid expressions.
#325 implements the tokenizer, Pratt parser, and internal syntax representation so that syntax
errors and profile-excluded-but-valid-CEL syntax produce real, span-carrying diagnostics, while
valid Profile v1 syntax still returns `NotYetImplemented` until the binder (#326) lands. The
existing spec already normatively fixes precedence, associativity, and the two compile-time
diagnostic scenarios this closes; this change adds only the lexical/parsing implementation-scope
details the existing spec left unstated (exact literal token grammar, deferred-token handling,
reserved-word position rules, diagnostic category) so `docs/internal/cel-engine-architecture.md`
and the spec stay traceable to what actually ships.

## What Changes

- New internal `ArchLinterNet.CEL.Parsing` namespace (never public — see the "Tooling and AST"
  extension-direction row in `docs/internal/cel-engine-architecture.md`, which prohibits exposing
  parser/AST types until a separate tooling story approves a stable neutral model):
  - `CelTokenKind` / `CelToken` / `CelTokenizer` — full Profile v1 + deferred-CEL token set,
    enforcing `MaxTokenCount` and `MaxLiteralSize`.
  - `CelSyntaxNode` hierarchy (literal nodes, identifier, unary, binary, member access, index,
    call) — immutable, internal, source-span-carrying.
  - `CelParser` — precedence-climbing parser implementing the frozen precedence/associativity
    table, enforcing `MaxNestingDepth` and `MaxAstNodeCount`, requiring full input consumption,
    and distinguishing `SyntaxError` (invented/malformed syntax) from `UnsupportedFeature`
    (valid CEL excluded from v1: arithmetic, conditional `?:`, list/map/message literals,
    `uint`/`bytes`/`null` literals).
- `CelEnvironment.CompilePredicate`/`Compile` now run the tokenizer+parser after the existing
  `MaxExpressionLength` gate. A syntax/unsupported-feature/structural-limit diagnostic short-
  circuits compilation with a real span. Syntactically valid Profile v1 expressions continue to
  fall through to the existing `NotYetImplemented` stub (binder/type-checker is #326's scope) —
  this preserves the documented stub contract asserted by
  `CelExternalConsumerSampleTests.HappyPath_BuildEnvironmentAndInspectCompilationResult` and
  `CelInternalApiCoverageTests.CelCompilationResult_NotYetImplemented_CarriesProfileIdParameter`.
- New focused parser/tokenizer tests plus negative-conformance and adversarial-limit tests in
  `tests/ArchLinterNet.CEL.Tests/`.
- `docs/internal/cel-engine-architecture.md` updated to record the actual parser ownership and
  the scope decisions this change locks in (see design.md).

## Capabilities

### Modified Capabilities

- `cel-profile-v1`: Adds one ADDED requirement pinning the tokenizer/parser implementation scope
  (literal grammar subset, deferred-token treatment, reserved-word position rules, diagnostic
  category) that the existing spec left as an implementation detail. No existing requirement,
  scenario, or public API shape changes.

## Impact

- **`src/ArchLinterNet.CEL/Parsing/`**: new internal-only namespace (tokenizer, AST, parser).
- **`src/ArchLinterNet.CEL/CelEnvironment.cs`**: wires the parser into the two compile entry
  points; no public signature change.
- **`tests/ArchLinterNet.CEL.Tests/`**: new parser/tokenizer test files.
- **`docs/internal/cel-engine-architecture.md`**: parser ownership section updated to reflect
  shipped scope.
- **Downstream #326 (binder/type-checker)**: consumes `CelSyntaxNode` internally; no public API
  is added for it to depend on.
- No public API surface change — `ArchLinterNet.CEL.Parsing` is internal; external consumers see
  only richer, span-carrying diagnostics from the same `CompilePredicate`/`Compile` entry points.
