## 1. Tokenizer

- [x] 1.1 `CelTokenKind` — every Profile v1 + deferred-CEL token kind (literals, identifiers,
      keywords, operators, punctuation, EOF).
- [x] 1.2 `CelToken` — kind, `CelSourceSpan`, decoded value (string/long/double/bool as
      applicable), reserved-identifier flag.
- [x] 1.3 `CelTokenizer` — full lexer per design.md decisions 1-3; enforces `MaxTokenCount` and
      `MaxLiteralSize`; every diagnostic carries `profileId`.

## 2. Syntax tree

- [x] 2.1 `CelSyntaxNode` abstract base (span-carrying).
- [x] 2.2 Literal nodes (bool/int/float/string), identifier node.
- [x] 2.3 `CelUnarySyntax`, `CelBinarySyntax` (+ `CelUnaryOperator`/`CelBinaryOperator` for the
      exactly-v1 operator set).
- [x] 2.4 `CelMemberAccessSyntax`, `CelIndexSyntax`, `CelCallSyntax`.

## 3. Parser

- [x] 3.1 Precedence-climbing parser matching the frozen precedence/associativity table
      (`||` < `&&` < non-associative comparisons < unary `!` < postfix `.`/`[]`).
- [x] 3.2 Non-associative comparison chaining rejected as `SyntaxError`.
- [x] 3.3 Full input consumption enforced (trailing tokens after a complete expression are
      `SyntaxError`).
- [x] 3.4 Deferred-but-valid CEL syntax (arithmetic, `?:`, `null`/`uint`/`bytes` literals)
      produces `UnsupportedFeature`, never `SyntaxError`.
- [x] 3.5 Reserved-identifier position rule (design.md decision 4).
- [x] 3.6 `MaxNestingDepth` and `MaxAstNodeCount` enforced as `BudgetExceeded`.

## 4. Wiring

- [x] 4.1 `CelEnvironment.CompilePredicate`/`Compile` run tokenizer+parser after the
      `MaxExpressionLength` gate; parse failures short-circuit with real diagnostics; valid syntax
      still falls through to `NotYetImplemented`.
- [x] 4.2 Existing stub-contract tests (`CelExternalConsumerSampleTests`,
      `CelInternalApiCoverageTests`) still pass unmodified.

## 5. Tests

- [x] 5.1 Tokenizer unit tests (every token kind, escapes, adversarial malformed input).
- [x] 5.2 Parser unit tests (every node kind, precedence, associativity, source spans).
- [x] 5.3 Negative conformance tests (invented syntax, proprietary operators, alternate call
      forms).
- [x] 5.4 Deferred-CEL `UnsupportedFeature` tests (arithmetic, `?:`, `null`, `uint`, `bytes`).
- [x] 5.5 Adversarial structural-limit tests (`MaxTokenCount`, `MaxAstNodeCount`,
      `MaxNestingDepth`, `MaxLiteralSize`).
- [x] 5.6 `CelEnvironment.CompilePredicate`/`Compile` wiring tests (syntax error surfaces span;
      valid syntax still `NotYetImplemented`).

## 6. Docs

- [x] 6.1 `docs/internal/cel-engine-architecture.md` parser/language-evolution section updated to
      match shipped scope.

## 7. Validation

- [x] 7.1 `rtk make fmt`
- [x] 7.2 `rtk make acceptance`
