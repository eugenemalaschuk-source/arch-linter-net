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

All eight repositories named in issue #338 were fetched and reviewed (two passes: an initial
cel-spec-focused pass, then a full second pass covering the remaining seven). Every non-cel-spec
repository is pinned to the exact commit SHA fetched, so this review is reproducible — `git show <sha>:<path>` against the given repository reconstructs exactly what was read.

| # | Repository | Pinned commit SHA | Files reviewed | Status |
|---|---|---|---|---|
| 1 | `cel-expr/cel-spec` | `59505c14f3187e6eb9684fbd3d07146f614c6148` (already pinned by `cel-profile-v1` spec) | `tests/simple/testdata/parse.textproto` (full: `nest`, `repeat`, `string_literals`, `bytes_literals`, `whitespace`, `comments`, `selectors`, `receiver_function_names`, `struct_field_names` sections); `logic.textproto`, `comparisons.textproto`, `string.textproto` (cross-checked against already-adapted fixtures in `CelOfficialConformanceFixtureTests`) | Reviewed |
| 2 | `google/cel-go` | `82a222f46994cfdf3d161480ca00d06dac341f96` | `parser/parser_test.go` (full: literal table, parse-error table, macro tests, "Tests from C++/Java parser" sections), `parser/unescape_test.go` (full) | Reviewed (full) |
| 3 | `google/cel-java` | `ff7bccfe61d49894e156c1e994a2c4f161627340` | `parser/src/test/resources/parser_errors.baseline` (full: every `I:`/`E:` error-corpus entry) | Reviewed (full) |
| 4 | `projectnessie/cel-java` | `079fcc5fa65897e429bbdc36bc6e67ea08431038` | `core/src/test/java/org/projectnessie/cel/parser/ParserTest.java` (full) — confirmed to be a direct table-for-table Java port of `cel-go`'s own `parser_test.go` corpus (same numbered cases, same error strings); reviewed for divergence, found none beyond translation | Reviewed (full) |
| 5 | `rayokota/cel.net` | `fede03221723b096b7ca2122bb62e405d5cf6c95` | `src/test/Cel/Parser/ParserTest.cs`, `src/test/Cel/Parser/UnescapeTest.cs` — confirmed direct C# port of the same cel-go/cel-java corpus | Reviewed (full) |
| 6 | `telus-labs/cel-net` | `3031d5c39d23818524a4d75ebe532a1515f6fc11` | `tests/Cel.Tests/Tests/CelParserTests.cs` (full — small, independently-written suite) | Reviewed (full) |
| 7 | `plaisted/cel-compiled` | `c8822e22f767b389465e535848c274e298e6975e` | `Cel.Compiled.Tests/ParserTests.cs` (full — independently-written suite covering full CEL incl. arithmetic, which this port implements and Profile v1 defers) | Reviewed (full) |
| 8 | `hbjydev/celdotnet` | `516cdc2e1678457fc002615158ab4ff919afff51` | `tests/CelDotNet.Tests/Lexer/CelLexerTests.cs`, `tests/CelDotNet.Tests/Parser/CelParserTests.cs` (full — independently-written suite; this port also implements triple-quoted strings and full arithmetic, both deferred/excluded in Profile v1) | Reviewed (full) |

These SHAs are review-provenance pins (the exact commit read), not Profile v1 runtime or build
dependencies — none of these repositories are referenced by `ArchLinterNet.CEL`'s source or test
project files.

## Scenario matrix (adapted or confirmed)

