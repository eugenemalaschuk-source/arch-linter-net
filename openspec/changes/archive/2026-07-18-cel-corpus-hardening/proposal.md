## Why

Issue #338 mines upstream CEL syntax test corpora (cel-spec, cel-go, cel-java, and several .NET/JVM
CEL ports) to find blind spots our own design-led test suite missed before the reusable-engine
story closes. Mining the pinned `cel-expr/cel-spec` conformance corpus
(`tests/simple/testdata/parse.textproto`, commit `59505c14f3187e6eb9684fbd3d07146f614c6148`)
against `ArchLinterNet.CEL.Parsing.CelTokenizer` surfaced one real defect: triple-quoted string
openers (`'''`/`"""`) are valid CEL lexical syntax that Profile v1 deliberately excludes (design
decision 3, archived in `2026-07-15-cel-tokenizer-parser`), but the tokenizer did not reject them
as a unit. It silently re-tokenized `'''hello'''` as three adjacent single-quoted string literals
(`''`, `'hello'`, `''`), which the parser then rejected with a generic "unexpected trailing input"
`SyntaxError` at the wrong span — masking the actual unsupported construct instead of naming it.

This is exactly the corpus classification model #338 asks for: a valid-but-unsupported (category B)
CEL construct that must produce an explicit diagnostic, not silent reinterpretation. The intentional
Profile v1 exclusion itself is correct and unchanged; only the diagnostic's precision needed fixing.

## What Changes

- `CelTokenizer` now detects a triple-quote opener (`'''`/`"""`, with or without an `r`/`R`/`b`/`B`
  prefix) before attempting to lex a string, and rejects it with a single `SyntaxError` whose span
  covers exactly the opener (plus any prefix), instead of silently splitting it into three shorter
  string tokens.
- `cel-profile-v1` spec clarified: the existing "triple-quoted strings ... out of scope" sentence
  now states the required rejection shape (single `SyntaxError` at the opener, not re-tokenization).
- Test coverage added, provenance-tagged to the upstream corpus file/commit that exposed the gap,
  covering: triple-quoted openers (single/double quote, raw/byte prefix combinations), octal
  escapes (confirmed already-correct rejection, now explicitly regression-tested), keyword-casing
  identifiers (`True`/`TRUE`/`Null` etc. as plain identifiers), and string-prefix-letter/identifier
  disambiguation (`r`, `b`, `rb'...'` reverse-order prefix).
- A provenance-aware scenario manifest documenting reviewed upstream corpora, classifications, and
  the one fix made is added under `docs/internal/`.

## Impact

- Affected capability: `cel-profile-v1` (tokenizer implementation-scope requirement).
- Affected code: `src/ArchLinterNet.CEL/Parsing/CelTokenizer.cs`,
  `tests/ArchLinterNet.CEL.Tests/CelTokenizerTests.cs`.
- No public API change; no Profile v1 broadening. The fix only makes an already-intended rejection
  cleaner and more precisely located.
