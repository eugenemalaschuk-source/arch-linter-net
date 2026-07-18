## Tasks

- [x] Add `CelTokenKind.TripleQuotedStringLiteral` and `CelTokenizer.LexTripleQuotedString`
      (escape-aware closer scan; `SyntaxError` only if genuinely unterminated).
- [x] Add the corresponding `CelParser.ParsePrimary` case, classifying a well-formed triple-quoted
      string literal as `UnsupportedFeature`.
- [x] Fix the quote-character message-formatting bug (moot: the corrected path no longer
      interpolates a quote character into a rejection message).
- [x] Update tokenizer/parser tests to match the corrected classification, and add
      unterminated/escaped-quote regression cases.
- [x] Correct `cel-profile-v1` spec.
- [x] Pin the second-pass upstream repositories to exact reviewed commit SHAs in the corpus
      mining manifest.
- [x] Run `rtk make fmt` and `rtk make acceptance`.