| Upstream source | Scenario | Classification | Local test | Outcome |
|---|---|---|---|---|
| cel-spec `parse.textproto` `string_literals` | `'''hello'''` / `"""hello"""` triple-quote openers (and `r'''...'''`/`b"""..."""` prefixed forms) | B | `CelTokenizerTests.TripleQuotedStringLiteral_TokenizesAsOneToken_NotMisTokenized`, `CelParserDeferredFeatureTests.TripleQuotedStringLiteral_IsUnsupportedFeature` | **Defect found and fixed, twice.** First pass: the tokenizer silently re-tokenized `'''hello'''` as three adjacent string literals instead of one construct. Second pass (per code review on PR #346): the initial fix classified a well-formed triple-quoted literal as `SyntaxError`, but per this manifest's own classification model triple-quoted strings are category B (valid CEL, merely unsupported) — they must produce `UnsupportedFeature`, matching how `null`/`u`-suffixed/byte-string literals are already handled (tokenize successfully as a dedicated deferred token kind, then the parser classifies them once fully validated). Fixed by adding `CelTokenKind.TripleQuotedStringLiteral` and `CelTokenizer.LexTripleQuotedString` (escape-aware closer scan; still `SyntaxError` if genuinely unterminated), with a matching `CelParser.ParsePrimary` case. |
| google/cel-go `parser_test.go` ("Tests from C++ parser": `1.99e90000009` → "invalid double literal") | Float literal with an exponent beyond IEEE 754 double's representable range | B/C boundary | `CelTokenizerTests.FloatLiteral_MagnitudeOutOfDoubleRange_IsSyntaxError_NotSilentInfinity` | **Defect found and fixed** — `double.TryParse` silently rounded the magnitude to `+Infinity` instead of failing; the tokenizer trusted that success unconditionally, so a typo'd/absurd exponent silently produced an `Infinity`-valued `FloatLiteral` token rather than a `SyntaxError`. See `CelTokenizer.BuildFloatToken`. |
| cel-spec `parse.textproto` `string_literals` | Octal escapes (`\012`, `\000`, `\177`) | B | `CelTokenizerTests.OctalEscape_IsRejectedAsUnknownEscape` | Confirmed already-correct rejection (design decision 3); now explicitly regression-tested with upstream provenance. |
| cel-spec grammar keyword-casing convention; `telus-labs/cel-net` `CelParserTests` | `True`, `TRUE`, `False`, `Null`, `In` | A (must tokenize as plain identifiers, not keywords) | `CelTokenizerTests.MixedOrUpperCaseKeyword_TokenizesAsPlainIdentifier` | Confirmed already-correct; regression-tested. |
| cel-spec grammar `STRING_LIT : ["r"|"R"] STRING` / `BYTES_LIT : ("b"|"B") STRING_LIT` prefix boundary; `plaisted/cel-compiled` `ParseRAsIdentifier`/`ParseRbAsIdentifier` | Bare `r`/`b`/`R`/`B` and `r_value`/`bytes_count` identifiers; reverse-order `rb'...'` (no lexical form) | A / D | `CelTokenizerTests.StringPrefixLetter_NotFollowedByQuote_TokenizesAsIdentifier`, `.ReverseOrderRawByteStringPrefix_HasNoLexicalForm_TokenizesAsIdentifierThenString` | Confirmed already-correct; regression-tested. `rb'...'` explicitly excluded as D (no CEL lexical form; upstream grammar only defines byte-marker-first ordering). |
| cel-spec `parse.textproto` `receiver_function_names` | Reserved words as receiver-call names (`a.as()`, `a.package()`, ...) | A (must parse; binder then reports `BindingError` for unknown function) | Already covered by `CelParserTests.ReservedWordAsCallNameOnReceiver_StillParses` | No gap — pre-existing coverage confirmed sufficient. |
| cel-spec `parse.textproto` `selectors` | Reserved words as map/struct member selectors (`x.as`, `x.break`, ...) | A | Already covered by existing `CelParserDeferredFeatureTests` reserved-word suite | No gap. |
| cel-spec `parse.textproto` `nest`/`repeat` sections | Deeply nested/repeated arithmetic, list/map/message literals, chained ternaries | B/E | Already covered by `CelParserDeferredFeatureTests` `MaxNestingDepth`/`MaxAstNodeCount`/`MaxLiteralSize` adversarial suite | No gap — existing structural-limit tests already exercise this shape. |
| cel-spec `parse.textproto` `whitespace`/`comments` | Tab/newline/form-feed/CR-separated tokens; `//`-comment-separated tokens | A | Already covered by `CelTokenizerTests` whitespace/comment tests | No gap. |
| cel-spec `parse.textproto` `string_literals` | Full escape catalog (`\a \b \f \t \v`, hex/octal/short-unicode/long-unicode, mixed-case hex digits, unassigned code points) | A (supported escapes) / B (octal) | Already covered by `CelTokenizerTests.StringLiteral_DecodesEscapes` plus the octal addition above | No gap. |
| google/cel-go `parser_test.go` (`*@a \| b`, `1 + $`) | Invented characters `@` and `$` | C | `CelTokenizerTests.InventedOrUnsupportedCharacter_IsSyntaxError` (extended) | Confirmed already-correct; regression-tested. |
| google/cel-go `parser_test.go` (`0xFFFFFFFFFFFFFFFFF[u]` → "invalid int/uint literal") | Hex int/uint literal overflow (17 hex digits) | C | `CelTokenizerTests.HexIntLiteral_Overflow_IsSyntaxError` | Confirmed already-correct; regression-tested. |
| google/cel-go `parser_test.go` (`TestAllTypes(){}`, `TestAllTypes{}()`) | A call result is never a message-literal receiver; a message literal is never itself callable | C | `CelParserTests.MessageLiteralOnCallResultReceiver_IsSyntaxError`, `.CallImmediatelyFollowingAMessageLiteral_IsSyntaxError` | Confirmed already-correct; regression-tested (distinct code path from the existing literal-receiver case `1{}`). |
| google/cel-go `parser_test.go` (`"😁" in ["😁", ...]`) | A deferred list literal as the RHS of the `in` operator | B | `CelParserDeferredFeatureTests.ListLiteralAsInOperatorRightHandSide_IsUnsupportedFeature` | Confirmed already-correct; regression-tested (deferred-construct classification must work when nested under a supported comparison operator, not just at top level). |
| google/cel-java `parser_errors.baseline` (`in` alone → "mismatched input 'in'"); (`?` alone → "mismatched input '?'") | Bare `in` keyword and bare `?` token as invalid primary expressions | C | `CelParserTests.BareInKeyword_IsSyntaxError`, `.BareQuestionToken_IsSyntaxError` | Confirmed already-correct; regression-tested (`in` tokenizes as its own `CelTokenKind.In`, distinct from reserved-word `Identifier` tokens, so it was worth confirming separately). |
| hbjydev/celdotnet `Throws_OnTrailingTokens` | Two adjacent complete primaries with no connecting operator (`1 2`) | C | `CelParserTests.TwoAdjacentPrimariesWithNoOperator_IsSyntaxError` | Confirmed already-correct; regression-tested. |
| plaisted/cel-compiled `ParseFloatWithUintSuffixThrows` | The `u`/`U` unsigned-integer suffix does not attach to a float literal (`3.14u`) | C | `CelTokenizerTests.UintSuffix_AfterFloatLiteral_DoesNotAttachToTheFloat` | Confirmed already-correct (tokenizes as `FloatLiteral` + separate `u` identifier, which then fails to parse); regression-tested. |
| google/cel-java `parser_errors.baseline` (`` `bar` ``, `` foo.`bar` ``, escaped/backtick identifiers) | CEL's backtick "extended identifier" extension syntax | D | N/A | Not imported — not part of the pinned core grammar; already rejected via the existing invented-backtick-character test. No action needed. |
| google/cel-java `parser_errors.baseline` (`a.?b`, `a[?b]`, `Msg{?field: value}`, `[?a, ?b]`) | CEL optional-chaining (`?.`) extension syntax | D/E | N/A | Not imported — part of the `cel.lib.ext.optional` extension library, not the pinned core `cel-spec` grammar. Already rejected (generic `SyntaxError`); mapped as a future-profile research item, no v1 test required. |
| hbjydev/celdotnet `Tokenises_TripleQuotedStrings` | This independent .NET port implements triple-quoted strings | D | N/A | Not imported as a requirement — Profile v1 deliberately excludes triple-quoted strings (design decision 3); this port choosing to support more than v1 needs is expected variance, not evidence v1 should follow. |
| plaisted/cel-compiled full arithmetic/precedence suite; hbjydev/celdotnet full arithmetic suite | Arithmetic operator precedence/associativity (`+ - * / %`) | E | N/A | Both ports implement arithmetic (deferred in Profile v1). Their precedence tests were reviewed for parser-shape ideas but are not directly portable since our parser treats the whole arithmetic chain as an opaque deferred region (see `CelParser.ParseAdditionLevel`); no gap in our own deferred-arithmetic-detection coverage was found. |

## Follow-up

None outstanding for the repositories named in issue #338 — all eight were reviewed in full across
two passes. Residual, explicitly out-of-scope items are the CEL extension-library syntax forms
(backtick "extended identifiers", `?.`/`[?`/optional-field-init syntax) encountered in the
google/cel-java corpus: these belong to `cel.lib.ext.*` extension libraries layered on top of the
pinned `cel-expr/cel-spec` core grammar, not the core grammar itself, so they are classification D
(and, for the optional-chaining family, E — mapped to the future-profile research direction in
`docs/internal/cel-engine-architecture.md` rather than given a v1 regression test).
