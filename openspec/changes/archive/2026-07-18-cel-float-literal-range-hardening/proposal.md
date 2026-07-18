## Why

Continuing the #338 corpus-mining pass (deeper review of `google/cel-go`, `google/cel-java`,
`projectnessie/cel-java`, and four independent .NET CEL ports — `rayokota/cel.net`,
`telus-labs/cel-net`, `plaisted/cel-compiled`, `hbjydev/celdotnet`) surfaced one further real
defect in `ArchLinterNet.CEL.Parsing.CelTokenizer`: a float literal whose magnitude exceeds what
IEEE 754 double can represent (e.g. `1.99e90000009`, sourced from `google/cel-go`
`parser/parser_test.go`'s "invalid double literal" case) was silently accepted as `+Infinity`
instead of being rejected. `double.TryParse` rounds an out-of-range magnitude to `Infinity` rather
than failing, and the tokenizer trusted that success unconditionally. Since Profile v1's `Float`
type is documented as "IEEE 754 double" (an ordinary finite/representable value), a policy
expression with a typo'd or absurd exponent silently becoming infinity is a correctness hazard,
not a permissive convenience — every other CEL implementation surveyed treats this as invalid.

## What Changes

- `CelTokenizer.BuildFloatToken` now rejects a successfully-`double.TryParse`d value that is
  `double.IsInfinity` with `SyntaxError`, instead of returning it as a valid `FloatLiteral` token.
- `cel-profile-v1` spec's tokenizer/parser implementation-scope requirement gains a sentence
  stating this explicitly, plus a scenario.
- Regression tests added, provenance-tagged to the upstream corpus that exposed the gap.
- Several additional corpus-derived scenarios reviewed and confirmed already-correct, added as
  regression tests: invented characters `@`/`$`; hex int/uint literal overflow (17 hex digits);
  a call result or a message literal is never itself a further message-literal receiver / callable
  (`f(){}`, `Type{}()`); a deferred list literal as the RHS of the `in` operator; the bare `in`
  keyword and bare `?` token as invalid primary expressions; two adjacent primaries with no
  connecting operator (`1 2`); the `u`/`U` unsigned-integer suffix not attaching to a float literal
  (`3.14u`).
- Corpus mining manifest (`docs/internal/cel-corpus-mining-manifest.md`) updated to record the
  full second-pass review across all originally-deferred repositories.

## Impact

- Affected capability: `cel-profile-v1` (tokenizer implementation-scope requirement).
- Affected code: `src/ArchLinterNet.CEL/Parsing/CelTokenizer.cs`, `CelTokenizerTests.cs`,
  `CelParserTests.cs`, `CelParserDeferredFeatureTests.cs`.
- No public API change; no Profile v1 broadening — the fix only makes an already-documented type
  guarantee ("Float is IEEE 754 double") actually enforced at the lexical boundary.
