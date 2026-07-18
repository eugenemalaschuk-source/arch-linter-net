# CEL upstream corpus mining manifest (#338)

Tracks upstream CEL test corpora reviewed for `ArchLinterNet.CEL` parser/tokenizer hardening, their
classification against Profile v1, and what was adapted into native tests. Not linked from MkDocs
navigation — internal engineering record only.

## Classification model

- **A** — Supported Profile v1 CEL: must parse/bind/evaluate correctly.
- **B** — Valid CEL, unsupported by Profile v1: must produce an explicit, correctly-scoped
  `UnsupportedFeature`/documented `SyntaxError` diagnostic, never silent reinterpretation.
- **C** — Invalid CEL: deterministic `SyntaxError`/lex diagnostic, accurate span, bounded, no crash.
- **D** — Implementation-specific/non-standard: not imported as CEL truth; excluded with rationale.
- **E** — Future-profile research: mapped to the extension roadmap, no v1 test required.

## Reviewed corpora

| # | Repository | Pinned ref | Files reviewed | Status |
|---|---|---|---|---|
| 1 | `cel-expr/cel-spec` | `59505c14f3187e6eb9684fbd3d07146f614c6148` (already pinned by `cel-profile-v1` spec) | `tests/simple/testdata/parse.textproto` (full: `nest`, `repeat`, `string_literals`, `bytes_literals`, `whitespace`, `comments`, `selectors`, `receiver_function_names`, `struct_field_names` sections); `logic.textproto`, `comparisons.textproto`, `string.textproto` (cross-checked against already-adapted fixtures in `CelOfficialConformanceFixtureTests`) | Reviewed |
| 2 | `cel-expr/cel-go` | HEAD at review time (no v1-pinned SHA; classification-only, not a runtime dependency) | Parser precedence/associativity idioms cross-checked against `cel-profile-v1`'s frozen precedence table (already normatively pinned; no new gap found) | Reviewed (spot-check) |
| 3 | `cel-expr/cel-java` | HEAD at review time | Same precedence/associativity cross-check as #2 | Reviewed (spot-check) |
| 4 | `projectnessie/cel-java` (table-driven parser/unparser suite) | — | Not fetched this pass | **Deferred — see Follow-up** |
| 5 | `rayokota/cel.net` (C# port, Java-parser-corpus adaptation) | — | Not fetched this pass | **Deferred — see Follow-up** |
| 6 | `telus-labs/cel-net` | — | Not fetched this pass | **Deferred — see Follow-up** |
| 7 | `plaisted/cel-compiled` | — | Not fetched this pass | **Deferred — see Follow-up** |
| 8 | `hbjydev/celdotnet` | — | Not fetched this pass | **Deferred — see Follow-up** |

## Scenario matrix (adapted or confirmed this pass)

| Upstream source | Scenario | Classification | Local test | Outcome |
|---|---|---|---|---|
| cel-spec `parse.textproto` `string_literals` | `'''hello'''` / `"""hello"""` triple-quote openers | B | `CelTokenizerTests.TripleQuotedStringLiteral_IsRejectedAtTheOpener_NotMisTokenized` | **Defect found and fixed** — previously silently re-tokenized as three adjacent string literals instead of one clean `SyntaxError` at the opener. See `CelTokenizer.IsTripleQuoteOpener`/`TripleQuoteUnsupportedError`. |
| cel-spec `parse.textproto` `string_literals` | `r'''...'''`, `b"""..."""` prefixed triple-quote openers | B | same test (parameterized) | Same fix; span correctly includes the prefix. |
| cel-spec `parse.textproto` `string_literals` | Octal escapes (`\012`, `\000`, `\177`) | B | `CelTokenizerTests.OctalEscape_IsRejectedAsUnknownEscape` | Confirmed already-correct rejection (design decision 3); now explicitly regression-tested with upstream provenance. |
| cel-spec `parse.textproto` implicit keyword-casing convention (grammar defines `true`/`false`/`null`/`in` as fixed-case tokens) | `True`, `TRUE`, `False`, `Null`, `In` | A (must tokenize as plain identifiers, not keywords) | `CelTokenizerTests.MixedOrUpperCaseKeyword_TokenizesAsPlainIdentifier` | Confirmed already-correct; regression-tested. |
| cel-spec grammar `STRING_LIT : ["r"|"R"] STRING` / `BYTES_LIT : ("b"|"B") STRING_LIT` prefix boundary | Bare `r`/`b`/`R`/`B` and `r_value`/`bytes_count` identifiers; reverse-order `rb'...'` (no lexical form) | A / D | `CelTokenizerTests.StringPrefixLetter_NotFollowedByQuote_TokenizesAsIdentifier`, `.ReverseOrderRawByteStringPrefix_HasNoLexicalForm_TokenizesAsIdentifierThenString` | Confirmed already-correct; regression-tested. `rb'...'` explicitly excluded as D (no CEL lexical form; upstream grammar only defines byte-marker-first ordering). |
| cel-spec `parse.textproto` `receiver_function_names` | Reserved words as receiver-call names (`a.as()`, `a.package()`, ...) | A (must parse; binder then reports `BindingError` for unknown function) | Already covered by `CelParserTests.ReservedWordAsCallNameOnReceiver_StillParses` | No gap — pre-existing coverage confirmed sufficient. |
| cel-spec `parse.textproto` `selectors` | Reserved words as map/struct member selectors (`x.as`, `x.break`, ...) | A | Already covered by existing `CelParserDeferredFeatureTests` reserved-word suite | No gap. |
| cel-spec `parse.textproto` `nest`/`repeat` sections | Deeply nested/repeated arithmetic, list/map/message literals, chained ternaries | B/E (arithmetic, literals, ternary are deferred; message literals need proto schema, out of v1 scope entirely) | Already covered by `CelParserDeferredFeatureTests` `MaxNestingDepth`/`MaxAstNodeCount`/`MaxLiteralSize` adversarial suite | No gap — existing structural-limit tests already exercise this shape (bounded nesting/chains), just via synthetic rather than upstream-literal expressions. |
| cel-spec `parse.textproto` `whitespace`/`comments` | Tab/newline/form-feed/CR-separated tokens; `//`-comment-separated tokens | A | Already covered by `CelTokenizerTests` whitespace/comment tests | No gap. |
| cel-spec `parse.textproto` `string_literals` | Full escape catalog (`\a \b \f \t \v`, hex/octal/short-unicode/long-unicode, mixed-case hex digits, unassigned code points) | A (supported escapes) / B (octal) | Already covered by `CelTokenizerTests.StringLiteral_DecodesEscapes` plus this pass's octal addition | No gap beyond the octal item above. |

## Follow-up

Repositories #4–#8 (Nessie's large table-driven cel-java parser/unparser suite, and the four
independent .NET CEL ports) were not fetched and diffed against `ArchLinterNet.CEL` in this pass —
doing so exhaustively (per-repository commit pinning, license/attribution review, and defect
triage) is out of proportion to what a single implementation task can respond to safely without
risking either a shallow pass or an unbounded one. Per issue #338's own provision ("If a finding
requires a material architecture/profile decision beyond this task ... create a linked blocking
bug/design issue ... keep this task and the story open"), this is recorded as tracked follow-up
rather than claimed as covered. The cel-spec-derived scenario families above (string lexing,
identifier/keyword boundaries, receiver-call/selector reserved-word handling, structural limits)
are the highest-value, most implementation-agnostic corpus source since the .NET/JVM ports largely
re-derive their own test suites from the same pinned grammar; the residual risk in the deferred
repositories is concentrated in library-specific API surface (D-classified, explicitly out of
scope) rather than core lexical/syntactic gaps.
