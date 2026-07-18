## MODIFIED Requirements

### Requirement: Tokenizer and parser implementation scope for Profile v1

`ArchLinterNet.CEL`'s tokenizer and parser (internal `ArchLinterNet.CEL.Parsing` namespace) SHALL
implement exactly the following lexical and syntactic scope for Profile v1, in addition to the
precedence, associativity, and operator/type/function requirements already fixed elsewhere in
this spec:

- Numeric literals SHALL NOT include a sign; `-` SHALL always tokenize as a standalone operator
  token. A leading `-` before a numeric literal SHALL be reported as `UnsupportedFeature`
  (arithmetic/unary-minus is deferred), never accepted as part of the literal. A float literal
  (`FLOAT_LIT`) whose decimal text overflows the finite representable range of an IEEE 754 double
  (i.e. whose magnitude, typically from an extreme exponent, would otherwise round to positive or
  negative infinity during parsing) SHALL be reported as `SyntaxError` rather than silently
  converted into a non-finite `Infinity` token value. This literal-level rule does not narrow the
  `CelValue.Float(double)` runtime value domain or prohibit non-finite IEEE 754 values supplied
  through the public value API.
- The tokenizer SHALL accept `null` literals, `u`/`U`-suffixed unsigned-integer literals, and
  `b"..."`/`B"..."` byte-string literals as valid tokens (they are normative CEL syntax), and the
  parser SHALL reject each with `UnsupportedFeature` at the point the grammar would otherwise
  accept it — never `SyntaxError`. The same applies to the arithmetic operators (`+ - * / %`) and
  the conditional operator (`? :`) when encountered in an expression-forming position.
  A malformed instance of any of these tokens (e.g. an unterminated byte-string) SHALL still be
  reported as `SyntaxError`, since it is not valid CEL syntax to begin with.
- String literals SHALL support `'...'` and `"..."` quoting with the escape sequences
  `\n \t \r \\ \' \" \` \? \a \b \f \v`, `\xHH`, `\uHHHH`, and `\UHHHHHHHH`, plus `r"..."` /
  `R"..."` raw-string quoting (no escape processing). CEL has no standalone `\0` escape (only
  three-digit octal, which is out of scope below), so `\0` SHALL be rejected as an unknown escape
  sequence, not silently treated as NUL. `\uHHHH` and `\UHHHHHHHH` SHALL both reject a codepoint
  in the UTF-16 surrogate range (`0xD800`-`0xDFFF`) — neither is a valid standalone Unicode scalar
  value. Triple-quoted strings and octal escape sequences are out of scope for Profile v1 lexing;
  adding them is a pure lexer addition reserved for a future profile version, not a
  grammar-restructuring change. An unterminated string literal or a malformed escape sequence
  SHALL be reported as `SyntaxError`. A triple-quote opener (`'''` or `"""`, with or without a
  `r`/`R`/`b`/`B` prefix) SHALL be rejected as a single `SyntaxError` at the opener itself (a
  3-character span, plus any prefix) — it SHALL NOT be silently re-tokenized as a sequence of
  shorter single/double-quoted string literals, since that produces a misleading diagnostic at the
  wrong source location instead of naming the actual unsupported construct.
- Identifiers SHALL be restricted to the pinned grammar's ASCII `IDENT`/`SELECTOR` alphabet
  (`[_a-zA-Z][_a-zA-Z0-9]*`); a non-ASCII letter (e.g. `é`) is not part of any identifier and
  SHALL be reported as `SyntaxError`, not silently accepted as a Unicode identifier character.
  Numeric literals follow the same ASCII-digit restriction (`[0-9]`), and a decimal point SHALL
  only be consumed as part of a `FLOAT_LIT` when followed by at least one digit — `3.` alone is
  not a valid float literal and SHALL tokenize as an `IntLiteral` followed by a separate `.`.
- `SELECTOR ("." SELECTOR)* "{" ... "}"` (message/proto literal construction — every segment,
  including the first, is `SELECTOR`-governed, not `IDENT`-governed; see the reserved-word bullet
  below) and a leading `.` before an identifier (root/absolute-qualified name syntax, e.g.
  `.pkg.Type`) are both valid CEL primary
  forms under the pinned grammar; Profile v1 defers both, so the parser SHALL report
  `UnsupportedFeature` for each — never `SyntaxError` — at the point the grammar would otherwise
  accept them, and this SHALL hold regardless of nesting position (e.g. inside call arguments,
  parenthesized sub-expressions, or index expressions), not only at the top level of the
  expression being compiled. A `"{" ... "}"` immediately following an expression SHALL only be
  classified as a message literal when that expression is itself a qualified-name shape (an
  identifier, or a chain of pure member accesses rooted in one) — a call result, index result, or
  literal is never a valid message-literal receiver under the pinned grammar, so e.g. `1{}` SHALL
  be `SyntaxError`, not `UnsupportedFeature`.
