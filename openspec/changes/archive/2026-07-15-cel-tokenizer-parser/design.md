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

### 12. The conditional operator's branches follow the grammar's actual sub-structure, not a shared simplification

The pinned grammar is `Expr = ConditionalOr ["?" ConditionalOr ":" Expr]` — the true branch is
`ConditionalOr` precedence (no unparenthesized nested ternary there), the false branch is the full
recursive `Expr` (nested ternary IS allowed there, unparenthesized). The initial implementation
parsed both branches with a bare `ParseOr()` call and never re-checked for trailing deferred
arithmetic or a nested `?` before deciding `Expect(':')`/throwing — so `a ? b + c : d` (arithmetic
in the true branch) produced a nonsensical "expected ':' but found '+'" `SyntaxError` instead of
correctly validating the arithmetic and reporting `UnsupportedFeature`, and an incomplete false
branch could reach the throw before its own trailing content was verified (caught in review). The
fix: the true branch now goes through `ParseConditionalOrOperand()` (`ParseOr()` plus the same
deferred-arithmetic-chain validation `ParseExpression()` performs, but without the nested-`?`
check — matching `ConditionalOr` precedence exactly), and the false branch now goes through the
real `ParseExpression()` (handling nested ternaries and arithmetic identically to a top-level
expression, exactly as the grammar specifies).

### 13. Message-literal field keys are bare identifiers; map-literal keys remain arbitrary expressions

The pinned grammar distinguishes `FieldInitializerList` (message literal: `IDENT ":" value`,
field keys are bare identifiers) from `MapInitializerList` (map literal: `key ":" value`, keys are
arbitrary expressions) — these are syntactically different once you know which construct you're
in, and the parser already knows: a `{` after a qualified-name-shaped receiver is unambiguously a
message literal (`IsQualifiedNameCandidate`, decision 10), a standalone `{` is unambiguously a map
literal. The initial implementation used the same general-expression key parser for both, wrongly
accepting `Type{1: 2}` as well-formed deferred syntax when it is not valid CEL under any
interpretation (caught in review). `ParseBraceEntry(isMessageLiteral)` now requires a bare
`Identifier` token for a message-literal key and rejects anything else with `SyntaxError`, while a
map literal's key continues to use the general expression parser.

### 14. Unary prefix chains reject mixing '!' and '-'

