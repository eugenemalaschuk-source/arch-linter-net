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

### 2. Deferred-but-lexically-valid CEL tokens are tokenized, then rejected by the parser as `UnsupportedFeature` — at every nesting level

`null`, integer literals with a `u`/`U` suffix (`uint`), byte-string literals (`b"..."`), `?`/`:`
(conditional), `+ - * / %` (arithmetic), `IDENT ("." IDENT)* "{" ... "}"` (message/proto literal
construction), and a leading `.` before an identifier (root/absolute-qualified name syntax, e.g.
`.pkg.Type`) are all normative CEL syntax. The tokenizer accepts the lexical tokens involved (so a
malformed variant, e.g. `b"unterminated`, still reports the correct lexical error) and the parser
converts each to `UnsupportedFeature` at the point the grammar would otherwise accept it — never
`SyntaxError`, matching the "invalid CEL and valid-but-unsupported CEL are distinguishable"
acceptance criterion. This detection happens inside the shared `ParseExpression()` recursion point
(and the postfix `{` check), not only at the top-level `Parse()` entry, so `f(a + b)`, `(a + b)`,
and `items[a + b]` are all correctly reported as `UnsupportedFeature` rather than a generic
"expected `)`/`]`/`,`" `SyntaxError` from the enclosing construct — an initial implementation gap
caught in review (the top-level-only check missed every nested position). Truly invented tokens
(`=>`, `??`, `~`, stray `` ` `` outside a string) have no lexical form in the pinned grammar and
are always `SyntaxError`.

### 3. String literal grammar: single/double quotes, common escapes, raw and byte prefixes; triple-quoted strings, octal escapes, and `\0` out of scope

Supported: `'...'` and `"..."` with escapes `\n \t \r \\ \' \" \` \? \a \b \f \v`, `\xHH`,
`\uHHHH`, `\UHHHHHHHH`; `r"..."`/`R"..."` raw strings (no escape processing, matching the pinned
grammar's raw-string form); `b"..."`/`B"..."` byte-string literals (tokenized, then
`UnsupportedFeature` at parse time — v1 has no `Bytes` type). CEL has no standalone `\0` escape
(only three-digit octal, itself out of scope — see below), so `\0` is rejected as an unknown
escape, not silently treated as NUL (an initial implementation bug caught in review: `\0` was
wrongly wired to emit `'\0'`). Both `\uHHHH` and `\UHHHHHHHH` reject a codepoint in the UTF-16
surrogate range (`0xD800`-`0xDFFF`) — a standalone surrogate is not a valid Unicode scalar value;
the initial implementation only enforced this for `\U`, missing the 4-digit `\u` form (also caught
in review). Triple-quoted strings (`'''...'''`, `"""..."""`) and octal escapes (`\NNN`) are not
implemented — Profile v1 policy expressions are single-line string comparisons/prefixes
(`startsWith`, `endsWith`, `contains`, `containsKey` argument literals); neither construct is
exercised by any approved v1 use case, and both can be added without breaking existing parses if a
later profile needs them (pure lexer addition, no grammar restructuring). An unterminated string,
an invalid escape sequence, or a malformed `\xHH`/`\uHHHH`/`\UHHHHHHHH` digit run is `SyntaxError`,
not `UnsupportedFeature` — these are lexically malformed under the pinned grammar, not
valid-but-deferred.

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
  side) and decremented on return; exceeding it stops parsing immediately. It is also incremented
  once per postfix step in a member-access/indexing chain (each `.selector` or `[index]`) — the
  public `MaxNestingDepth` doc explicitly lists "member access chains" as an example of what the
  limit bounds, and the initial implementation only tracked recursive constructs, leaving
  `a.b.c.d...` completely unbounded (caught in review). Chain steps accumulate for the lifetime of
  the postfix loop and are released via `try`/`finally` when the loop exits, matching the
  recursive-construct pattern (a linear chain still counts as "how deep is this expression," it
  just doesn't recurse to build that depth).
- `MaxAstNodeCount`: incremented once per constructed `CelSyntaxNode`; exceeding it stops parsing
  immediately.
- `MaxIdentifierCount`: incremented once per identifier reference consumed — a bare variable
  reference, a function name (free or receiver call), or a member-selector name. This is a purely
  syntactic count (no schema/binder information needed), so it is enforced directly by the parser
  rather than deferred to the binder (#326); the initial implementation left the field entirely
  unenforced, silently accepting unbounded identifier counts (caught in review).

All five report `BudgetExceeded` with `limitName`/`observedValue`/`profileId` parameters, matching
the existing `CelCompilationResult<T>.BudgetExceeded` parameter shape for `MaxExpressionLength`.

### 8. Identifiers and digits are restricted to ASCII, matching the pinned grammar exactly

The pinned grammar's `IDENT` production is `[_a-zA-Z][_a-zA-Z0-9]*` — ASCII only. The initial
implementation used `char.IsLetter`/`char.IsLetterOrDigit`, which accept any Unicode letter (e.g.
`é`), silently making `é == é` parse as valid CEL when it is not (caught in review). The tokenizer
now checks the ASCII ranges explicitly for both identifier characters and digits (`char.IsDigit`
similarly accepts non-ASCII decimal digits, e.g. Arabic-indic, which `DIGIT = [0-9]` excludes).
This also fixed a related digit-literal bug: the decimal-point branch for `FLOAT_LIT` accepted a
trailing `.` with no following digit (`"3."`), but the pinned grammar requires `DIGIT+` after the
point; the tokenizer now only consumes `.` as part of a float literal when at least one digit
follows, leaving a bare trailing `.` to tokenize separately as `Dot`.

### 9. A deferred construct is only classified as `UnsupportedFeature` after its own syntax is verified complete

The initial implementation classified a construct as deferred the moment it recognized the
*first* token of a deferred form (a `+`/`-`/`*`/`/`/`%` trailing an expression, a `?`, a leading
`.`, or a `{`/`[` at primary position), without checking whether what followed was actually
well-formed. That meant `a +` (dangling operator, no right-hand operand), `a ? b` (missing `:`
and false branch), `[` (unterminated list), and `.` (no following identifier) were all wrongly
reported as `UnsupportedFeature` — a "this is valid CEL, just deferred" claim that is false for
genuinely malformed input (caught in review). The fix: each deferred-construct branch now
consumes and validates the construct's full grammar (the arithmetic chain's operands via
`ParseUnary()`, the conditional's true-branch/`:`/false-branch, the list/map/message literal's
element or `key : value` entries and closing bracket/brace, the root-qualified name's `.IDENT`
chain) *before* throwing `UnsupportedFeature`; any failure during that validation naturally
propagates as `SyntaxError` from the underlying `ParseExpression`/`Expect` calls, since a
malformed deferred construct is exactly as invalid as any other malformed CEL. The validation
parses are throwaway (their nodes are discarded — Profile v1 has no AST shape for arithmetic,
conditionals, or literals), but still count against `MaxAstNodeCount`/`MaxNestingDepth` via the
same `Track()`/depth machinery as real nodes, so a pathological deferred-construct chain (e.g. a
10,000-term `a+a+a+...`) is still bounded rather than an unbounded validation pass.

### 10. Message-literal detection requires a qualified-name-shaped receiver

The pinned grammar's message-literal production is `IDENT ("." IDENT)* "{" ... "}"` — the `{`
only follows a *qualified name*, never an arbitrary expression. The initial implementation treated
any `{` immediately following any postfix expression as a message literal, which wrongly
classified genuinely invalid forms like `1{}` (an integer literal followed by `{}`, which has no
meaning under any CEL grammar) as deferred syntax (caught in review). `IsQualifiedNameCandidate`
now walks the built syntax node: `true` only for a bare identifier or a member-access chain
rooted in one; a call result, index result, or literal is never a candidate, so `{` following one
falls through to the ordinary "unexpected trailing input" `SyntaxError` path instead.

### 11. Whitespace and string-terminator characters match the pinned grammar exactly, not .NET's broader Unicode categories

The pinned grammar's `WHITESPACE` token is `[\t\n\f\r ]` — five specific ASCII characters. The
initial implementation used `char.IsWhiteSpace(c)`, which also treats non-ASCII Unicode space
separators (e.g. U+00A0 NBSP) and other whitespace-category code points (e.g. U+000B vertical
tab) as skippable whitespace — accepting characters the grammar does not (caught in review). The
tokenizer now checks the five characters explicitly. Separately, the string-literal lexer only
rejected an unescaped `\n` as terminating a normal (non-raw, non-triple-quoted) string, but the
pinned grammar excludes both `\n` and `\r` from a normal string's character class — an unescaped
carriage return was silently absorbed into string content instead of ending the literal with
`SyntaxError` (also caught in review); the check now covers both.

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
