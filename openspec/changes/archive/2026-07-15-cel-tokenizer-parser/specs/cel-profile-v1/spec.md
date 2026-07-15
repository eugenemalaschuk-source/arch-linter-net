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
  expression being compiled.
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