The pinned grammar's `Unary` production is `Member | "!" {"!"} Member | "-" {"-"} Member` — each
alternative repeats only its own operator; there is no production allowing `!` and `-` to
alternate in one prefix chain. The initial implementation's `ParseUnary()` recursed into itself
unconditionally for both operators, so `!-x` and `-!x` were silently accepted (the inner operator
just recursed into the outer one's handling) even though neither has any valid CEL interpretation
(caught in review). `ParsePrefixChain(opKind)` now consumes a run of the *same* operator
iteratively, then explicitly rejects the *other* prefix operator as `SyntaxError` before parsing
the `Member` — `!!!x` and `---x` still work exactly as before (verified by regression tests), but
`!-x`/`-!x` are now correctly invalid.

### 15. Byte-string literals: combined `br"..."` prefix; `\x`/`\X`/`\u` accepted, only `\U` string-only

Two related lexer gaps, both caught in review (one of them twice — see the correction below).
First, the pinned grammar defines `BYTES_LIT : ("b"|"B") STRING_LIT` and
`STRING_LIT : ["r"|"R"] STRING` — a byte-string literal can itself have an inner raw-string prefix
(`br"..."`, `Br"..."`, `bR"..."`, `BR"..."`; byte marker always first, since `STRING_LIT`'s raw
marker is nested inside `BYTES_LIT`'s byte marker in the grammar, not the reverse). The initial
implementation only matched a single prefix character at a time, so `br'...'` fell through to
being lexed as a bare two-letter identifier `br` followed by an unrelated string token — entirely
losing the byte-string meaning. `TryMatchStringPrefix` now matches an optional byte marker
followed by an optional raw marker before the opening quote.

Second — corrected twice, so recorded carefully: the pinned grammar's byte-string escape set
includes `\u` (4-digit Unicode escape) but *not* `\U` (8-digit). The initial implementation
accepted both unchanged inside byte-string literals (a gap, since a byte sequence has no 8-digit
Unicode code points to decode into — `\U` can address the full Unicode range up to 0x10FFFF, which
does not fit the byte-literal escape model). The first review-fix pass over-corrected by rejecting
*both* `\u` and `\U` inside byte-string literals; a later review pass confirmed the pinned grammar
actually accepts `\u` there (only `\U` is string-only) — an easy mistake given `\u`/`\U` read as a
natural pair, but the grammar treats them asymmetrically for byte literals specifically.
`AppendEscape` now takes an `isBytes` flag and rejects only `\U` (not `\u`) with `SyntaxError` when
lexing a byte-string literal; `\X` (uppercase) is also accepted as an alias for `\x`, matching the
grammar's case-insensitive hex-escape marker.

### 16. MaxLiteralSize bounds list/map/message-literal element count during validation

`MaxLiteralSize`'s public doc has always promised "element count, for list/map literals," but the
initial implementation only checked it against string/byte-string content length — the
list/map/message-literal validation added by decision 9 never consulted it, silently accepting an
unbounded element/entry count while still being fully bounded by `MaxTokenCount`/`MaxAstNodeCount`
indirectly (caught in review as a documentation/implementation mismatch, not a security gap, since
those other budgets already prevent unbounded work — but the *wrong limit* would fire, confusing
callers who set a tight `MaxLiteralSize` expecting it to catch this case first). `TrackLiteralElement`
now increments per parsed element (list) or entry (map/message) and throws `BudgetExceeded` with
`limitName = "MaxLiteralSize"` the moment the count is exceeded, consistent with the documented
contract.

### 17. Deferred-construct classification is a pending decision reported only once the whole expression has parsed, not an immediate throw

The most significant fix in this review round, replacing the model decision 9 originally
established. Decision 9 said a deferred construct is only classified as `UnsupportedFeature`
*after* its own syntax is validated — but the implementation still *threw immediately* the moment
that validation succeeded, before the surrounding context had a chance to validate anything past
it. Two concrete failures resulted, both caught in review: `(a + b` (a genuinely unterminated
paren) reported `UnsupportedFeature` instead of `SyntaxError`, because `ParseExpression` threw for
the validated `a + b` chain before `ParsePrimary`'s `Expect(RParen, ...)` ever ran; and
`a ? b + c == d : e` reported a nonsensical `SyntaxError` ("expected ':'") instead of
`UnsupportedFeature`, because the arithmetic-chain check lived as a one-shot post-check *after*
`ParseOr()` returned, so it never re-examined the token stream after consuming `b + c` to notice a
trailing `==` was still part of the same operand (see decision 18).

The fix replaces every deferred-construct "validate then throw" with "validate then record a
pending diagnostic and return a `CelDeferredSyntax` placeholder, keep parsing normally." A single
`_pendingDeferred` field (span + message + feature, set once — `??=` semantics, so the first
deferred construct encountered wins) accumulates across the whole recursive-descent walk. Only
`CelParser.Parse` — after the *entire* top-level expression has parsed successfully and the
top-level trailing-input check has passed — checks `_pendingDeferred` and reports it. Any genuine
syntax error anywhere in the structure (a missing closing paren/bracket/brace, a missing ternary
`:`, a malformed nested construct) still throws immediately via the existing exception mechanism,
which unwinds past the pending-deferred bookkeeping entirely — so structural malformedness is
still caught with full fidelity, exactly where it occurs, and always wins over a deferred-feature
classification for the same expression.

Every call site that used to `throw FailUnsupported(...)` now calls `MarkDeferred(...)` and
returns a `Track(new CelDeferredSyntax(span))` placeholder instead: null/uint/bytes literals,
list/map literals (`ParsePrimary`'s `LBrace`/`LBracket` cases), unary minus (`ParsePrefixChain`),
the ternary operator, and the message-literal postfix branch. `CelDeferredSyntax` carries only a
span — it is never a "real" node semantically, only a vehicle to let the surrounding
recursive-descent machinery (span merging, postfix chaining, argument/element lists) continue
exactly as if a normal node had been returned, since Profile v1 has no AST shape to build for any
of these constructs and none of them will ever survive to be used once the pending diagnostic is
eventually reported.

### 18. Deferred arithmetic is absorbed at the pinned grammar's `Addition`/`Multiplication` level, not as a post-hoc trailer check

The pinned grammar is `Relation = Addition [Relop Addition]`, `Addition = Multiplication
{("+"|"-") Multiplication}`, `Multiplication = Unary {("*"|"/"|"%") Unary}` — arithmetic sits
*between* `Unary` and `Relation` (comparison) in the precedence chain, meaning each comparison
operand is itself allowed to be an arithmetic expression. The implementation instead had
`ParseComparison` call bare `ParseUnary()` for its operands, with a single arithmetic-chain check
bolted on as a post-check inside `ParseExpression`, run only once after `ParseOr()` had already
fully returned. This meant arithmetic was only recognized when it trailed a completely-reduced
`ConditionalOr` with nothing else pending — the moment a comparison operator followed the
arithmetic (`a + b == c`), `ParseComparison` had already returned early (its "operand" `ParseUnary`
call stopping right before the `+`, never having seen a comparison operator to match), leaving the
`+` to be discovered later, in the wrong place in the grammar, or (inside a ternary true branch)
never revisited at all before the missing-`:` error fired (see decision 17's example).

The fix introduces `ParseAdditionLevel`, called from `ParseComparison` in place of bare
`ParseUnary()` for both its `left` and `right` operands. It absorbs a flat run of
`+`/`-`/`*`/`/`/`%` around `Unary` operands — deliberately *not* distinguishing `Addition` from
`Multiplication` precedence, because Profile v1 never builds a real arithmetic AST for either, so
the precedence difference would only change tree *shape* (irrelevant, since the tree is discarded)
and never *which tokens are consumed* (the only thing that matters for correctly delimiting the
deferred region and validating what follows it) — collapsing the two grammar levels into one loop
is a safe, non-lossy simplification given that constraint, not a grammar shortcut that changes
observable behavior. Marking deferred now happens inside this loop (per decision 17's
mark-don't-throw model), so `a + b == c`, `a ? b + c == d : e`, and `(a + b) == c` all correctly
absorb the arithmetic at the operand level before the enclosing comparison/ternary/parenthesis
logic ever runs.

### 19. A root-qualified name's trailing message literal gets the same field-key validation as a non-root-qualified one

Decision 10 established that message-literal detection requires a qualified-name-shaped receiver
(`IsQualifiedNameCandidate`), and decision 13 required message-literal field keys to be bare
identifiers. The root-qualified-name case (`ParsePrimary`'s `Dot` branch) originally built its
result as an opaque placeholder and threw immediately — which, independent of decision 17's fix,
meant `IsQualifiedNameCandidate` never got a chance to recognize it (an opaque placeholder is not
an identifier or member-access node), so a trailing `{` after a root-qualified name was never
routed through `ParsePostfix`'s message-literal branch at all; it just became unconsumed trailing
input. `.pkg.Type{1: 2}` therefore skipped field-key validation entirely — caught in review as
inconsistent with the identical, non-root-qualified `Type{1: 2}` case, which does correctly reject
a non-identifier key.

The fix: the `Dot` case now builds the `.pkg.Type` chain using the exact same
`CelIdentifierSyntax`/`CelMemberAccessSyntax` node shapes an ordinary (non-root-qualified)
reference chain would use, marking deferred (root-qualified name) once the chain is fully
consumed, but *returning that node* rather than an opaque placeholder. Because the returned shape
is indistinguishable from a normal identifier/member-access chain, `IsQualifiedNameCandidate`
recognizes it, and `ParsePostfix`'s existing message-literal branch (unchanged) naturally applies
the same bare-identifier-field-key validation to whatever follows — no duplicated validation logic
needed. `.pkg.Type{1: 2}` is now `SyntaxError` (bad field key) exactly like `Type{1: 2}`;
`.pkg.Type{field: 1}` remains `UnsupportedFeature` (root-qualified name is deferred regardless of
what follows it).

**Superseded by decisions 20 and 21 below** — a later review round found two further gaps in this
`IsQualifiedNameCandidate`-based mechanism itself (not specific to root-qualified names), which
required replacing the structural node-shape check with explicit parse state.

### 20. Message-literal-receiver eligibility is explicit parse state, not an AST-shape check

`IsQualifiedNameCandidate` (decision 10, refined by decision 19) inferred message-literal-receiver
eligibility from the *shape* of the already-built `CelSyntaxNode` — true for a
`CelIdentifierSyntax`/`CelMemberAccessSyntax` chain, false otherwise. This has a fundamental gap:
a parenthesized qualified name (`(Type)`, `(pkg.Type)`) produces the *exact same node shape* a
bare `Type`/`pkg.Type` would, since `ParsePrimary`'s `LParen` case just returns the inner
expression's node directly — so `IsQualifiedNameCandidate` could not distinguish them, and
`(Type){field: 1}` was wrongly accepted as a well-formed (if deferred) message literal (caught in
review). Per the pinned grammar, a message literal's qualified-name prefix
(`IDENT ("." IDENT)*`) is a primary-level production, not a generic postfix step usable after any
expression that merely *evaluates to* an identifier shape — parenthesization removes eligibility
even though it doesn't change the resulting node's type.

The fix replaces the structural check with an explicit `bool isQualifiedNameChain` threaded
through `ParsePrimary`/`ParsePostfix` as parse *state*, not derived from the node afterward:
`ParsePrimary` sets it `true` only for a bare identifier or root-qualified name that wasn't
immediately called, and `false` for every other primary form (literals, list/map-literal
placeholders, and critically the `LParen` case, unconditionally, regardless of what the
parenthesized sub-expression contains). `ParsePostfix` clears it the moment a call or index step
occurs (a call/index result is never a qualified name) and otherwise carries it forward unchanged
across `.member` steps. `Check(LBrace) && isQualifiedNameChain` (state) replaces
`Check(LBrace) && IsQualifiedNameCandidate(expr)` (shape) as the message-literal trigger
condition — `IsQualifiedNameCandidate` is removed entirely, since the state it approximated is now
tracked precisely.

### 21. Root-qualified names consume only their leading `. IDENT`, reusing the ordinary postfix path for everything after

The original root-qualified-name implementation (decisions 10 and 19) parsed the *entire*
`"." IDENT ("." IDENT)*` chain inside `ParsePrimary`'s `Dot` case in one dedicated loop, then
returned control to `ParsePostfix`. Two gaps followed from this, both caught in review. First,
that dedicated loop never checked for a trailing `(args)` to recognize a call — so `.f()` and
`.pkg.f()` left the `(` as unconsumed trailing input, an unconditional `SyntaxError` regardless of
how well-formed the call was, even though `f()` and `pkg.f()` (no leading dot) parse correctly via
`ParseIdentifierPrimary`/`ParsePostfix`'s own call handling. Second, the loop's `.member` steps
never called `EnterChainStep` (unlike `ParsePostfix`'s own `.member`/`[index]` loop, which does),
so a root-qualified chain's `MaxNestingDepth` was completely unenforced past the leading link —
`.a.b.c.d.e...` could grow without bound while a structurally identical non-root-qualified
`a.b.c.d.e...` was correctly bounded.

Rather than duplicating call-recognition and depth-tracking inside the dedicated loop, the fix
eliminates the loop: `ParseRootQualifiedNamePrimary` now consumes only the leading `"." IDENT`
(mirroring `ParseIdentifierPrimary` exactly, including checking for an immediately-following
`(args)` to produce a root-qualified free-function-call node) and returns — leaving every
subsequent `.member`, `.call(...)`, `[index]`, or message-literal step to `ParsePostfix`'s
existing loop, the *same* loop a non-root-qualified chain uses, `EnterChainStep` and all. This is
not a parallel fix but a deletion of the parallel path: a root-qualified chain's tail is now
*identical code* to a non-root-qualified chain's tail, so any future fix to call/index/
message-literal/depth handling in `ParsePostfix` automatically applies to both, and the two can no
longer drift apart the way they just did. The leading link itself (the `"." IDENT` consumed inside
`ParseRootQualifiedNamePrimary`) is not separately depth-tracked — a deliberate, narrower gap than
the one being fixed (a single fixed-cost link per root-qualified name, not an unboundable loop),
recorded as a residual trade-off below rather than closed, since closing it would require passing
a depth contribution out of `ParsePrimary` through every other case, for one link's worth of
budget.

### 22. A reserved word beginning a primary is IDENT-governed or SELECTOR-governed depending on what follows it, not on its root-qualified/non-root-qualified position

Decision 4 established `IDENT = SELECTOR - RESERVED` / `SELECTOR = identifier-regex - KEYWORD` for
schema-declared names, and the parser already applied `IDENT` rules (reserved words rejected) to
*any* reserved word starting a primary expression, and `SELECTOR` rules (reserved words allowed)
to a reserved word reached via `.member`. This is too coarse: a reserved word starting a primary
is not always "a bare variable reference" — it can also be the first segment of a qualified
type/message name (`package.Type`, or `package` immediately followed by a message literal's `{`),
which is a `SELECTOR`-governed position semantically identical to a `.member` step, just written
without a leading dot. The parser rejected `package{field: 1}` outright before ever checking
whether a `{` followed (`ParseIdentifierPrimary` threw on `token.IsReserved` unconditionally,
before even consuming the token), and — independently — `ParseRootQualifiedNamePrimary` accepted
*any* reserved word via `ExpectSelectorName()` unconditionally, so `.package` and `.package()`
(both invalid — a reserved word can never be a bare reference or a callable name, root-qualified
or not) were wrongly accepted as deferred root-qualified syntax. Both gaps were caught in the same
review round; the OpenSpec text was also found to describe the message-literal/qualified-name
prefix as `IDENT ("." IDENT)*`, which is actually wrong given the spec's own `SELECTOR` rule
already permits reserved words there — the grammar notation is now `SELECTOR ("." SELECTOR)*`.

The fix is one-token lookahead, applied identically in both `ParseIdentifierPrimary` and
`ParseRootQualifiedNamePrimary` via a shared `RequireNonTerminalReservedUsage` helper: a reserved
word is valid (do not throw) exactly when the *next* token is `.` (more qualification coming) or
`{` (a message literal); it is invalid (throw `SyntaxError`) for anything else, including `(`
(an attempted call) or end-of-construct (a bare terminal reference). This correctly classifies:
`package{field: 1}`, `package.Type{field: 1}`, `.package{field: 1}` as valid deferred syntax
(`UnsupportedFeature`); `package()`, `.package`, `.package()` as `SyntaxError`; and leaves
`x.package()` (reserved word in ordinary `.member`/`.call()` selector position on a non-reserved
receiver, handled by `ParsePostfix`'s pre-existing, unchanged `ExpectSelectorName()` path) fully
unaffected, since that was never routed through either `ParseIdentifierPrimary` or
`ParseRootQualifiedNamePrimary` to begin with.

**Incomplete — see decision 23 below.** The one-token lookahead in decision 22 checks only
whether the token *immediately following* a reserved root is `.` or `{`; it does not verify the
chain actually *reaches* a message literal. A later review round found this insufficient:
`package.Type` (dot follows, but the chain never reaches `{`) was wrongly accepted as a valid
qualified-name reference, and `package.Type()`/`package.Type[0]` (dot follows, then the chain
ends in a call/index instead of a message literal) were wrongly accepted as deferred
root-qualified/qualified-name syntax, when the pinned grammar only permits a reserved root inside
a chain that terminates in a message literal.

### 23. A reserved-word root's validity is a whole-chain property, resolved once the chain is known to have ended

Decision 22's one-token lookahead answered the wrong question: "is the *next* token `.` or `{`?"
instead of "does this chain *eventually* reach a message literal?" A dot immediately following a
reserved root only promises *more chain*, not that the chain will end in `{...}` — `package.Type`
satisfies the one-token check (next token is `.`) and then simply ends, `package.Type()` and
`package.Type[0]` satisfy it and then the chain terminates via a call/index instead. All three are
invalid per the pinned grammar (a reserved word is only valid as a message-literal type-name
root), but decision 22's check had already committed to "valid" after the first token and never
revisited the decision.

The fix threads a `CelToken? pendingReservedRoot` through the *entire* postfix chain (mirroring
how `isQualifiedNameChain`, decision 20, is already threaded) rather than deciding validity
eagerly at the root: `ParsePrimary`/`ParseIdentifierPrimary`/`ParseRootQualifiedNamePrimary` set it
to the reserved token when the primary's root was reserved (`null` otherwise — a reserved word
reached later via an ordinary `.member` step never sets it, since decision 22 already established
that position is unconditionally `SELECTOR`-governed). `ParsePostfix`'s loop clears it to `null`
the moment a message literal is actually reached (the pending requirement is satisfied) and
otherwise carries it forward unchanged across `.member`/`.call()`/`[index]` steps. Only after the
loop exits — meaning the chain is known to have ended, one way or another — does `ParsePostfix`
check whether it's still non-null and throw `SyntaxError` if so. This correctly rejects
`package.Type`, `package.Type()`, `package.Type[0]`, `.package.Type`, and `.package.Type()` while
still accepting `package.Type{field: 1}` and arbitrarily long chains like
`package.Type.Other{field: 1}`, since the pending flag survives however many `.member` hops occur
before the message literal is reached.

This also required correcting the OpenSpec text itself, which decision 22 had written to say an
immediately-following `.` was sufficient — an error in the same direction as the implementation
bug it was documenting, caught in the same review round that caught the implementation gap.

## Risks / Trade-offs

- Omitting triple-quoted strings and octal escapes is a real (if currently unreachable) grammar
  gap. If a future profile version needs them, they slot into the existing string-literal lexer
  path without touching the parser or AST — recorded so it isn't rediscovered as a surprise.
- Fail-fast diagnostics mean a source with multiple syntax problems only ever reports the first.
  Acceptable for v1's use case; would need explicit revisiting if this engine grows an
  interactive-editing/IDE consumer.
- The reverse byte/raw prefix order (`rb"..."`) has no lexical form in the pinned grammar and is
  intentionally not matched — it falls through to being lexed as an ordinary (malformed)
  identifier-then-string sequence, which is correct per the grammar but may surprise a reader
  expecting prefix-order symmetry.
- A root-qualified name's leading `"." IDENT` link is not individually bounded by
  `MaxNestingDepth` (see decision 21) — only the second and later links are, via the shared
  `ParsePostfix` mechanism. This is a single fixed-cost gap (one link, not a loop), not an
  unbounded one; closing it fully would require threading a depth contribution out of
  `ParsePrimary` through every case for one link's worth of budget, judged not worth the
  signature churn.

## Migration Plan

Purely additive; no existing public behavior changes for syntactically valid Profile v1 input
(still `NotYetImplemented` pending #326). No migration required.
