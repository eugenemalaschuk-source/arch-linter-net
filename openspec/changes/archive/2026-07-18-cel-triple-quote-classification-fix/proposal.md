## Why

Code review on PR #346 (issue #338) found that the earlier triple-quoted-string fix was itself
misclassified: a well-formed triple-quoted string literal (`'''hello'''`) was made to fail with
`SyntaxError`, but per this task's own corpus classification model triple-quoted strings are
category B — valid CEL syntax that Profile v1 merely defers, not malformed CEL. The correct
diagnostic is `UnsupportedFeature`, exactly like `null`/`u`-suffixed-integer/byte-string literals,
which the tokenizer already accepts as valid tokens and the parser classifies as deferred only
after confirming they are fully well-formed. The review also found a message-formatting bug in the
rejected code path (`source[start]` used instead of the actual quote character, printing `rrr...`
instead of `'''`/`"""` for prefixed forms `r'''...'''`/`br'''...'''`) — moot once the diagnostic
path changes, since the corrected implementation no longer interpolates quote characters into a
rejection message at all.

## What Changes

- New `CelTokenKind.TripleQuotedStringLiteral` — the tokenizer now scans a full triple-quoted
  string literal (escape-aware, so an escaped quote character cannot falsely end the scan) and
  emits one token for it, mirroring how `null`/`uint`/byte-string literals already tokenize
  successfully as deferred-construct token kinds.
- `CelParser.ParsePrimary` gains a case for this new kind: `MarkDeferred(..., "triple-quoted-string")`
  then a `CelDeferredSyntax` node, matching the existing `NullLiteral`/`UintLiteral`/`BytesLiteral`
  pattern exactly. Compiling a well-formed triple-quoted string literal now fails with
  `UnsupportedFeature`, not `SyntaxError`.
- A genuinely unterminated triple-quoted string (no matching closer before end of input) remains
  `SyntaxError` — only a fully-formed instance is deferred, consistent with every other deferred
  construct in this spec.
- `cel-profile-v1` spec corrected to describe this classification and the escape-aware
  well-formedness requirement.
- Tests updated: the tokenizer-level tests now assert successful single-token tokenization (plus
  new unterminated-triple-quote and escaped-quote-does-not-falsely-terminate cases); a new
  parser-level test asserts `UnsupportedFeature` with `feature = "triple-quoted-string"`.
- `docs/internal/cel-corpus-mining-manifest.md` corrected, and the upstream repositories reviewed
  in the second corpus-mining pass (`google/cel-go`, `google/cel-java`, `projectnessie/cel-java`,
  and the four .NET CEL ports) are now pinned to the exact commit SHA fetched, making the review
  reproducible (the manifest previously recorded these as "HEAD at review time").

## Impact

- Affected capability: `cel-profile-v1` (tokenizer implementation-scope requirement).
- Affected code: `src/ArchLinterNet.CEL/Parsing/CelTokenizer.cs`, `CelTokenKind.cs`, `CelParser.cs`,
  and the corresponding test files.
- No public API change; no Profile v1 broadening — triple-quoted strings remain unsupported syntax,
  only the diagnostic classification and its precision are corrected.