- A deferred construct (arithmetic, the conditional operator, a list/map/message literal, a
  root-qualified name) SHALL only be classified as `UnsupportedFeature` after the parser has
  verified its own syntax is complete and well-formed under the pinned grammar; a dangling or
  incomplete instance (e.g. `a +` with no right-hand operand, `a ? b` with no `:` and false
  branch, a bare `.` with no following identifier, or an unterminated `[`/`{`) SHALL be
  `SyntaxError`, since it is not valid CEL syntax to begin with — only a fully-formed but deferred
  construct is `UnsupportedFeature`. This validation SHALL follow the pinned grammar's actual
  sub-structure, not a simplified approximation: the conditional operator's true branch is
  `ConditionalOr` precedence (an unparenthesized nested ternary there is `SyntaxError`, not
  `UnsupportedFeature`) while its false branch is the full recursive `Expr` (an unparenthesized
  nested ternary there is valid and SHALL also be fully validated); deferred arithmetic SHALL be
  absorbed at the `Relation = Addition [Relop Addition]` grammar level — i.e. as part of parsing
  each comparison operand — rather than only recognized as a flat trailer once a fully-reduced
  `ConditionalOr` has already returned, so arithmetic combined with a comparison anywhere in the
  expression (e.g. `a + b == c`, or nested inside a ternary branch as in `a ? b + c == d : e`) is
  classified correctly instead of producing a spurious `SyntaxError` about a missing `:`/`)`; a
  message literal's field keys (`SELECTOR ("." SELECTOR)* "{" field ":" value ...  "}"`) SHALL be bare
  identifiers, never an arbitrary expression — `Type{1: 2}` and `Type{'field': 1}` are
  `SyntaxError`, unlike a standalone map literal (`{1: 2}`) whose keys are arbitrary expressions
  and remains `UnsupportedFeature`; this bare-identifier-field-key requirement SHALL apply
  identically when the message-literal receiver is a root-qualified name (e.g.
  `.pkg.Type{1: 2}` SHALL be `SyntaxError` for the same reason `Type{1: 2}` is, not bypass field
  validation by virtue of being root-qualified). Message-literal-receiver eligibility SHALL be
  determined by parse state (whether the immediately preceding syntax was a qualified-name
  chain with no intervening call, index, or parenthesization), not by the shape of the resulting
  internal syntax node alone — a parenthesized qualified name (e.g. `(Type)`, `(pkg.Type)`) is
  syntactically indistinguishable in shape from an unparenthesized one once parsed, but per the
  pinned grammar the qualified-name prefix of a message literal is a primary-level production,
  not a generic postfix step usable after any expression that happens to evaluate to an
  identifier shape; `(Type){field: 1}` SHALL therefore be `SyntaxError`, even though
  `Type{field: 1}` is `UnsupportedFeature`.
- A root-qualified name (a leading `.` before an identifier) SHALL only consume its own leading
  `"." IDENT` (plus an immediately following call, e.g. `.f(...)`, mirroring a non-root-qualified
  free function call); any further `.member`, `.call(...)`, `[index]`, or message-literal step
  SHALL be handled by the same parsing path used for a non-root-qualified identifier chain, not a
  separate bespoke path — so `.pkg.f()` (root-qualified receiver call) and `.pkg.Type{field: 1}`
  (root-qualified message literal) parse completely instead of leaving a trailing `(`/`{`
  unconsumed, and every `.member`/`[index]` step in a root-qualified chain after the leading one
  is bounded by `MaxNestingDepth` through the same mechanism a non-root-qualified chain uses.
