## ADDED Requirements

### Requirement: Tokenizer and parser implementation scope for Profile v1

`ArchLinterNet.CEL`'s tokenizer and parser (internal `ArchLinterNet.CEL.Parsing` namespace) SHALL
implement exactly the following lexical and syntactic scope for Profile v1, in addition to the
precedence, associativity, and operator/type/function requirements already fixed elsewhere in
this spec:

- Numeric literals SHALL NOT include a sign; `-` SHALL always tokenize as a standalone operator
  token. A leading `-` before a numeric literal SHALL be reported as `UnsupportedFeature`
  (arithmetic/unary-minus is deferred), never accepted as part of the literal.
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
  SHALL be reported as `SyntaxError`.
- Identifiers SHALL be restricted to the pinned grammar's ASCII `IDENT`/`SELECTOR` alphabet
  (`[_a-zA-Z][_a-zA-Z0-9]*`); a non-ASCII letter (e.g. `é`) is not part of any identifier and
  SHALL be reported as `SyntaxError`, not silently accepted as a Unicode identifier character.
  Numeric literals follow the same ASCII-digit restriction (`[0-9]`), and a decimal point SHALL
  only be consumed as part of a `FLOAT_LIT` when followed by at least one digit — `3.` alone is
  not a valid float literal and SHALL tokenize as an `IntLiteral` followed by a separate `.`.
- `IDENT ("." IDENT)* "{" ... "}"` (message/proto literal construction) and a leading `.` before
  an identifier (root/absolute-qualified name syntax, e.g. `.pkg.Type`) are both valid CEL primary
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
  construct is `UnsupportedFeature`.
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

#### Scenario: Message literal syntax is deferred, not a syntax error

- **WHEN** the expression `Type{field: 1}` is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic, not `SyntaxError`

#### Scenario: Root-qualified name syntax is deferred, not a syntax error

- **WHEN** the expression `.pkg.Type` is parsed
- **THEN** compilation fails with an `UnsupportedFeature` diagnostic, not `SyntaxError`

#### Scenario: A standalone \0 escape is rejected

- **WHEN** the string literal `'\0'` is tokenized
- **THEN** tokenization fails with a `SyntaxError` diagnostic (unknown escape sequence)

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
