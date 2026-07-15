## Context

`cel-profile-v1` pins the precedence table, supported operators/types/functions, and two
parser-observable compile scenarios ("Invalid user expression produces structured diagnostics",
"Chained comparisons require explicit parentheses"). It does not pin lexical grammar details
(exact string-escape subset, how deferred-but-valid CEL tokens like `uint`/`bytes`/`null`
literals or arithmetic operators are lexed and diagnosed, or how CEL's reserved-identifier rule
interacts with primary vs. member-selector position). Those decisions are made here so the
implementation is traceable and reproducible rather than an unrecorded judgment call.

## Goals / Non-Goals

**Goals**: deterministic tokenizer/parser for exactly the Profile v1 grammar; explicit
`SyntaxError` vs `UnsupportedFeature` distinction for constructs the parser can determine are
valid-but-deferred CEL; enforce every parse-time structural limit already reserved on
`CelCompilationLimits`; keep every syntax type internal.

**Non-Goals**: binder/type-checking (#326), evaluation (#327), a public AST or tooling model
(explicitly deferred per the architecture blueprint's "Tooling and AST" row), full fidelity with
every CEL literal form (octal escapes and message literals are out of scope — v1 doesn't have
messages, and octal escapes are vanishingly rare in policy-linting expressions).

## Decisions

### 1. Negative number literals are not a lexer feature in v1

The upstream CEL grammar folds an optional leading `-` into `INT_LIT`/`FLOAT_LIT` at the lexer
level as a historical quirk, independent of the `Unary` production's `-` operator. Since Profile
v1 defers the entire arithmetic operator group (`+ - * / %`, per the architecture blueprint's
extension-direction matrix row 1) including unary minus, tokenizing `-5` as a single literal
token would let a v1 expression express a negative literal with no operator to explain how it got
there — an inconsistent half-feature. Instead `-` always tokenizes as a standalone `Minus` token;
`-5` parses as `Minus` immediately followed by `IntLiteral(5)`, and the parser reports
`UnsupportedFeature` (arithmetic/unary-minus deferred) rather than accepting a negative literal.
**Alternative rejected**: lexer-level negative literals — would let v1 express negative numbers
through the back door while claiming arithmetic is fully deferred.

### 2. Deferred-but-lexically-valid CEL tokens are tokenized, then rejected by the parser as `UnsupportedFeature`

`null`, integer literals with a `u`/`U` suffix (`uint`), byte-string literals (`b"..."`), `?`/`:`
(conditional), and `+ - * / %` (arithmetic) are all normative CEL tokens. The tokenizer accepts
them (so a malformed variant, e.g. `b"unterminated`, still reports the correct lexical error) and
the parser converts them to `UnsupportedFeature` at the point the grammar would otherwise accept
them — never `SyntaxError`, matching the "invalid CEL and valid-but-unsupported CEL are
distinguishable" acceptance criterion. Truly invented tokens (`=>`, `??`, `~`, stray `` ` ``
outside a string) have no lexical form in the pinned grammar and are always `SyntaxError`.

### 3. String literal grammar: single/double quotes, common escapes, raw and byte prefixes; triple-quoted and octal escapes out of scope

Supported: `'...'` and `"..."` with escapes `\n \t \r \\ \' \" \` \? \a \b \f \v \0`, `\xHH`,
`\uHHHH`, `\UHHHHHHHH`; `r"..."`/`R"..."` raw strings (no escape processing, matching the pinned
grammar's raw-string form); `b"..."`/`B"..."` byte-string literals (tokenized, then
`UnsupportedFeature` at parse time — v1 has no `Bytes` type). Triple-quoted strings (`'''...'''`,
`"""..."""`) and octal escapes (`\NNN`) are not implemented — Profile v1 policy expressions are
single-line string comparisons/prefixes (`startsWith`, `endsWith`, `contains`, `containsKey`
argument literals); neither construct is exercised by any approved v1 use case, and both can be
added without breaking existing parses if a later profile needs them (pure lexer addition, no
grammar restructuring). An unterminated string, an invalid escape sequence, or a malformed
`\xHH`/`\uHHHH`/`\UHHHHHHHH` digit run is `SyntaxError`, not `UnsupportedFeature` — these are
lexically malformed under the pinned grammar, not valid-but-deferred.

### 4. Reserved-identifier position rule matches the pinned `IDENT`/`SELECTOR` distinction already normative for schema names

`cel-profile-v1`'s context-schema requirement already states: `IDENT = SELECTOR - RESERVED`,
`SELECTOR = identifier-regex - KEYWORD`. The parser applies the same rule to expression syntax
(not just schema declaration): a reserved identifier (`as`, `break`, `const`, `continue`, `else`,
`for`, `function`, `if`, `import`, `let`, `loop`, `package`, `namespace`, `return`, `var`, `void`,
`while`) used as a bare primary reference (`if == true`) is `SyntaxError`; the same word used as a
member-selector name after `.` (`x.as`) or as a call name (`x.for()`) is syntactically valid — the
binder (#326) decides whether it resolves. The four keyword tokens (`true`, `false`, `null`,
`in`) are never valid in selector position because they lex as dedicated token kinds, not
`Identifier` — they were never candidates for the selector grammar rule to begin with.

### 5. Free-function call syntax (`f(x)`) is parsed, not rejected

Profile v1's function catalog has no free functions (every supported function is a receiver
call), but the pinned CEL primary-expression grammar allows `IDENT "(" args ")"` independent of
what functions a profile declares. The parser accepts this form and produces a `CelCallSyntax`
with a `null` receiver; resolving whether `f` is a known function is explicitly binder territory
per `cel-profile-v1`'s "Calling an unknown function name is a compile-time `BindingError`" note.
Rejecting the syntax here would duplicate binder responsibility inside the parser and contradict
the pipeline-ownership boundary in `docs/internal/cel-engine-architecture.md`.