- A reserved identifier (see the existing `IDENT = SELECTOR - RESERVED` / `SELECTOR =
  identifier-regex - KEYWORD` distinction) that begins a primary expression is only valid as the
  root of a `SELECTOR ("." SELECTOR)*` chain that ITSELF terminates in a message literal
  (`"{" fieldInits "}"`) — a reserved root is never a valid plain qualified-name reference, call
  target, or index target on its own. This is a whole-chain requirement, not satisfied merely by
  the token immediately following the reserved root being `.`: the parser SHALL track this as
  pending state through the entire chain (every `.member` step the chain passes through, however
  many segments), resolving it only once the chain is known to have ended — satisfied the moment
  a message literal (`{...}`) is actually reached anywhere in the chain, and reported as
  `SyntaxError` if the chain instead ends via a call, an index, or simply no further tokens, with
  no message literal ever reached. This rule applies identically whether the reserved identifier
  is root-qualified or not: `package{field: 1}`, `package.Type{field: 1}`,
  `package.Type.Other{field: 1}`, and `.package{field: 1}` SHALL all be `UnsupportedFeature`
  (valid, deferred qualified-name/message-literal syntax), while `package()`, `package.Type`,
  `package.Type()`, `package.Type[0]`, `.package`, `.package()`, and `.package.Type` SHALL all be
  `SyntaxError` (a reserved word can never be a callable name, an index target, or a bare/plain
  qualified-name reference, root-qualified or not, unless the chain it roots eventually reaches a
  message literal). A reserved word used as an ordinary member-selector or receiver-call name on a
  non-reserved receiver (e.g. `x.package()`, `x.package`) is unaffected by this rule — it was
  already `SELECTOR`-governed under the existing member-access requirement, and the pending-state
  tracking above only ever originates from a reserved *root* token, never from a reserved word
  reached partway through a chain whose root was not itself reserved.
- The decision to classify an expression as `UnsupportedFeature` (as opposed to allowing parsing
  to continue toward a normal result) SHALL be deferred until the entire top-level expression has
  finished parsing successfully — every enclosing `(`/`[`/`{` matched with its closing
  `)`/`]`/`}`, every ternary's `:` and false branch present, full input consumed. A deferred
  construct's own syntax being valid SHALL NOT cause the parser to stop validating whatever
  encloses it; only the first such classification decision made SHALL be reported (first deferred
  construct encountered, in a diagnostic-stability sense — not necessarily the syntactically
  outermost one).
- A unary prefix chain (`"!" {"!"} Member` or `"-" {"-"} Member`) SHALL repeat only the same
  operator; mixing `!` and `-` in one prefix chain (e.g. `!-x`, `-!x`) has no valid CEL
  interpretation under the pinned grammar and SHALL be `SyntaxError`, not `UnsupportedFeature`.
