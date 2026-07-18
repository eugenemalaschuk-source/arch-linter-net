## Why

A second code-review round on PR #346 (issue #338) found two further defects and one design
inconsistency, all stemming from the first triple-quote/octal-escape fixes:

1. **Critical — raw triple-quoted strings misparsed.** `LexTripleQuotedString`'s closer scan
   unconditionally treated `\` as escaping the next character, regardless of whether the literal
   was raw (`r'''...'''`). In a raw string a backslash has no special meaning (matching how
   `LexString` already handles raw single/double-quoted strings), so `r'''a\'''` — a complete,
   valid raw triple-quoted literal — falsely reported "unterminated". Simultaneously, because the
   scan only ever checked for a closer without validating escape sequences, a non-raw literal with
   a genuinely invalid escape (`'''\q'''`) incorrectly passed as "well-formed" and was classified
   `UnsupportedFeature` instead of `SyntaxError` — violating "fully validate syntax, then classify
   as deferred," which this spec already establishes for every other deferred construct.

2. **Critical — octal escapes misclassified.** The first fix rejected a well-formed three-digit
   octal escape (`\NNN`, e.g. `\012`) with `SyntaxError`. Per this task's own corpus classification
   model, a well-formed octal escape is valid CEL syntax (the pinned grammar defines it) that
   Profile v1 simply doesn't support — category B, requiring `UnsupportedFeature`, not malformed
   CEL. A malformed octal sequence (`\0` alone, wrong digit count, or a value outside
   `\000`-`\377`) correctly remains `SyntaxError`.

3. **Important — inconsistent `MaxLiteralSize` accounting.** The triple-quote budget check bound
   raw token text length (prefix + 6 quote characters + content), unlike every other literal kind,
   which bounds decoded content length — making the same nominal content cost more or less budget
   depending on which quote form it used.

## What Changes

- `CelTokenizer.LexTripleQuotedString` now takes `isRaw`/`isBytes` and, for non-raw literals,
  reuses the exact same `AppendEscape` logic `LexString` uses to decode/validate escapes (the CEL
  escape grammar is uniform across single/double/triple-quote forms) — fixing both the raw-string
  bug and the invalid-escape-passes-as-deferred bug in one shared code path. For raw literals,
  backslash is treated as an ordinary character, exactly like `LexString`'s raw handling.
- `AppendEscape` gains octal-digit recognition (`\NNN`, delegating to a new `AppendOctalEscape`
  helper that validates exactly 3 octal digits in the `\000`-`\377` range) and now reports whether
  the escape it decoded was octal.
- New `CelTokenKind.StringLiteralWithOctalEscape`: a plain (non-byte) string literal containing a
  well-formed octal escape tokenizes as this kind instead of `StringLiteral`; `CelParser.ParsePrimary`
  gains a matching case classifying it `UnsupportedFeature` (`feature = "octal-escape"`), mirroring
  the `null`/`uint`/byte-string/triple-quote precedent exactly. A byte-string literal needs no
  reclassification (it is already always deferred), so its octal escapes decode normally.
- `CelTokenizer.CheckTokenBudgets`'s `MaxLiteralSize` check now covers
  `StringLiteral`/`BytesLiteral`/`TripleQuotedStringLiteral`/`StringLiteralWithOctalEscape`
  uniformly via decoded `StringValue.Length` (triple-quoted literals now carry a decoded
  `StringValue`, a natural consequence of reusing `AppendEscape`), removing the previous
  raw-text-length special case.
- `cel-profile-v1` spec corrected accordingly; tests updated/added with provenance tags.

## Impact

- Affected capability: `cel-profile-v1` (tokenizer implementation-scope requirement).
- Affected code: `src/ArchLinterNet.CEL/Parsing/CelTokenizer.cs`, `CelTokenKind.cs`, `CelParser.cs`,
  and the corresponding test files.
- No public API change; no Profile v1 broadening — octal escapes and triple-quoted strings remain
  unsupported syntax, only diagnostic classification/precision and escape validity are corrected.