### 6. Parser is fail-fast, not error-recovering

On the first `SyntaxError`/`UnsupportedFeature`/`BudgetExceeded` condition, parsing stops and
returns exactly that one diagnostic. `cel-profile-v1` never requires multiple simultaneous parse
diagnostics, and error-recovery (synchronizing to a safe token and continuing) adds meaningful
complexity for a single-expression, non-IDE-tooling use case (policy-linting expressions, not a
source file being edited interactively). This is deterministic and matches the "deterministic
recovery or fail-fast behavior" acceptance criterion by picking fail-fast explicitly.

### 7. Structural limit checkpoints

- `MaxTokenCount`: checked incrementally during tokenization; tokenizing stops the instant the
  count is exceeded (no wasted work tokenizing the remainder).
- `MaxLiteralSize`: checked when a string/raw/byte literal finishes lexing, against its decoded
  content length.
- `MaxNestingDepth`: a single counter incremented on every recursive descent into a sub-expression
  (parenthesized expression, unary operand, index expression, call argument, binary right-hand
  side) and decremented on return; exceeding it stops parsing immediately.
- `MaxAstNodeCount`: incremented once per constructed `CelSyntaxNode`; exceeding it stops parsing
  immediately.

All four report `BudgetExceeded` with `limitName`/`observedValue`/`profileId` parameters, matching
the existing `CelCompilationResult<T>.BudgetExceeded` parameter shape for `MaxExpressionLength`.

## Risks / Trade-offs

- Omitting triple-quoted strings and octal escapes is a real (if currently unreachable) grammar
  gap. If a future profile version needs them, they slot into the existing string-literal lexer
  path without touching the parser or AST — recorded so it isn't rediscovered as a surprise.
- Fail-fast diagnostics mean a source with multiple syntax problems only ever reports the first.
  Acceptable for v1's use case; would need explicit revisiting if this engine grows an
  interactive-editing/IDE consumer.

## Migration Plan

Purely additive; no existing public behavior changes for syntactically valid Profile v1 input
(still `NotYetImplemented` pending #326). No migration required.