- A reserved identifier (`as`, `break`, `const`, `continue`, `else`, `for`, `function`, `if`,
  `import`, `let`, `loop`, `package`, `namespace`, `return`, `var`, `void`, `while`) used as a bare
  primary expression reference SHALL be `SyntaxError`; the same word used in member-selector
  position (after `.`, as a member name or call name) SHALL parse successfully, per this spec's
  existing `IDENT = SELECTOR - RESERVED` / `SELECTOR = identifier-regex - KEYWORD` distinction —
  whether it resolves is compile-time binder territory (#326), not a parser concern.
- Free-function call syntax (`IDENT "(" args ")"` with no receiver) SHALL parse successfully even
  though Profile v1's function catalog declares no free functions; resolving whether the name is
  a known function is compile-time binder territory (#326), consistent with this spec's existing
  "calling an unknown function name is a compile-time `BindingError`" note.
- The tokenizer's whitespace token SHALL be restricted to exactly tab, newline, form-feed,
  carriage return, and space, matching the pinned grammar — not the broader .NET
  `char.IsWhiteSpace()` category, which also accepts non-ASCII Unicode space separators (e.g.
  U+00A0 NBSP) and other whitespace-category characters (e.g. U+000B vertical tab) the grammar
  does not include; such a character SHALL be reported as `SyntaxError`.
- An unescaped carriage return (`\r`), like an unescaped newline (`\n`), SHALL terminate a
  single/double-quoted or raw string literal with `SyntaxError` rather than being silently
  absorbed into the string's content.
- The tokenizer SHALL recognize the combined byte-plus-raw string prefix (`br"..."`/`Br"..."`/
  `bR"..."`/`BR"..."`, byte marker first per `BYTES_LIT : ("b"|"B") STRING_LIT` /
  `STRING_LIT : ["r"|"R"] STRING`), not only the single-marker forms. `\x`/`\X` (byte-value
  escapes) SHALL both be accepted as equivalent, and `\u` (4-digit Unicode escape) SHALL be
  accepted inside a byte-string literal exactly as in a string literal; only `\U` (8-digit
  Unicode escape) SHALL be rejected inside a byte-string literal — the pinned grammar's
  byte-string escape set includes `\u` but not `\U`.
- `MaxLiteralSize` SHALL bound element/entry count during list/map/message-literal syntax
  validation (each parsed element or `key : value` entry counted as it is validated), matching its
  documented "element count for list/map literals" contract, in addition to the already-enforced
  string/byte-string content-length bound.
- The parser SHALL enforce `MaxNestingDepth` against every postfix member-access (`.selector`) or
  indexing (`[...]`) step in a chain, not only against recursive constructs like parenthesized
  sub-expressions — the public `MaxNestingDepth` documentation explicitly lists "member access
  chains" as bounded by this limit.
- The parser SHALL enforce `MaxIdentifierCount` by counting each distinct identifier reference
  (a bare variable reference, a function name in a call, or a member-selector name) as it is
  consumed, producing `BudgetExceeded` the moment the count is exceeded — this is a purely
  syntactic count requiring no binder/schema information, so #325 enforces it directly rather than
  deferring it to #326's binder pass.
- On the first syntax error, unsupported-feature condition, or structural-limit violation, parsing
  SHALL stop and return exactly that one diagnostic (fail-fast, no error recovery).
- Every diagnostic produced by the tokenizer or parser SHALL use diagnostic category `"parser"`
  and SHALL carry `profileId` in `Parameters`, consistent with this spec's blanket diagnostic
  requirement. `BudgetExceeded` diagnostics from the tokenizer/parser SHALL additionally carry
  `limitName` and `observedValue`, matching the existing `MaxExpressionLength` `BudgetExceeded`
  parameter shape.

#### Scenario: Negative numeric literal is reported as a deferred feature, not accepted

- **WHEN** the expression `-5 == 5` is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic (arithmetic/unary-minus is
  deferred in Profile v1)

#### Scenario: Deferred arithmetic operator is distinguished from invented syntax

- **WHEN** the expression `a + b` is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic, not `SyntaxError`

#### Scenario: Invented operator syntax is a syntax error

- **WHEN** the expression `a => b` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic

#### Scenario: Deferred arithmetic is detected regardless of nesting position

- **WHEN** the expression `f(a + b)` (arithmetic inside a call argument) is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic, not a generic `SyntaxError`
  about an unexpected closing token

#### Scenario: A non-ASCII letter is rejected, not absorbed into an identifier

- **WHEN** the expression `é` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic

#### Scenario: A decimal point with no following digit does not form a float literal

- **WHEN** the source `3.` is tokenized
- **THEN** it produces an `IntLiteral` token for `3` followed by a separate `.` (`Dot`) token, not
  a single `FloatLiteral` token

#### Scenario: A float literal whose magnitude cannot be represented is a syntax error, not Infinity

- **WHEN** the source `1.99e90000009` (an exponent far beyond IEEE 754 double's range) is tokenized
- **THEN** tokenization fails with a `SyntaxError` diagnostic
- **AND** no `FloatLiteral` token carrying `double.PositiveInfinity` is produced

#### Scenario: Message literal syntax is deferred, not a syntax error

- **WHEN** the expression `Type{field: 1}` is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic, not `SyntaxError`

#### Scenario: Root-qualified name syntax is deferred, not a syntax error

- **WHEN** the expression `.pkg.Type` is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic, not `SyntaxError`

#### Scenario: A standalone \0 escape is rejected

- **WHEN** the string literal `'\0'` is tokenized
- **THEN** tokenization fails with a `SyntaxError` diagnostic (unknown escape sequence)

#### Scenario: A triple-quoted string opener is rejected as a unit, not re-tokenized as shorter literals

- **WHEN** `'''hello'''` is tokenized
- **THEN** tokenization fails with a single `SyntaxError` diagnostic whose span covers exactly the
  3-character `'''` opener
- **AND** the tokenizer does NOT succeed by re-interpreting the input as three adjacent
  single-quoted string literals (`''`, `'hello'`, `''`)

#### Scenario: A surrogate-range \u escape is rejected

- **WHEN** the string literal `'\uD800'` is tokenized
- **THEN** tokenization fails with a `SyntaxError` diagnostic

#### Scenario: Reserved identifier is rejected as a bare reference but accepted as a member name

- **WHEN** the expression `if == true` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic
- **WHEN** the expression `x.if` is parsed against a schema declaring variable `x`
- **THEN** the expression parses successfully (member-selector position is valid; whether `if`
  resolves against `x`'s schema is a binder concern)

#### Scenario: Unterminated string literal is a syntax error, not an unsupported feature

- **WHEN** the expression `'unterminated` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic

#### Scenario: Every parser diagnostic carries the parser category and profile identifier

- **WHEN** any tokenizer or parser diagnostic is produced
- **THEN** `diagnostic.Category` equals `"parser"`
- **AND** `diagnostic.Parameters["profileId"]` equals `"arch-linter/cel/v1"`

#### Scenario: A dangling deferred operator with no operand is a syntax error, not a deferred feature

- **WHEN** the expression `a +` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic, not `UnsupportedFeature`

#### Scenario: An incomplete conditional expression is a syntax error

- **WHEN** the expression `a ? b` (missing `:` and false branch) is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic, not `UnsupportedFeature`

#### Scenario: An unterminated list literal is a syntax error

- **WHEN** the expression `[` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic, not `UnsupportedFeature`

#### Scenario: A bare leading dot with no following identifier is a syntax error

- **WHEN** the expression `.` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic, not `UnsupportedFeature`

#### Scenario: A message literal on a non-qualified-name receiver is a syntax error

- **WHEN** the expression `1{}` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic, not `UnsupportedFeature` (an integer
  literal is never a valid message-literal receiver)

#### Scenario: MaxIdentifierCount is enforced by the parser

- **WHEN** `CelCompilationLimits.MaxIdentifierCount` is `1` and the expression `a || b` (two
  identifier references) is parsed
- **THEN** compilation fails with a `BudgetExceeded` diagnostic carrying
  `limitName = "MaxIdentifierCount"`

#### Scenario: MaxNestingDepth is enforced against a member-access chain

- **WHEN** `CelCompilationLimits.MaxNestingDepth` is exceeded by the length of a non-parenthesized
  member-access chain (e.g. `a.b.c.d.e`)
- **THEN** compilation fails with a `BudgetExceeded` diagnostic carrying
  `limitName = "MaxNestingDepth"`

#### Scenario: A non-ASCII whitespace character is rejected

- **WHEN** the expression `true` followed by U+00A0 (NBSP) followed by `== false` is tokenized
- **THEN** tokenization fails with a `SyntaxError` diagnostic

#### Scenario: An unescaped carriage return inside a string literal is rejected

- **WHEN** the string literal `'a` followed by an unescaped carriage return followed by `b'` is
  tokenized
- **THEN** tokenization fails with a `SyntaxError` diagnostic

#### Scenario: A ternary's true branch containing arithmetic is fully validated, not misreported as a missing colon

- **WHEN** the expression `a ? b + c : d` is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic, not a `SyntaxError` about a
  missing `:`

#### Scenario: A ternary's false branch may contain an unparenthesized nested ternary

- **WHEN** the expression `a ? b : c ? d : e` is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic

#### Scenario: A ternary's true branch may not contain an unparenthesized nested ternary

- **WHEN** the expression `a ? b ? c : d : e` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic

#### Scenario: Arithmetic combined with a comparison is classified correctly, not as a missing colon

- **WHEN** the expression `a ? b + c == d : e` is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic, not a `SyntaxError` about a
  missing `:`

#### Scenario: Arithmetic inside a comparison operand is deferred, not a syntax error

- **WHEN** the expression `a + b == c` is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic

#### Scenario: An unterminated parenthesized sub-expression containing arithmetic is a syntax error

- **WHEN** the expression `(a + b` (missing closing paren) is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic, not `UnsupportedFeature` — the
  missing `)` SHALL be caught before the deferred-arithmetic classification is ever considered

#### Scenario: An unterminated call containing an arithmetic argument is a syntax error

- **WHEN** the expression `f(a + b` (missing closing paren) is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic, not `UnsupportedFeature`

#### Scenario: A root-qualified message literal's field key is validated the same as a non-root-qualified one

- **WHEN** the expression `.pkg.Type{1: 2}` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic, not `UnsupportedFeature`

#### Scenario: A parenthesized qualified name is not a message-literal receiver

- **WHEN** the expression `(Type){field: 1}` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic, not `UnsupportedFeature`

#### Scenario: A parenthesized member-access chain is not a message-literal receiver

- **WHEN** the expression `(pkg.Type){field: 1}` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic, not `UnsupportedFeature`

#### Scenario: A root-qualified free function call parses completely

- **WHEN** the expression `.f()` is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic (not a `SyntaxError` about
  trailing input), since the call is fully consumed

#### Scenario: A root-qualified receiver call parses completely

- **WHEN** the expression `.pkg.f()` is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic (not a `SyntaxError` about
  trailing input)

#### Scenario: MaxNestingDepth bounds a root-qualified member-access chain

- **WHEN** `CelCompilationLimits.MaxNestingDepth` is exceeded by the length of a root-qualified
  member-access chain (e.g. `.a.b.c.d.e`)
- **THEN** compilation fails with a `BudgetExceeded` diagnostic carrying
  `limitName = "MaxNestingDepth"`

#### Scenario: A reserved word immediately followed by a message literal brace is deferred, not a syntax error

- **WHEN** the expression `package{field: 1}` is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic, not `SyntaxError`

#### Scenario: A reserved word as the root of a multi-segment qualified name is deferred

- **WHEN** the expression `package.Type{field: 1}` is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic, not `SyntaxError`

#### Scenario: A root-qualified reserved word immediately followed by a message literal brace is deferred

- **WHEN** the expression `.package{field: 1}` is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic, not `SyntaxError`

#### Scenario: A reserved word used as a callable free-function name is a syntax error

- **WHEN** the expression `package()` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic

#### Scenario: A root-qualified reserved word with nothing following is a syntax error

- **WHEN** the expression `.package` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic

#### Scenario: A root-qualified reserved word used as a call name is a syntax error

- **WHEN** the expression `.package()` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic

#### Scenario: A reserved word as a member/call selector on a non-reserved receiver is unaffected

- **WHEN** the expression `x.package()` is parsed
- **THEN** it parses successfully as a receiver call named `package` on `x`

#### Scenario: A reserved-word-rooted chain with no message literal anywhere is a syntax error

- **WHEN** the expression `package.Type` (no trailing `{...}`) is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic — a `.` immediately following the
  reserved root is not by itself sufficient; the chain must actually reach a message literal

#### Scenario: A reserved-word-rooted chain ending in a call is a syntax error

- **WHEN** the expression `package.Type()` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic

#### Scenario: A reserved-word-rooted chain ending in an index is a syntax error

- **WHEN** the expression `package.Type[0]` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic

#### Scenario: A root-qualified reserved-word-rooted chain with no message literal is a syntax error

- **WHEN** the expression `.package.Type` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic

#### Scenario: A root-qualified reserved-word-rooted chain ending in a call is a syntax error

- **WHEN** the expression `.package.Type()` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic

#### Scenario: A reserved-word-rooted chain reaching a message literal through multiple segments is deferred

- **WHEN** the expression `package.Type.Other{field: 1}` is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic, not `SyntaxError`

#### Scenario: A message literal field key must be a bare identifier

- **WHEN** the expression `Type{1: 2}` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic, not `UnsupportedFeature`

#### Scenario: A map literal key may be an arbitrary expression

- **WHEN** the expression `{1: 2}` (standalone brace, no qualified-name receiver) is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic (still deferred, just not a
  syntax error)

#### Scenario: Mixing '!' and '-' in a unary prefix chain is a syntax error

- **WHEN** the expression `!-x` is parsed
- **THEN** compilation fails with a `SyntaxError` diagnostic, not `UnsupportedFeature`

#### Scenario: A combined byte-and-raw string prefix is recognized

- **WHEN** the source `br'a\nb'` is tokenized
- **THEN** it produces a single `BytesLiteral` token whose decoded value is the four raw
  characters `a`, `\`, `n`, `b` (escapes are not processed)

#### Scenario: A 4-digit Unicode escape inside a byte-string literal is accepted

- **WHEN** the byte-string literal `b'A'` is tokenized
- **THEN** it produces a `BytesLiteral` token whose decoded value is `"A"`

#### Scenario: An 8-digit Unicode escape inside a byte-string literal is rejected

- **WHEN** the byte-string literal `b'\U00000041'` is tokenized
- **THEN** tokenization fails with a `SyntaxError` diagnostic

#### Scenario: MaxLiteralSize bounds list-literal element count during validation

- **WHEN** `CelCompilationLimits.MaxLiteralSize` is exceeded by the number of elements in a list
  literal being validated (e.g. `[1, 2, 3, 4, 5]` against a limit of `3`)
- **THEN** compilation fails with a `BudgetExceeded` diagnostic carrying
  `limitName = "MaxLiteralSize"`
