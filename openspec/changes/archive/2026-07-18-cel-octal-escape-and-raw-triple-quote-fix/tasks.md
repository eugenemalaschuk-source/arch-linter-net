## Tasks

- [x] Thread `isRaw`/`isBytes` into `LexTripleQuotedString`; reuse `AppendEscape` for non-raw
      escape validation; skip escape processing entirely for raw literals.
- [x] Add `AppendOctalEscape` (3-digit validation, `\000`-`\377` range) and wire it into
      `AppendEscape`'s default case.
- [x] Add `CelTokenKind.StringLiteralWithOctalEscape` and the corresponding
      `CelParser.ParsePrimary` case.
- [x] Update `CheckTokenBudgets` to bound all four literal-with-`StringValue` kinds uniformly by
      decoded content length.
- [x] Update/replace the now-outdated octal-rejection tests; add raw-triple-quote-escape,
      invalid-escape-in-triple-quote, malformed-octal, and budget-consistency regression tests.
- [x] Update `cel-profile-v1` spec and the corpus mining manifest.
- [x] Run `rtk make fmt` and `rtk make acceptance`.
